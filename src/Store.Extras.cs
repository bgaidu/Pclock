using System;
using System.Collections.Generic;
using System.Globalization;
using PCLockConstants = PCLock.Constants;

namespace PCLock
{
    /// <summary>
    /// 新增配置的持久化（P0/P1）。全部为「默认关闭」，覆盖安装后行为与旧版完全一致。
    /// 按用户隔离的键沿用 U:&lt;user&gt;:&lt;field&gt;；全局键用于首次密码、时间回拨等。
    /// </summary>
    public static partial class Store
    {
        // === 首次运行强制设置密码（P0-1）===
        // 旧安装没有 PinSetupDone 值：默认 1（视为已完成），避免升级后强制改密打扰。
        public static bool NeedsFirstPinSetup() { return GetInt("PinSetupDone", 1) == 0; }
        public static void MarkFirstPinSetupDone() { SetInt("PinSetupDone", 1); }
        /// <summary>仍在使用默认 PIN 1234（用于设置页提醒，验证一次约 0.2 秒）</summary>
        public static bool IsDefaultPinStillInUse() { return VerifyPin(PCLockConstants.DefaultPin); }

        // === 多规则时段（P0-4）===
        /// <summary>新规则串；为空表示使用旧 Win 字符串（完全兼容旧语义）</summary>
        public static string GetRules(string user) { return GetStr(UKey(user, "Rules"), ""); }
        public static void SetRules(string user, string spec) { SetStr(UKey(user, "Rules"), spec == null ? "" : spec); }

        public static WindowDecision GetWindowDecision(string user, DateTime now)
        {
            return WindowRules.Evaluate(GetRules(user), now);
        }

        // === 每日额度（P0-3）===
        public static int GetDailyLimitMinutes(string user) { return ClampMin(GetInt(UKey(user, "DailyLimit"), 0), 0, 1440); }
        public static void SetDailyLimitMinutes(string user, int m) { SetInt(UKey(user, "DailyLimit"), ClampMin(m, 0, 1440)); }

        public static int GetUnlockLimit(string user) { return ClampMin(GetInt(UKey(user, "UnlockLimit"), 0), 0, 99); }
        public static void SetUnlockLimit(string user, int n) { SetInt(UKey(user, "UnlockLimit"), ClampMin(n, 0, 99)); }

        /// <summary>今日解锁次数（跨天自动归零）</summary>
        public static int GetUnlockCountToday(string user)
        {
            if (GetStr(UKey(user, "UnlockDate"), "") != DateTime.Now.ToString("yyyy-MM-dd")) return 0;
            return GetInt(UKey(user, "UnlockCount"), 0);
        }
        public static void IncrementUnlockCount(string user)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string dateKey = UKey(user, "UnlockDate");
            int n = GetStr(dateKey, "") == today ? GetInt(UKey(user, "UnlockCount"), 0) : 0;
            SetStr(dateKey, today);
            SetInt(UKey(user, "UnlockCount"), n + 1);
        }

        // === 答题判定与反馈（P0-2 / P0-7）===
        public static int GetQuestionCount(string user)
        {
            return QuizConfig.ClampQuestionCount(GetInt(UKey(user, "QCount"), QuizConfig.DefaultQuestionCount));
        }
        public static void SetQuestionCount(string user, int n)
        {
            SetInt(UKey(user, "QCount"), QuizConfig.ClampQuestionCount(n));
        }
        public static int GetRequiredStreak(string user)
        {
            int n = GetInt(UKey(user, "QStreak"), GetQuestionCount(user));
            return QuizConfig.ClampStreak(n, GetQuestionCount(user));
        }
        public static void SetRequiredStreak(string user, int n)
        {
            SetInt(UKey(user, "QStreak"), QuizConfig.ClampStreak(n, GetQuestionCount(user)));
        }
        public static bool IsFeedbackEnabled(string user) { return GetInt(UKey(user, "Feedback"), 1) == 1; }
        public static void SetFeedbackEnabled(string user, bool on) { SetInt(UKey(user, "Feedback"), on ? 1 : 0); }

        // === 错题库 / 加练池（P0-6，跨天与重启保留）===
        public static int GetExtraThreshold(string user) { return ClampMin(GetInt(UKey(user, "ExtraThreshold"), 0), 0, 99); }
        public static void SetExtraThreshold(string user, int n) { SetInt(UKey(user, "ExtraThreshold"), ClampMin(n, 0, 99)); }
        public static int GetExtraPerThreshold(string user) { return ClampMin(GetInt(UKey(user, "ExtraPer"), 0), 0, 20); }
        public static void SetExtraPerThreshold(string user, int n) { SetInt(UKey(user, "ExtraPer"), ClampMin(n, 0, 20)); }

