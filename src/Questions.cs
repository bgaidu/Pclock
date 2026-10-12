using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace PCLock
{
    /// <summary>学科：混合随机 / 单科 / 两科组合</summary>
    public enum Subject { Mixed = 0, Chinese = 1, Math = 2, English = 3, ChineseMath = 4, ChineseEnglish = 5, MathEnglish = 6 }

    /// <summary>题型</summary>
    public enum QType { MathCalc, ChinesePinyin, EnglishWord }

    /// <summary>一道题。口算题用 Answer（数值）；语/英选择题用 Options + CorrectIndex。</summary>
    public class Question
    {
        public Subject Subject;
        public int Grade;
        public QType Type;
        public string Prompt;       // 题干（词语 / 算式 / 英文单词 / 中文词）
        public string[] Options;    // 4 个选项（口算题为 null）
        public int CorrectIndex;    // 正确选项下标（口算题为 -1）
        public int Answer;          // 口算题数值答案（其他为 -1）
        public string Explanation;  // 语文词义解释（其他为空）
    }

    /// <summary>语文词条</summary>
    public class WordEntry
    {
        public int Grade;
        public string Word;
        public string Pinyin;
        public string Meaning;
    }

    /// <summary>英语词条</summary>
    public class EnglishEntry
    {
        public int Grade;
        public string En;
        public string Zh;
    }

    /// <summary>题库：按年级/学科出一批互不重复的题目。</summary>
    public static class QuestionBank
    {
        static Random rnd = new Random();
        static List<WordEntry> chinese;
        static List<EnglishEntry> english;

        static QuestionBank()
        {
            chinese = LoadWordEntries();
            english = LoadEnglishEntries();
        }

        // 外部题库优先：程序目录下 chinese.txt / english.txt 若存在且能解析则使用，
        // 否则回退到程序内置的 ChineseData.All / EnglishData.All（兜底，保证永不空题库）。
        static List<WordEntry> LoadWordEntries()
        {
            List<WordEntry> ext = ParseChineseFile(Path.Combine(AppDir(), "chinese.txt"));
            if (ext != null && ext.Count > 0) return ext;
            return new List<WordEntry>(ChineseData.All);
        }
        static List<EnglishEntry> LoadEnglishEntries()
        {
            List<EnglishEntry> ext = ParseEnglishFile(Path.Combine(AppDir(), "english.txt"));
            if (ext != null && ext.Count > 0) return ext;
            return new List<EnglishEntry>(EnglishData.All);
        }

        static string AppDir()
        {
            try { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
            catch { return "."; }
        }

        static List<WordEntry> ParseChineseFile(string path)
        {
            if (!File.Exists(path)) return null;
            List<WordEntry> list = new List<WordEntry>();
            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line == "" || line.StartsWith("#")) continue;
                    string[] p = line.Split('|');
                    if (p.Length < 4) continue;
                    int g;
                    if (!int.TryParse(p[0], out g)) continue;
                    list.Add(new WordEntry { Grade = g, Word = p[1].Trim(), Pinyin = p[2].Trim(), Meaning = p[3].Trim() });
                }
            }
            catch { return null; }
            return list;
        }

        static List<EnglishEntry> ParseEnglishFile(string path)
        {
            if (!File.Exists(path)) return null;
            List<EnglishEntry> list = new List<EnglishEntry>();
            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line == "" || line.StartsWith("#")) continue;
                    string[] p = line.Split('|');
                    if (p.Length < 3) continue;
                    int g;
                    if (!int.TryParse(p[0], out g)) continue;
                    list.Add(new EnglishEntry { Grade = g, En = p[1].Trim(), Zh = p[2].Trim() });
                }
            }
            catch { return null; }
            return list;
        }

        /// <summary>出 count 道题。</summary>
        public static List<Question> GetBatch(int grade, Subject subject, int count)
        {
            switch (subject)
            {
                case Subject.Math: return PickMath(grade, count);
                case Subject.Chinese: return PickChinese(grade, count);
                case Subject.English: return PickEnglish(grade, count);
                case Subject.ChineseMath: return PickFromSubjects(grade, count, 3, 0, Subject.Chinese, Subject.Math);
                case Subject.ChineseEnglish: return PickFromSubjects(grade, count, 3, 0, Subject.Chinese, Subject.English);
                case Subject.MathEnglish: return PickFromSubjects(grade, count, 3, 0, Subject.Math, Subject.English);
                default: return PickMixed(grade, count); // Mixed
            }
        }

        // ===== 数学：按年级难度算法生成（结果恒为整数）=====
        public static Question MakeMath(int grade)
        {
            int a, b, ans = 0;
            string text = "";
            int kind = rnd.Next(grade >= 3 ? 4 : 3);

            if (grade <= 1)
            {
                if (kind == 0) { a = rnd.Next(1, 20); b = rnd.Next(1, 20); ans = a + b; text = a + " + " + b + " = ?"; }
                else { a = rnd.Next(2, 20); b = rnd.Next(1, a); ans = a - b; text = a + " - " + b + " = ?"; }
            }
            else if (grade == 2)
            {
                if (kind == 0) { a = rnd.Next(10, 100); b = rnd.Next(10, 100); ans = a + b; text = a + " + " + b + " = ?"; }
                else if (kind == 1) { a = rnd.Next(20, 100); b = rnd.Next(1, a); ans = a - b; text = a + " - " + b + " = ?"; }
                else { a = rnd.Next(2, 10); b = rnd.Next(2, 10); ans = a * b; text = a + " × " + b + " = ?"; }
            }
            else if (grade == 3)
            {
                if (kind == 0) { a = rnd.Next(300, 1000); b = rnd.Next(10, a); ans = a - b; text = a + " - " + b + " = ?"; }
                else if (kind == 1) { a = rnd.Next(56, 500); b = rnd.Next(56, 500); ans = a + b; text = a + " + " + b + " = ?"; }
                else if (kind == 2) { a = rnd.Next(13, 40); b = rnd.Next(3, 10); ans = a * b; text = a + " × " + b + " = ?"; }
                else { b = rnd.Next(3, 10); ans = rnd.Next(12, 41); a = b * ans; text = a + " ÷ " + b + " = ?"; }
            }
            else if (grade == 4)
            {
                if (kind == 0) { a = rnd.Next(100, 999); b = rnd.Next(10, 99); ans = a * b; text = a + " × " + b + " = ?"; }
                else if (kind == 1) { b = rnd.Next(2, 10); ans = rnd.Next(10, 100); a = b * ans; text = a + " ÷ " + b + " = ?"; }
                else if (kind == 2) { b = rnd.Next(11, 99); ans = rnd.Next(2, 10); a = b * ans; text = a + " ÷ " + b + " = ?"; }
                else { a = rnd.Next(500, 9999); b = rnd.Next(100, 999); ans = a + b; text = a + " + " + b + " = ?"; }
            }
            else if (grade == 5)
            {
                if (kind == 0) { int t = 5 * rnd.Next(1, 9); int n = rnd.Next(1, 10) * 2; ans = (t * n) / 10; text = DecText(t) + " × " + n + " = ?"; }
                else if (kind == 1) { a = rnd.Next(100, 999); b = rnd.Next(10, 99); ans = a * b; text = a + " × " + b + " = ?"; }
                else if (kind == 2) { b = rnd.Next(11, 99); ans = rnd.Next(2, 20); a = b * ans; text = a + " ÷ " + b + " = ?"; }
                else { a = rnd.Next(1000, 9999); b = rnd.Next(100, 999); ans = a + b; text = a + " + " + b + " = ?"; }
            }
            else // grade 6
            {
                if (kind == 0) // 百分数（结果为整数）
                {
                    int X = (rnd.Next(2) == 0) ? 50 : 25;
                    int Y = (X == 50) ? rnd.Next(2, 50) * 2 : rnd.Next(1, 25) * 4;
                    ans = Y * X / 100; text = X + "% × " + Y + " = ?";
                }
                else if (kind == 1) { int t = 5 * rnd.Next(1, 9); int n = rnd.Next(1, 10) * 2; ans = (t * n) / 10; text = DecText(t) + " × " + n + " = ?"; }
                else if (kind == 2) { a = rnd.Next(100, 999); b = rnd.Next(100, 999); ans = a * b; text = a + " × " + b + " = ?"; }
                else if (kind == 3) { a = rnd.Next(11, 32); ans = a * a; text = a + "² = ?"; }
                else { b = rnd.Next(100, 999); ans = rnd.Next(2, 20); a = b * ans; text = a + " ÷ " + b + " = ?"; }
            }

            Question q = new Question();
            q.Subject = Subject.Math; q.Grade = grade; q.Type = QType.MathCalc;
            q.Prompt = text; q.Options = null; q.CorrectIndex = -1; q.Answer = ans; q.Explanation = "";
            return q;
        }

        // t 为"十分位整数"（如 15 表示 1.5），仅生成 .5 / 整数
        static string DecText(int t)
        {
            int ip = t / 10; int fr = t % 10;
            if (fr == 0) return ip.ToString();
            return ip.ToString() + ".5";
        }

        static List<Question> PickMath(int grade, int count)
        {
            List<Question> list = new List<Question>();
            for (int i = 0; i < count; i++) list.Add(MakeMath(grade));
            return list;
        }

        static List<Question> PickChinese(int grade, int count)
        {
            List<WordEntry> pool = FilterGrade(chinese, grade);
            Shuffle(pool);
            List<Question> list = new List<Question>();
            for (int i = 0; i < count; i++)
            {
                WordEntry e = (i < pool.Count) ? pool[i] : pool[rnd.Next(pool.Count)];
                list.Add(BuildChinese(e));
            }
            return list;
        }

        static List<Question> PickEnglish(int grade, int count)
        {
            List<EnglishEntry> pool = FilterGrade(english, grade);
            Shuffle(pool);
            List<Question> list = new List<Question>();
            for (int i = 0; i < count; i++)
            {
                EnglishEntry e = (i < pool.Count) ? pool[i] : pool[rnd.Next(pool.Count)];
                list.Add(BuildEnglish(e));
            }
            return list;
        }

        static List<Question> PickMixed(int grade, int count)
        {
            // 混合随机：语/数/英三科，每科最多出现 4 次
            return PickFromSubjects(grade, count, 0, 4, Subject.Chinese, Subject.Math, Subject.English);
        }

        // 从给定学科出题：每科至少 minPer、最多 maxPer（0=不限），共 total 题；保证不重复。
        // 两科模式：minPer=3 → 每科不小于 3 题；三科混合：maxPer=4 → 每科不大于 4 题。
        static List<Question> PickFromSubjects(int grade, int total, int minPer, int maxPer, params Subject[] subs)
        {
            List<List<Question>> pools = new List<List<Question>>();
            foreach (var s in subs)
            {
                if (s == Subject.Math)
                    pools.Add(MakeMathBatch(grade, total));
                else if (s == Subject.Chinese)
                {
                    List<WordEntry> w = FilterGrade(chinese, grade);
                    Shuffle(w);
                    List<Question> q = new List<Question>();
                    for (int i = 0; i < w.Count && i < total; i++) q.Add(BuildChinese(w[i]));
                    pools.Add(q);
                }
                else
                {
                    List<EnglishEntry> w = FilterGrade(english, grade);
                    Shuffle(w);
                    List<Question> q = new List<Question>();
                    for (int i = 0; i < w.Count && i < total; i++) q.Add(BuildEnglish(w[i]));
                    pools.Add(q);
                }
            }
            List<Question> list = new List<Question>();
            int[] used = new int[subs.Length];
            for (int i = 0; i < subs.Length; i++)
            {
                int n = Math.Min(minPer, pools[i].Count);
                for (int j = 0; j < n; j++) list.Add(pools[i][j]);
                used[i] = n;
            }
            int remain = total - list.Count;
            int guard = 0;
            while (remain > 0 && guard < total * 30)
            {
                guard++;
                List<int> cand = new List<int>();
                for (int i = 0; i < subs.Length; i++)
                {
                    if (used[i] >= pools[i].Count) continue;
                    if (maxPer > 0 && used[i] >= maxPer) continue;
                    cand.Add(i);
                }
                if (cand.Count == 0) break;
                int p = cand[rnd.Next(cand.Count)];
                list.Add(pools[p][used[p]]);
                used[p]++;
                remain--;
            }
            Shuffle(list);
            return list;
        }

        // 生成 n 道互不相同（按题干）的数学题
        static List<Question> MakeMathBatch(int grade, int n)
        {
            List<Question> list = new List<Question>();
            HashSet<string> seen = new HashSet<string>();
            int guard = 0;
            while (list.Count < n && guard < n * 50 + 50)
            {
                guard++;
                Question q = MakeMath(grade);
                if (seen.Add(q.Prompt)) list.Add(q);
            }
            return list;
        }

        static Question BuildChinese(WordEntry e)
        {
            Question q = new Question();
            q.Subject = Subject.Chinese; q.Grade = e.Grade; q.Type = QType.ChinesePinyin;
            q.Prompt = e.Word; q.Explanation = e.Meaning; q.Answer = -1;
            List<string> opts = new List<string>(); opts.Add(e.Pinyin);
            List<WordEntry> pool = FilterGrade(chinese, e.Grade);
            Shuffle(pool);
            foreach (var p in pool)
            {
                if (p == e) continue;
                if (opts.Count >= 4) break;
                if (p.Pinyin != e.Pinyin && !opts.Contains(p.Pinyin)) opts.Add(p.Pinyin);
            }
            while (opts.Count < 4) opts.Add(e.Pinyin + "?");
            Shuffle(opts);
            q.Options = opts.ToArray();
            q.CorrectIndex = IndexOfStr(q.Options, e.Pinyin);
            return q;
        }

        static Question BuildEnglish(EnglishEntry e)
        {
            Question q = new Question();
            q.Subject = Subject.English; q.Grade = e.Grade; q.Type = QType.EnglishWord;
            q.Answer = -1; q.Explanation = "";
            bool wordToZh = rnd.Next(2) == 0;
            List<string> opts = new List<string>();
            if (wordToZh)
            {
                q.Prompt = e.En; opts.Add(e.Zh);
                AddEnglishDistractors(opts, english, e.Grade, e, true);
                while (opts.Count < 4) opts.Add(e.Zh + "?");
                Shuffle(opts); q.Options = opts.ToArray(); q.CorrectIndex = IndexOfStr(q.Options, e.Zh);
            }
            else
            {
                q.Prompt = e.Zh; opts.Add(e.En);
                AddEnglishDistractors(opts, english, e.Grade, e, false);
                while (opts.Count < 4) opts.Add(e.En + "?");
                Shuffle(opts); q.Options = opts.ToArray(); q.CorrectIndex = IndexOfStr(q.Options, e.En);
            }
            return q;
        }

        static void AddEnglishDistractors(List<string> opts, List<EnglishEntry> pool, int grade, EnglishEntry self, bool useZh)
        {
            List<EnglishEntry> g = FilterGrade(pool, grade);
            Shuffle(g);
            foreach (var p in g)
            {
                if (p == self) continue;
                if (opts.Count >= 4) break;
                string v = useZh ? p.Zh : p.En;
                string selfv = useZh ? self.Zh : self.En;
                if (v != selfv && !opts.Contains(v)) opts.Add(v);
            }
        }

        // ===== 工具 =====
        static List<WordEntry> FilterGrade(List<WordEntry> src, int g)
        {
            List<WordEntry> r = new List<WordEntry>();
            foreach (var x in src) if (x.Grade == g) r.Add(x);
            return r;
        }
        static List<EnglishEntry> FilterGrade(List<EnglishEntry> src, int g)
        {
            List<EnglishEntry> r = new List<EnglishEntry>();
            foreach (var x in src) if (x.Grade == g) r.Add(x);
            return r;
        }
        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
        static int IndexOfStr(string[] arr, string s)
        {
            for (int i = 0; i < arr.Length; i++) if (arr[i] == s) return i;
            return -1;
        }
    }
}
