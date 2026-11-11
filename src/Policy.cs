using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PCLock
{
    /// <summary>
    /// 新增策略的纯函数层（不依赖 WinForms / 注册表，可被 tests\run.bat 直接编译）。
    /// 借鉴 StudyOnTV / CleanTV-With-ChildLock / PoHW 的规则模型，但保持 PCLock 原有
    /// 防作弊行为不变：所有新规则默认关闭，旧配置格式按原语义解释。
    /// </summary>

    // ===== 按星期 + 时段的多规则（P0-4）=====
    public sealed class WindowRule
    {
        /// <summary>bit0=周一 ... bit6=周日；127=每天</summary>
        public int DaysMask;
        public TimeSpan Start;
        public TimeSpan End;
        /// <summary>true=仅时段内需要答题（时段外自由、不扣时长）；false=仅时段内可用（时段外硬锁）</summary>
        public bool QuizOnly;

        public bool Matches(DayOfWeek day, TimeSpan now)
        {
            int minutes = (int)now.TotalMinutes;
            int start = (int)Start.TotalMinutes;
            int end = (int)End.TotalMinutes;
            if (start == end) return DaySet(day);                 // 起止相同 = 全天
            if (start < end)
                return minutes >= start && minutes <= end && DaySet(day);
            // 跨午夜：22:00-06:00。上半段属当天，下半段属前一天（与 CleanTV 规则一致）
            if (minutes >= start) return DaySet(day);
            if (minutes <= end) return DaySet(PreviousDay(day));
            return false;
        }

        bool DaySet(DayOfWeek day)
        {
            int iso = day == DayOfWeek.Sunday ? 7 : (int)day; // Monday=1 ... Saturday=6
            return (DaysMask & (1 << (iso - 1))) != 0;
        }

        static DayOfWeek PreviousDay(DayOfWeek day)
        {
            return day == DayOfWeek.Monday ? DayOfWeek.Sunday : day - 1;
        }
    }

    public sealed class WindowDecision
    {
        public bool HasRules;        // 是否配置了有效规则
        public bool HardLock;        // 不在「仅时段内可用」窗口内 → 硬锁
        public bool QuizControlled; // 处于需答题管控时段内 → 正常计时
        public bool FreeUse;         // 全部为 quiz-only 且时段外 → 自由使用、不计时
        public string Description;
    }

    public static class WindowRules
    {
        /// <summary>
        /// 规则串格式：段间用分号；段内可带星期前缀与 @quiz 后缀。
        ///   旧格式（无竖线）完全兼容：08:00-12:00;14:00-21:00 → 每天、硬锁语义。
        ///   新格式：1-5|16:00-21:00@quiz;6,7|08:00-22:00
        /// 星期：1-5 或 1,3,5 或 6,7；空 / * = 每天。@quiz = 仅时段内需要答题。
        /// </summary>
        public static List<WindowRule> Parse(string spec)
        {
            List<WindowRule> rules = new List<WindowRule>();
            if (string.IsNullOrWhiteSpace(spec)) return rules;
            foreach (string raw in spec.Split(';'))
            {
                string seg = raw.Trim();
                if (seg.Length == 0) continue;
                bool quizOnly = false;
                if (seg.EndsWith("@quiz", StringComparison.OrdinalIgnoreCase))
                {
                    quizOnly = true;
                    seg = seg.Substring(0, seg.Length - 5).Trim();
                }
                int bar = seg.IndexOf('|');
                string daysPart = bar >= 0 ? seg.Substring(0, bar).Trim() : "";
                string timePart = bar >= 0 ? seg.Substring(bar + 1).Trim() : seg;

                int dash = timePart.IndexOf('-');
                if (dash < 0) continue;
                TimeSpan? a = ParseHm(timePart.Substring(0, dash));
                TimeSpan? b = ParseHm(timePart.Substring(dash + 1));
                if (!a.HasValue || !b.HasValue) continue;
                int mask = ParseDays(daysPart);
                if (mask == 0) continue;
                WindowRule r = new WindowRule();
                r.DaysMask = mask;
                r.Start = a.Value;
                r.End = b.Value;
                r.QuizOnly = quizOnly;
                rules.Add(r);
            }
            return rules;
        }

        static int ParseDays(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "*" || s == "每天") return 127;
            int mask = 0;
            foreach (string part in s.Split(','))
            {
                string p = part.Trim();
                if (p.Length == 0) continue;
                int range = p.IndexOf('-');
                if (range > 0)
                {
                    int lo, hi;
                    if (!int.TryParse(p.Substring(0, range), out lo) ||
                        !int.TryParse(p.Substring(range + 1), out hi)) continue;
                    if (lo < 1 || hi > 7 || lo > hi) continue;
                    for (int d = lo; d <= hi; d++) mask |= 1 << (d - 1);
                }
                else
                {
                    int d;
                    if (!int.TryParse(p, out d) || d < 1 || d > 7) continue;
                    mask |= 1 << (d - 1);
                }
            }
            return mask;
        }

        static TimeSpan? ParseHm(string s)
        {
            s = s.Trim();
            int colon = s.IndexOf(':');
            if (colon < 0) return null;
            int h, m;
            if (!int.TryParse(s.Substring(0, colon), out h)) return null;
            if (!int.TryParse(s.Substring(colon + 1), out m)) return null;
            if (h < 0 || h > 23 || m < 0 || m > 59) return null;
            return new TimeSpan(h, m, 0);
        }

        /// <summary>纯函数：按当前时刻判定管控状态（默认关闭时 HasRules=false，行为与旧版一致）</summary>
        public static WindowDecision Evaluate(string spec, DateTime now)
        {
            WindowDecision d = new WindowDecision();
            List<WindowRule> rules = Parse(spec);
            d.HasRules = rules.Count > 0;
            if (!d.HasRules)
            {
                d.Description = "未配置多规则时段";
                return d;
            }
            bool matched = false, matchedQuizOnly = true;
            bool hasHardRule = false;
            foreach (WindowRule r in rules)
            {
                if (!r.QuizOnly) hasHardRule = true;
                if (r.Matches(now.DayOfWeek, now.TimeOfDay))
                {
                    matched = true;
                    if (!r.QuizOnly) matchedQuizOnly = false;
                }
            }
            if (matched)
            {
                d.QuizControlled = true;
                d.Description = matchedQuizOnly ? "处于答题管控时段" : "处于允许使用时段";
            }
            else if (hasHardRule)
            {
                d.HardLock = true;
                d.Description = "不在允许使用时段（硬锁）";
            }
            else
            {
                d.FreeUse = true;
                d.Description = "自由使用（不计时、不扣时长）";
            }
            return d;
        }
    }

    // ===== 每日额度（P0-3）=====
    public static class DailyQuota
    {
        /// <summary>limitMinutes / unlockLimit 为 0 表示该维度关闭</summary>
        public static bool ShouldLock(int usedSeconds, int limitMinutes, int unlockCount, int unlockLimit)
        {
            if (limitMinutes > 0 && usedSeconds >= limitMinutes * 60) return true;
            if (unlockLimit > 0 && unlockCount >= unlockLimit) return true;
            return false;
        }
    }

    // ===== 错题库 / 累计加练池（P0-6）=====
    public struct PracticeState
    {
        public int WrongAccum;   // 未凑满一个阈值的累计答错数
        public int ExtraPending; // 待完成的加练题数（跨天、重启保留）
    }

    public static class ExtraPractice
    {
        /// <summary>threshold / perThreshold 为 0 时该功能关闭，状态不变</summary>
        public static PracticeState OnWrong(PracticeState s, int threshold, int perThreshold)
        {
            if (threshold <= 0 || perThreshold <= 0) return s;
            s.WrongAccum++;
            while (s.WrongAccum >= threshold)
            {
                s.WrongAccum -= threshold;
                s.ExtraPending += perThreshold;
            }
            return s;
        }

        public static PracticeState OnExtraCorrect(PracticeState s)
        {
            if (s.ExtraPending > 0) s.ExtraPending--;
            return s;
        }

        public static bool CanUnlock(PracticeState s) { return s.ExtraPending == 0; }

        /// <summary>家长关闭新增加练后，已欠的题仍需完成，但不再累计新错</summary>
        public static PracticeState ClearAccum(PracticeState s)
        {
            s.WrongAccum = 0;
            return s;
        }
    }

    // ===== 护眼强制休息（P0-5）=====
    public static class RestCooldown
    {
        /// <summary>截止时间戳用 UTC Ticks 持久化，重启不清零</summary>
        public static bool IsResting(long restUntilUtcTicks, long nowUtcTicks)
        {
            return restUntilUtcTicks > 0 && restUntilUtcTicks > nowUtcTicks;
        }

        public static int RemainingSeconds(long restUntilUtcTicks, long nowUtcTicks)
        {
            if (!IsResting(restUntilUtcTicks, nowUtcTicks)) return 0;
            double sec = (restUntilUtcTicks - nowUtcTicks) / (double)TimeSpan.TicksPerSecond;
            long ceil = (long)Math.Ceiling(sec);
            return ceil > int.MaxValue ? int.MaxValue : (int)ceil;
        }

        public static long Deadline(long nowUtcTicks, int minutes)
        {
            if (minutes <= 0) return 0;
            return nowUtcTicks + (long)minutes * 60 * TimeSpan.TicksPerSecond;
        }
    }

    // ===== 时间回拨主动检测（P1-12，默认关闭）=====
    public static class TimeTamper
    {
        /// <summary>容差内（NTP 校时、家长手动微调）不误报；超过容差的大幅回拨视为篡改</summary>
        public static bool IsBackward(long lastKnownUtcTicks, long nowUtcTicks, int toleranceSeconds)
        {
            if (lastKnownUtcTicks <= 0) return false;
            long tolerance = (long)toleranceSeconds * TimeSpan.TicksPerSecond;
            return nowUtcTicks < lastKnownUtcTicks - tolerance;
        }
    }

    // ===== 星级时长奖励（P1-8，默认关闭）=====
    public static class StarReward
    {
        public static int StarOf(Question q)
        {
            if (q == null) return 1;
            int star = q.Grade;                 // 1..6 年级 → 1..5 星
            if (q.Type == QType.MathCalc) star--; // 口算题相对简单，降一星
            if (star < 1) star = 1;
            if (star > 5) star = 5;
            return star;
        }

        /// <summary>按答对题目的星级累计奖励秒数；单次上限 3600 秒，防止配置写错导致无限加时</summary>
        public static int RewardSeconds(List<Question> correctQuestions, int[] rewardMinutes)
        {
            if (correctQuestions == null || rewardMinutes == null || rewardMinutes.Length != 5) return 0;
            long total = 0;
            foreach (Question q in correctQuestions)
            {
                int m = rewardMinutes[StarOf(q) - 1];
                if (m < 0) m = 0;
                total += (long)m * 60;
                if (total > 3600) return 3600;
            }
            return (int)total;
        }
    }

    // ===== 答题反馈（P0-2）=====
    public static class AnswerFeedback
    {
        /// <summary>答错时展示的正确答案与解析；保留「答错清零」，仅增加反馈</summary>
        public static string Build(Question q)
        {
            if (q == null) return "";
            StringBuilder sb = new StringBuilder();
            if (q.Type == QType.MathCalc)
            {
                sb.Append(Strings.Fmt("正确答案：{0}", q.Answer.ToString(CultureInfo.InvariantCulture)));
                sb.Append(Strings.Fmt("　（{0}）", q.Prompt));
            }
            else if (q.Options != null && q.CorrectIndex >= 0 && q.CorrectIndex < q.Options.Length)
            {
                sb.Append(Strings.Fmt("正确答案：{0}", q.Options[q.CorrectIndex]));
                if (!string.IsNullOrEmpty(q.Explanation))
                    sb.Append(Strings.Fmt("　解析：{0}", q.Explanation));
            }
            return sb.ToString();
        }
    }

    // ===== 答题判定可配（P0-7）=====
    public static class QuizConfig
    {
        public const int DefaultQuestionCount = 10;
        public const int MaxQuestionCount = 30;

        public static int ClampQuestionCount(int n)
        {
            if (n < 1) return DefaultQuestionCount;
            if (n > MaxQuestionCount) return MaxQuestionCount;
            return n;
        }

        /// <summary>需连续答对 N 道（默认 = 题量，即全部答对）；答错仍整批清零，防作弊语义不变</summary>
        public static int ClampStreak(int streak, int questionCount)
        {
            if (streak < 1) return questionCount;
            if (streak > questionCount) return questionCount;
            return streak;
        }

        /// <summary>
        /// 题库因学期/题库名过滤而不足题量时，把「需连对」钳到实际题数。
        /// 不钳会出现「状态栏写共 10 题、实际只有 3 题」——数字对不上，且配置形同失效。
        /// </summary>
        public static int EffectiveStreak(int configured, int available)
        {
            if (available < 1) return 1;
            if (configured < 1) return available;
            return configured > available ? available : configured;
        }
    }
}