        public static PracticeState GetPracticeState(string user)
        {
            PracticeState s = new PracticeState();
            s.WrongAccum = GetInt(UKey(user, "WrongAccum"), 0);
            s.ExtraPending = GetInt(UKey(user, "ExtraPending"), 0);
            return s;
        }
        public static void SetPracticeState(string user, PracticeState s)
        {
            SetInt(UKey(user, "WrongAccum"), s.WrongAccum < 0 ? 0 : s.WrongAccum);
            SetInt(UKey(user, "ExtraPending"), s.ExtraPending < 0 ? 0 : s.ExtraPending);
        }

        // === 护眼强制休息 / 家长临时放行（P0-5）===
        public static int GetRestMinutes(string user) { return ClampMin(GetInt(UKey(user, "RestMin"), 0), 0, 480); }
        public static void SetRestMinutes(string user, int m) { SetInt(UKey(user, "RestMin"), ClampMin(m, 0, 480)); }

        public static long GetRestUntilUtc() { return GetLong("RestUntilUtc", 0); }
        public static void SetRestUntilUtc(long ticks) { SetLong("RestUntilUtc", ticks); }
        public static void ClearRest() { SetRestUntilUtc(0); }

        public static int GetTempPassMinutes(string user)
        {
            return ClampMin(GetInt(UKey(user, "TempPassMin"), PCLockConstants.DefaultTempPassMinutes), 1, 240);
        }
        public static void SetTempPassMinutes(string user, int m)
        {
            SetInt(UKey(user, "TempPassMin"), ClampMin(m, 1, 240));
        }

        // === 时间回拨主动检测（P1-12，默认关闭）===
        public static bool IsAntiTamperEnabled() { return GetInt("AntiTamper", 0) == 1; }
        public static void SetAntiTamperEnabled(bool on) { SetInt("AntiTamper", on ? 1 : 0); }
        public static long GetLastKnownUtc() { return GetLong("LastKnownUtc", 0); }
        public static void SetLastKnownUtc(long ticks) { SetLong("LastKnownUtc", ticks); }
        public static bool IsTamperLocked() { return GetInt("TamperLock", 0) == 1; }
        public static void SetTamperLock(int v) { SetInt("TamperLock", v); }

        // === 星级奖励（P1-8，默认关闭）===
        public static bool IsStarRewardEnabled(string user) { return GetInt(UKey(user, "StarEnabled"), 0) == 1; }
        public static void SetStarRewardEnabled(string user, bool on) { SetInt(UKey(user, "StarEnabled"), on ? 1 : 0); }

        /// <summary>1-5 星每题奖励分钟数，字符串 "1,2,3,4,5"；非法值回退默认</summary>
        public static int[] GetStarRewards(string user)
        {
            int[] r = ParseStarRewards(GetStr(UKey(user, "StarRewards"), PCLockConstants.DefaultStarRewards));
            return r;
        }
        public static void SetStarRewards(string user, int[] rewards)
        {
            if (rewards == null || rewards.Length != 5) return;
            string[] parts = new string[5];
            for (int i = 0; i < 5; i++) parts[i] = ClampMin(rewards[i], 0, 60).ToString(CultureInfo.InvariantCulture);
            SetStr(UKey(user, "StarRewards"), string.Join(",", parts));
        }
        public static int[] ParseStarRewards(string spec)
        {
            int[] def = new int[] { 1, 2, 3, 4, 5 };
            if (string.IsNullOrEmpty(spec)) return def;
            string[] parts = spec.Split(',');
            if (parts.Length != 5) return def;
            int[] r = new int[5];
            for (int i = 0; i < 5; i++)
            {
                int v;
                if (!int.TryParse(parts[i].Trim(), out v)) return def;
                r[i] = ClampMin(v, 0, 60);
            }
            return r;
        }

        /// <summary>本次答题累计的星级奖励秒数（解锁时一次性取出并清零）</summary>
        public static void SetPendingReward(int sec) { SetInt("PendingRewardSec", sec < 0 ? 0 : sec); }
        public static int TakePendingReward()
        {
            int v = GetInt("PendingRewardSec", 0);
            SetInt("PendingRewardSec", 0);
            return v;
        }

