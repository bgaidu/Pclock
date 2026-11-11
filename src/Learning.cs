using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.IO;

namespace PCLock
{
    /// <summary>学习策略纯函数层（借鉴 StudyOnTV / PoHW），不依赖 WinForms / 注册表，可单测。</summary>

    // ===== 间隔重复 SM-2 简化版 =====
    public struct ReviewState
    {
        public double Ease;
        public int IntervalDays;
        public long DueUtcTicks;
        public int Repetitions;
    }

    public static class SpacedRepetition
    {
        public static ReviewState Initial()
        {
            ReviewState s = new ReviewState();
            s.Ease = 2.5; s.IntervalDays = 0; s.DueUtcTicks = 0; s.Repetitions = 0;
            return s;
        }

        /// <summary>答对：1 天 → 6 天 → 之后按 ease 倍率延长；答错：10 分钟后重来并降低 ease</summary>
        public static ReviewState Review(ReviewState s, bool correct, long nowUtcTicks)
        {
            if (s.Ease < 1.3) s.Ease = 2.5;
            if (correct)
            {
                s.Repetitions++;
                s.IntervalDays = s.Repetitions <= 1 ? 1 : (s.Repetitions == 2 ? 6 :
                    (int)Math.Max(6, Math.Round(s.IntervalDays * s.Ease)));
                s.DueUtcTicks = nowUtcTicks + (long)s.IntervalDays * TimeSpan.TicksPerDay;
            }
            else
            {
                s.Repetitions = 0;
                s.IntervalDays = 0;
                s.Ease = Math.Max(1.3, s.Ease - 0.2);
                s.DueUtcTicks = nowUtcTicks + 10 * TimeSpan.TicksPerMinute;
            }
            return s;
        }

        public static bool IsDue(ReviewState s, long nowUtcTicks)
        {
            return s.DueUtcTicks <= 0 || s.DueUtcTicks <= nowUtcTicks;
        }

        public static string Serialize(ReviewState s)
        {
            return s.Ease.ToString("R", CultureInfo.InvariantCulture) + "|" +
                s.IntervalDays + "|" + s.DueUtcTicks + "|" + s.Repetitions;
        }