        // === 练习模式（P1-9，默认关闭）===
        public static bool IsPracticeEnabled(string user) { return GetInt(UKey(user, "PracticeOn"), 0) == 1; }
        public static void SetPracticeEnabled(string user, bool on) { SetInt(UKey(user, "PracticeOn"), on ? 1 : 0); }

        // === 7 天使用记录（P1-10）===
        // 与 GetUsedToday 独立：按 yyyyMMdd 存当天累计秒数，覆盖写，供统计视图读取。
        public static void RecordUsageDay(string user, DateTime day, int seconds)
        {
            if (seconds < 0) seconds = 0;
            SetInt(UKey(user, "Used:" + day.ToString("yyyyMMdd")), seconds);
        }
        public static int[] GetUsageLastDays(string user, DateTime today, int days)
        {
            DateTime[] ds = UsageCsv.LastNDays(today, days);
            int[] r = new int[ds.Length];
            for (int i = 0; i < ds.Length; i++)
                r[i] = GetInt(UKey(user, "Used:" + ds[i].ToString("yyyyMMdd")), 0);
            return r;
        }

        // === 本批新增：学习策略 / 题型 / 体验 / 多语言 ===
        public static int GetPreferDifficulty(string user) { return ClampMin(GetInt(UKey(user, "PreferDiff"), 0), 0, 3); }
        public static void SetPreferDifficulty(string user, int v) { SetInt(UKey(user, "PreferDiff"), ClampMin(v, 0, 3)); }

        public static int GetTerm(string user) { return ClampMin(GetInt(UKey(user, "Term"), 0), 0, 2); }
        public static void SetTerm(string user, int v) { SetInt(UKey(user, "Term"), ClampMin(v, 0, 2)); }

        public static string GetBankFilter(string user) { return GetStr(UKey(user, "Bank"), ""); }
        public static void SetBankFilter(string user, string v) { SetStr(UKey(user, "Bank"), v == null ? "" : v.Trim()); }

        public static bool IsMathChoiceOn(string user) { return GetInt(UKey(user, "MathChoice"), 0) == 1; }
        public static void SetMathChoiceOn(string user, bool on) { SetInt(UKey(user, "MathChoice"), on ? 1 : 0); }
        public static bool IsChineseWordOn(string user) { return GetInt(UKey(user, "ChineseWord"), 0) == 1; }
        public static void SetChineseWordOn(string user, bool on) { SetInt(UKey(user, "ChineseWord"), on ? 1 : 0); }
        public static bool IsTrueFalseOn(string user) { return GetInt(UKey(user, "TrueFalse"), 0) == 1; }
        public static void SetTrueFalseOn(string user, bool on) { SetInt(UKey(user, "TrueFalse"), on ? 1 : 0); }

        public static int GetFeedbackHoldSec(string user) { return ClampMin(GetInt(UKey(user, "FeedbackHold"), 0), 0, 120); }
        public static void SetFeedbackHoldSec(string user, int v) { SetInt(UKey(user, "FeedbackHold"), ClampMin(v, 0, 120)); }

        public static string GetEncourageText(string user) { return GetStr(UKey(user, "Encourage"), "答对了！"); }
        public static void SetEncourageText(string user, string v)
        {
            SetStr(UKey(user, "Encourage"), string.IsNullOrEmpty(v) ? "答对了！" : v);
        }

        public static bool IsChildGradeOn(string user) { return GetInt(UKey(user, "ChildGrade"), 0) == 1; }
        public static void SetChildGradeOn(string user, bool on) { SetInt(UKey(user, "ChildGrade"), on ? 1 : 0); }

        public static bool IsReviewOn(string user) { return GetInt(UKey(user, "ReviewOn"), 0) == 1; }
        public static void SetReviewOn(string user, bool on) { SetInt(UKey(user, "ReviewOn"), on ? 1 : 0); }

        /// <summary>间隔重复状态按题 key 存注册表（跨重启保留）</summary>
        public static ReviewState GetReviewState(string user, string key)
        {
            return SpacedRepetition.Parse(GetStr(UKey(user, "Rev:" + key), ""));
        }
        public static void SetReviewState(string user, string key, ReviewState s)
        {
            SetStr(UKey(user, "Rev:" + key), SpacedRepetition.Serialize(s));
        }