        public static ReviewState Parse(string stored)
        {
            ReviewState s = Initial();
            if (string.IsNullOrEmpty(stored)) return s;
            string[] p = stored.Split('|');
            if (p.Length != 4) return s;
            double e; int i, r; long d;
            if (double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out e)) s.Ease = e;
            if (int.TryParse(p[1], out i)) s.IntervalDays = i;
            if (long.TryParse(p[2], out d)) s.DueUtcTicks = d;
            if (int.TryParse(p[3], out r)) s.Repetitions = r;
            return s;
        }
    }

    // ===== 按题统计 =====
    public static class QuestionStats
    {
        /// <summary>题目稳定 key：题干 + 选项 + 答案下标的 FNV-1a 哈希（纯函数、无依赖）</summary>
        public static string KeyOf(Question q)
        {
            if (q == null) return "";
            StringBuilder sb = new StringBuilder();
            sb.Append(q.Prompt).Append('#');
            if (q.Options != null) foreach (string o in q.Options) sb.Append(o).Append('#');
            sb.Append(q.CorrectIndex).Append('#').Append(q.Answer);
            uint h = 2166136261;
            foreach (char c in sb.ToString())
            {
                h ^= c;
                h *= 16777619;
            }
            return h.ToString("x8");
        }

        /// <summary>存储格式：shown|answered|correct；累计一次展示与作答</summary>
        public static string Update(string stored, bool correct)
        {
            int shown = 0, answered = 0, right = 0;
            if (!string.IsNullOrEmpty(stored))
            {
                string[] p = stored.Split('|');
                if (p.Length == 3)
                {
                    int.TryParse(p[0], out shown);
                    int.TryParse(p[1], out answered);
                    int.TryParse(p[2], out right);
                }
            }
            shown++; answered++;
            if (correct) right++;
            return shown + "|" + answered + "|" + right;
        }

        public static int Accuracy(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return -1;
            string[] p = stored.Split('|');
            int answered, right;
            if (p.Length != 3 || !int.TryParse(p[1], out answered) ||
                !int.TryParse(p[2], out right) || answered <= 0) return -1;
            return (int)(100L * right / answered);
        }

        /// <summary>返回作答次数 >= minAnswered 中正确率最低的 key（没有则返回空串）</summary>
        public static string Weakest(string[] keys, string[] values, int minAnswered)
        {
            string best = "";
            int bestAcc = 101, bestAnswered = 0;
            if (keys == null || values == null) return best;
            int n = Math.Min(keys.Length, values.Length);
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(values[i])) continue;
                string[] p = values[i].Split('|');
                int answered;
                if (p.Length != 3 || !int.TryParse(p[1], out answered) || answered < minAnswered) continue;
                int acc = Accuracy(values[i]);
                if (acc < bestAcc || (acc == bestAcc && answered > bestAnswered))
                {
                    bestAcc = acc; bestAnswered = answered; best = keys[i];
                }
            }
            return best;
        }
    }

    // ===== 难度 80/20 倾向加权（PoHW 思路）=====
    public static class DifficultyBias
    {
        /// <summary>preferred 为 0 时不做倾向；返回应选中的下标</summary>
        public static int PickIndex(int[] difficulties, int preferred, Random rnd, int preferPercent)
        {
            if (difficulties == null || difficulties.Length == 0) return -1;
            if (rnd == null) rnd = new Random();
            if (preferred < 1 || preferPercent <= 0) return rnd.Next(difficulties.Length);
            List<int> match = new List<int>();
            for (int i = 0; i < difficulties.Length; i++) if (difficulties[i] == preferred) match.Add(i);
            if (match.Count == 0 || rnd.Next(100) >= preferPercent) return rnd.Next(difficulties.Length);
            return match[rnd.Next(match.Count)];
        }
    }

    // ===== 下次可用时间 =====
    public static class NextWindow
    {
        /// <summary>从当前时刻起，找到下一个命中「仅时段内可用」规则的分钟数；无规则返回 -1</summary>
        public static int MinutesUntil(List<WindowRule> rules, DateTime now)
        {
            if (rules == null || rules.Count == 0) return -1;
            bool hasHard = false;
            foreach (WindowRule r in rules) if (!r.QuizOnly) { hasHard = true; break; }
            if (!hasHard) return -1;
            for (int i = 1; i <= 7 * 24 * 60; i++)
            {
                DateTime t = now.AddMinutes(i);
                foreach (WindowRule r in rules)
                    if (!r.QuizOnly && r.Matches(t.DayOfWeek, t.TimeOfDay)) return i;
            }
            return -1;
        }

        public static string Format(int minutes)
        {
            if (minutes < 0) return "";
            if (minutes < 60) return minutes + " 分钟";
            int h = minutes / 60, m = minutes % 60;
            if (h < 24) return h + " 小时 " + (m > 0 ? m + " 分钟" : "");
            return (h / 24) + " 天 " + (h % 24) + " 小时";
        }
    }

    /// <summary>
    /// 错题本地缓存（review.json）：题目是算法动态生成的，无法像 PoHW 那样按题库 ID 复查，
    /// 因此把答错的题整题序列化到程序目录，到期时优先混入下一批题。纯本地、不联网。
    /// </summary>
    public static class ReviewBank
    {
        /// <summary>旧版本共用的错题库文件（未按用户隔离）：首次读取时归给当前用户并备份为 .bak</summary>
        public const string LegacyFileName = "review.json";

        static string Dir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>按用户隔离的错题库文件名（review-&lt;user&gt;.json），避免 Windows 多用户共用一份</summary>
        public static string FileNameFor(string user)
        {
            return "review-" + SafeName(user) + ".json";
        }

        /// <summary>用户名安全化：只保留字母数字，其余替换为下划线（防非法路径字符 / 超长名）</summary>
        public static string SafeName(string user)
        {
            if (string.IsNullOrEmpty(user)) return "default";
            StringBuilder sb = new StringBuilder();
            foreach (char c in user)
            {
                if (sb.Length >= 40) break;
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            string s = sb.ToString().Trim('_');
            return s.Length == 0 ? "default" : s;
        }

        static string PathFor(string user)
        {
            return Path.Combine(Dir, FileNameFor(user));
        }

        public static string Export(IList<ReviewItem> items)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"items\":[");
            if (items != null)
            {
                bool first = true;
                foreach (ReviewItem it in items)
                {
                    if (it == null || it.Q == null) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    Question q = it.Q;
                    sb.Append("{\"k\":\"").Append(MiniJson.Escape(it.Key)).Append("\",")
                      .Append("\"q\":\"").Append(MiniJson.Escape(q.Prompt)).Append("\",")
                      .Append("\"ty\":").Append((int)q.Type).Append(",\"sj\":").Append((int)q.Subject)
                      .Append(",\"g\":").Append(q.Grade).Append(",\"c\":").Append(q.CorrectIndex)
                      .Append(",\"a\":").Append(q.Answer).Append(",\"o\":[");
                    if (q.Options != null)
                        for (int i = 0; i < q.Options.Length; i++)
                        {
                            if (i > 0) sb.Append(',');
                            sb.Append('"').Append(MiniJson.Escape(q.Options[i])).Append('"');
                        }
                    sb.Append("],\"due\":").Append(it.DueUtcTicks).Append('}');
                }
            }
            return sb.Append("]}").ToString();
        }

        /// <summary>读取该用户的错题缓存；旧版共用的 review.json 会一次性迁移过来（按用户分文件）</summary>
        public static List<ReviewItem> Load(string user)
        {
            string path = PathFor(user);
            try
            {
                if (!File.Exists(path)) MigrateLegacy(user, path);
                if (!File.Exists(path)) return new List<ReviewItem>();
                return ParseItems(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception) { return new List<ReviewItem>(); }
        }

        static void MigrateLegacy(string user, string path)
        {
            try
            {
                string legacy = Path.Combine(Dir, LegacyFileName);
                if (!File.Exists(legacy)) return;
                File.WriteAllText(path, File.ReadAllText(legacy, Encoding.UTF8), new UTF8Encoding(false));
                File.Move(legacy, legacy + ".bak");   // 留原始数据，不删除；此后只读写按用户的文件
                AuditLog.Write("错题库按用户迁移：" + LegacyFileName + " -> " + FileNameFor(user));
            }
            catch (Exception) { }
        }

        static List<ReviewItem> ParseItems(string json)
        {
            List<ReviewItem> r = new List<ReviewItem>();
            try
            {
                object root = MiniJson.Parse(json);
                Dictionary<string, object> doc = root as Dictionary<string, object>;
                if (doc == null) return r;
                object arr;
                if (!doc.TryGetValue("items", out arr)) return r;
                List<object> list = arr as List<object>;
                if (list == null) return r;
                foreach (object o in list)
                {
                    Dictionary<string, object> d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    ReviewItem it = new ReviewItem();
                    it.Key = Str(d, "k");
                    Question q = new Question();
                    q.Prompt = Str(d, "q");
                    q.Type = (QType)Int(d, "ty");
                    q.Subject = (Subject)Int(d, "sj");
                    q.Grade = Int(d, "g");
                    q.CorrectIndex = Int(d, "c");
                    q.Answer = Int(d, "a");
                    object opts;
                    if (d.TryGetValue("o", out opts))
                    {
                        List<object> os = opts as List<object>;
                        if (os != null && os.Count > 0)
                        {
                            q.Options = new string[os.Count];
                            for (int i = 0; i < os.Count; i++)
                                q.Options[i] = os[i] == null ? "" : Convert.ToString(os[i]);
                        }
                    }
                    it.DueUtcTicks = Long(d, "due");
                    it.Q = q;
                    r.Add(it);
                }
            }
            catch (Exception) { }
            return r;
        }

        static string Str(Dictionary<string, object> d, string k)
        {
            object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : "";
        }
        static int Int(Dictionary<string, object> d, string k)
        {
            object v;
            if (!d.TryGetValue(k, out v) || v == null) return 0;
            return v is double ? (int)(double)v : 0;
        }
        static long Long(Dictionary<string, object> d, string k)
        {
            object v;
            if (!d.TryGetValue(k, out v) || v == null) return 0;
            return v is double ? (long)(double)v : 0;
        }

        static void Save(string user, List<ReviewItem> items)
        {
            try { File.WriteAllText(PathFor(user), Export(items), new UTF8Encoding(false)); }
            catch (Exception) { }
        }

        /// <summary>答错后记录（或更新）一道错题；答对到期题后由 Remove 清掉。按用户分文件。</summary>
        public static void RecordWrong(Question q, string user, ReviewState state)
        {
            if (q == null) return;
            List<ReviewItem> items = Load(user);
            string key = QuestionStats.KeyOf(q);
            ReviewItem found = null;
            foreach (ReviewItem it in items) if (it.Key == key) { found = it; break; }
            if (found == null) { found = new ReviewItem(); found.Key = key; items.Add(found); }
            found.Q = q;
            found.DueUtcTicks = state.DueUtcTicks;
            if (items.Count > 200) items.RemoveRange(0, items.Count - 200);
            Save(user, items);
        }

        /// <summary>答对某题后从错题库移除；不存在时不写文件（避免每题一次无谓 IO）</summary>
        public static bool Remove(string user, string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            List<ReviewItem> items = Load(user);
            bool removed = false;
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].Key == key) { items.RemoveAt(i); removed = true; }
            if (removed) Save(user, items);
            return removed;
        }

        /// <summary>
        /// 把到期错题替换到题目前 N 个位置（题量不变），实现「到期优先」。
        /// 会跳过本批已有的题与重复的错题，避免同一批出现两道一样的题。
        /// </summary>
        public static int MixDue(List<Question> questions, string user, int maxCount)
        {
            if (questions == null || questions.Count == 0 || maxCount <= 0) return 0;
            string[] existing = new string[questions.Count];
            for (int i = 0; i < questions.Count; i++) existing[i] = QuestionStats.KeyOf(questions[i]);
            List<Question> pick = SelectDueToMix(Load(user), existing, DateTime.UtcNow.Ticks, maxCount);
            return ApplyMix(questions, pick);
        }

        /// <summary>纯函数：把选中的错题替换到题目前若干个位置（题量不变）</summary>
        public static int ApplyMix(List<Question> questions, List<Question> pick)
        {
            if (questions == null || pick == null || questions.Count == 0) return 0;
            int n = Math.Min(questions.Count, pick.Count);
            for (int i = 0; i < n; i++) questions[i] = pick[i];
            return n;
        }

        /// <summary>
        /// 纯函数：从错题缓存里选出可以混入本批的题（到期、未在本批、互不重复），最多 maxCount 道。
        /// 单独抽出来便于单测，不依赖文件系统。
        /// </summary>
        public static List<Question> SelectDueToMix(List<ReviewItem> items, string[] existingKeys, long nowTicks, int maxCount)
        {
            List<Question> r = new List<Question>();
            if (items == null || maxCount <= 0) return r;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (existingKeys != null)
                foreach (string k in existingKeys)
                    if (!string.IsNullOrEmpty(k)) seen.Add(k);
            foreach (ReviewItem it in items)
            {
                if (r.Count >= maxCount) break;
                if (it == null || it.Q == null) continue;
                if (it.DueUtcTicks > nowTicks) continue;                 // 未到期
                string key = string.IsNullOrEmpty(it.Key) ? QuestionStats.KeyOf(it.Q) : it.Key;
                if (!seen.Add(key)) continue;                             // 已在本批 / 已选过
                r.Add(it.Q);
            }
            return r;
        }
    }

    public class ReviewItem
    {
        public string Key;
        public Question Q;
        public long DueUtcTicks;
    }
}