        /// <summary>按题统计（shown|answered|correct），跨重启保留</summary>
        public static string GetQuestionStat(string user, string key)
        {
            return GetStr(UKey(user, "Stat:" + key), "");
        }
        public static void RecordAnswer(string user, Question q, bool correct)
        {
            if (q == null) return;
            string key = QuestionStats.KeyOf(q);
            SetStr(UKey(user, "Stat:" + key),
                QuestionStats.Update(GetQuestionStat(user, key), correct));
            ReviewState s = GetReviewState(user, key);
            s = SpacedRepetition.Review(s, correct, DateTime.UtcNow.Ticks);
            SetReviewState(user, key, s);
            answerCount++;
            if (answerCount % 25 == 0) PruneQuestionStats(user);   // 偶尔清一次，避免每次作答都枚举注册表
        }

        // === 运行期维护（第四批）：日志保留期 / 统计键上限 ===
        public static int GetLogKeepDays()
        {
            return ClampMin(GetInt("LogKeepDays", PCLockConstants.DefaultLogKeepDays), 0, 3650);
        }
        public static void SetLogKeepDays(int days) { SetInt("LogKeepDays", ClampMin(days, 0, 3650)); }

        public static int GetStatCap()
        {
            return ClampMin(GetInt("StatCap", PCLockConstants.DefaultStatCap), 50, 5000);
        }
        public static void SetStatCap(int n) { SetInt("StatCap", ClampMin(n, 50, 5000)); }

        static int answerCount;

        /// <summary>
        /// 统计键超过上限时按「作答次数少 + 正确率高」优先删除，连带删掉对应的 SM-2 进度键。
        /// 一年下来每用户可产生数千个 Stat:/Rev: 值，不清会持续膨胀注册表。
        /// </summary>
        public static int PruneQuestionStats(string user)
        {
            try
            {
                int cap = GetStatCap();
                string[] keys = ListStatKeys(user);
                if (!Housekeeping.ShouldPruneStat(keys.Length, cap)) return 0;
                string[] vals = new string[keys.Length];
                for (int i = 0; i < keys.Length; i++) vals[i] = GetQuestionStat(user, keys[i]);
                List<string> drop = Housekeeping.SelectKeysToDrop(keys, vals, cap);
                int n = 0;
                foreach (string k in drop)
                {
                    try { root.DeleteValue(UKey(user, "Stat:" + k), false); n++; }
                    catch (Exception) { }
                    try { root.DeleteValue(UKey(user, "Rev:" + k), false); }
                    catch (Exception) { }
                }
                if (n > 0) Log("统计键清理：用户 " + user + " 删除 " + n + " 条");
                return n;
            }
            catch (Exception ex)
            {
                Log("PruneQuestionStats 失败: " + ex.Message);
                return 0;
            }
        }

        /// <summary>列出有统计记录的题 key（供弱项分析 / 到期复习使用）</summary>
        public static string[] ListStatKeys(string user)
        {
            List<string> r = new List<string>();
            string prefix = UKey(user, "Stat:");
            try
            {
                if (root == null) return r.ToArray();
                foreach (string name in root.GetValueNames())
                    if (name.StartsWith(prefix, StringComparison.Ordinal)) r.Add(name.Substring(prefix.Length));
            }
            catch (Exception) { }
            return r.ToArray();
        }

        /// <summary>节假日日期列表（yyyy-MM-dd，逗号分隔）：命中时按周日规则处理</summary>
        public static string GetHolidayDates() { return GetStr("HolidayDates", ""); }
        public static void SetHolidayDates(string v) { SetStr("HolidayDates", v == null ? "" : v.Trim()); }

        public static string GetLanguage() { string s = GetStr("Lang", "zh"); return s == "en" ? "en" : "zh"; }
        public static void SetLanguage(string v) { SetStr("Lang", v == "en" ? "en" : "zh"); }

        /// <summary>删除该用户的全部配置（二次确认 + 家长 PIN 后调用）</summary>
        public static int DeleteUserConfig(string user)
        {
            int n = 0;
            string prefix = UKey(user, "");
            try
            {
                if (root == null) return 0;
                foreach (string name in root.GetValueNames())
                {
                    if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    try { root.DeleteValue(name, false); n++; } catch (Exception) { }
                }
            }
            catch (Exception ex) { Log("DeleteUserConfig 失败: " + ex.Message); }
            return n;
        }

        static int ClampMin(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }
}
