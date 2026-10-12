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
            string text; int ans;
            switch (grade)
            {
                case 1: Gen1(out text, out ans); break;
                case 2: Gen2(out text, out ans); break;
                case 3: Gen3(out text, out ans); break;
                case 4: Gen4(out text, out ans); break;
                case 5: Gen5(out text, out ans); break;
                default: Gen6(out text, out ans); break;
            }
            if (ans < 0) ans = 0;
            Question q = new Question();
            q.Subject = Subject.Math; q.Grade = grade; q.Type = QType.MathCalc;
            q.Prompt = text; q.Options = null; q.CorrectIndex = -1; q.Answer = ans; q.Explanation = "";
            return q;
        }

        // 一年级：10以内加减、20以内进位加/退位减、连加连减、加减混合
        static void Gen1(out string text, out int ans)
        {
            int k = rnd.Next(7), a, b, c, room, add;
            switch (k)
            {
                case 0: a = rnd.Next(1, 10); b = rnd.Next(1, 11 - a); ans = a + b; text = a + " + " + b + " = ?"; break;
                case 1: a = rnd.Next(2, 11); b = rnd.Next(1, a); ans = a - b; text = a + " - " + b + " = ?"; break;
                case 2:
                    a = rnd.Next(8, 10); b = rnd.Next(11 - a, 19 - a);
                    if (b > 9) b = 9; if (b < 2) b = 2;
                    ans = a + b; text = a + " + " + b + " = ?"; break;
                case 3:
                    a = rnd.Next(11, 19); b = rnd.Next((a % 10) + 1, 10);
                    if (b < 2) b = 2; if (b >= a) b = a - 2; if (b < 2) b = 2;
                    ans = a - b; text = a + " - " + b + " = ?"; break;
                case 4:
                    a = rnd.Next(1, 10); b = rnd.Next(1, 10); c = rnd.Next(1, 10);
                    if (a + b + c > 20) { c = 1; if (a + b + c > 20) { b = 1; if (a + b + c > 20) a = 1; } }
                    ans = a + b + c; text = a + " + " + b + " + " + c + " = ?"; break;
                case 5:
                    a = rnd.Next(10, 20); b = rnd.Next(1, a); c = rnd.Next(1, a - b + 1);
                    ans = a - b - c; text = a + " - " + b + " - " + c + " = ?"; break;
                default:
                    a = rnd.Next(6, 15); b = rnd.Next(1, a); room = 20 - (a - b); if (room < 1) room = 1;
                    add = rnd.Next(1, (room < 9 ? room : 9) + 1);
                    ans = a - b + add; if (ans > 20) { add = 20 - (a - b); if (add < 1) add = 1; ans = a - b + add; }
                    text = a + " - " + b + " + " + add + " = ?"; break;
            }
        }

        // 二年级：100以内进位加/退位减、表内乘除、连减
        static void Gen2(out string text, out int ans)
        {
            int k = rnd.Next(5), a, b, c, g;
            switch (k)
            {
                case 0:
                    a = rnd.Next(10, 90); b = rnd.Next(10, 90);
                    if (a + b > 100) { b = 100 - a; if (b < 10) { a = 40; b = 50; } }
                    ans = a + b; text = a + " + " + b + " = ?"; break;
                case 1:
                    a = rnd.Next(21, 100); g = 0;
                    do { b = rnd.Next(11, a); g++; } while (((a % 10) >= (b % 10) || b >= a) && g < 60);
                    if (b >= a || (a % 10) >= (b % 10)) { a = 52; b = 27; }
                    ans = a - b; text = a + " - " + b + " = ?"; break;
                case 2: a = rnd.Next(2, 10); b = rnd.Next(2, 10); ans = a * b; text = a + " × " + b + " = ?"; break;
                case 3: b = rnd.Next(2, 10); ans = rnd.Next(2, 10); a = b * ans; text = a + " ÷ " + b + " = ?"; break;
                default:
                    a = rnd.Next(60, 100); b = rnd.Next(10, 40); c = rnd.Next(1, 20);
                    if (b + c >= a) c = 1; if (b + c >= a) b = 10;
                    ans = a - b - c; text = a + " - " + b + " - " + c + " = ?"; break;
            }
        }

        // 三年级：两三位数乘/除一位数、四则混合(小括号)、同分母分数加减、一位小数加减
        static void Gen3(out string text, out int ans)
        {
            int k = rnd.Next(5), a, b, c, d, p, q, r, tot, ia, ib, f, g2, lo, hi;
            switch (k)
            {
                case 0: a = rnd.Next(10, 1000); b = rnd.Next(2, 10); ans = a * b; text = a + " × " + b + " = ?"; break;
                case 1:
                    {
                        b = rnd.Next(2, 10); lo = (100 + b - 1) / b; if (lo < 20) lo = 20; hi = 1000 / b;
                        int[] zc = new int[] { 102, 105, 108, 120, 150, 204, 306, 408, 500, 600, 700, 800, 900 };
                        ans = (rnd.Next(3) == 0) ? zc[rnd.Next(zc.Length)] : rnd.Next(lo, hi);
                        if (ans < lo || ans > hi) ans = rnd.Next(lo, hi);
                        a = b * ans; text = a + " ÷ " + b + " = ?";
                    }
                    break;
                case 2:
                    switch (rnd.Next(5))
                    {
                        case 0: a = rnd.Next(10, 60); b = rnd.Next(2, 10); c = rnd.Next(2, 10); ans = a + b * c; text = a + " + " + b + " × " + c + " = ?"; break;
                        case 1: a = rnd.Next(11, 40); b = rnd.Next(2, 10); c = rnd.Next(1, a * b); ans = a * b - c; text = a + " × " + b + " - " + c + " = ?"; break;
                        case 2: a = rnd.Next(5, 20); b = rnd.Next(5, 20); c = rnd.Next(2, 10); ans = (a + b) * c; text = "(" + a + " + " + b + ") × " + c + " = ?"; break;
                        case 3: c = rnd.Next(2, 10); d = c * rnd.Next(2, 10); b = rnd.Next(10, 50); a = b + d; ans = d / c; text = "(" + a + " - " + b + ") ÷ " + c + " = ?"; break;
                        default: a = rnd.Next(2, 10); b = rnd.Next(5, 20); c = rnd.Next(1, b); ans = a * (b - c); text = a + " × (" + b + " - " + c + ") = ?"; break;
                    }
                    break;
                case 3:
                    q = rnd.Next(2, 10); r = rnd.Next(1, 3); tot = q * r; p = rnd.Next(1, tot); c = tot - p;
                    ans = r; text = p + "/" + q + " + " + c + "/" + q + " = ?"; break;
                default:
                    if (rnd.Next(2) == 0)
                    { ia = rnd.Next(1, 10); ib = rnd.Next(1, 10); f = rnd.Next(1, 10); g2 = 10 - f; ans = ia + ib + 1; text = ia + "." + f + " + " + ib + "." + g2 + " = ?"; }
                    else { ia = rnd.Next(3, 10); ib = rnd.Next(1, ia); f = rnd.Next(1, 10); ans = ia - ib; text = ia + "." + f + " - " + ib + "." + f + " = ?"; }
                    break;
            }
        }

        // 四年级：三位数×两位数、除数是两位数、四则(中括号)、运算律简便
        static void Gen4(out string text, out int ans)
        {
            int k = rnd.Next(4), a, b, c, d, n, m;
            switch (k)
            {
                case 0: a = rnd.Next(100, 1000); b = rnd.Next(10, 100); ans = a * b; text = a + " × " + b + " = ?"; break;
                case 1: b = rnd.Next(11, 100); ans = rnd.Next(11, 100); a = b * ans; text = a + " ÷ " + b + " = ?"; break;
                case 2:
                    switch (rnd.Next(4))
                    {
                        case 0: a = rnd.Next(10, 50); b = rnd.Next(2, 10); c = rnd.Next(5, 15); d = rnd.Next(1, c); ans = a + b * (c - d); text = a + " + " + b + " × (" + c + " - " + d + ") = ?"; break;
                        case 1: a = rnd.Next(2, 10); b = rnd.Next(10, 40); c = rnd.Next(2, 10); d = rnd.Next(2, 10); ans = a * (b + c * d); text = a + " × [" + b + " + " + c + " × " + d + "] = ?"; break;
                        case 2: a = rnd.Next(10, 40); b = rnd.Next(10, 40); c = rnd.Next(10, 30); d = rnd.Next(1, c); ans = (a + b) * (c - d); text = "(" + a + " + " + b + ") × (" + c + " - " + d + ") = ?"; break;
                        default: a = rnd.Next(11, 30); b = rnd.Next(11, 30); c = rnd.Next(2, 10); d = rnd.Next(2, 10); ans = a * b - c * d; text = a + " × " + b + " - " + c + " × " + d + " = ?"; break;
                    }
                    break;
                default:
                    switch (rnd.Next(5))
                    {
                        case 0: n = rnd.Next(2, 10); ans = n * 100; text = "25 × " + n + " × 4 = ?"; break;
                        case 1: n = rnd.Next(2, 10); ans = n * 1000; text = "125 × " + n + " × 8 = ?"; break;
                        case 2: n = rnd.Next(1, 20); m = rnd.Next(2, 6); ans = (100 + n) * m; text = "(100 + " + n + ") × " + m + " = ?"; break;
                        case 3: n = rnd.Next(2, 10); ans = n * 100; text = "99 × " + n + " + " + n + " = ?"; break;
                        default: n = rnd.Next(2, 10); ans = n * 100; text = "101 × " + n + " - " + n + " = ?"; break;
                    }
                    break;
            }
        }

        // 五年级：小数乘除、分数乘除、异分母分数加减、最大公因数/最小公倍数
        static void Gen5(out string text, out int ans)
        {
            int k = rnd.Next(6), b, c, p, q, r, n, ip, f, mm, kk, cc, d1, q1, k2, b1, t, dd, aa, ccc, x, y, gg;
            switch (k)
            {
                case 0:
                    f = new int[] { 2, 4, 5, 6, 8 }[rnd.Next(5)]; ip = rnd.Next(1, 10); kk = ip * 10 + f;
                    cc = (f == 5) ? (2 * rnd.Next(1, 5)) : 5;
                    ans = kk * cc / 10; text = DecStr(kk) + " × " + cc + " = ?"; break;
                case 1:
                    d1 = new int[] { 12, 14, 15, 24, 25, 35, 45, 16, 18 }[rnd.Next(9)]; q1 = rnd.Next(2, 5); k2 = d1 * q1;
                    if (k2 > 99) { q1 = 2; k2 = d1 * 2; }
                    ans = q1; text = DecStr(k2) + " ÷ " + DecStr(d1) + " = ?"; break;
                case 2:
                    if (rnd.Next(2) == 0) { q = rnd.Next(2, 10); mm = rnd.Next(2, 10); n = q * mm; p = rnd.Next(1, 10); ans = mm * p; text = n + " × " + p + "/" + q + " = ?"; }
                    else { p = rnd.Next(2, 10); q = rnd.Next(2, 10); r = rnd.Next(1, 5); c = r * q; b = p; ans = r; text = p + "/" + q + " × " + c + "/" + b + " = ?"; }
                    break;
                case 3:
                    if (rnd.Next(2) == 0) { p = rnd.Next(1, 4); q = rnd.Next(2, 10); n = rnd.Next(2, 12); if (p != 1 && (n * q) % p != 0) n = p; ans = n * q / p; text = n + " ÷ " + p + "/" + q + " = ?"; }
                    else { p = rnd.Next(2, 10); q = rnd.Next(2, 10); r = rnd.Next(1, 5); c = p; b = r * q; ans = r; text = p + "/" + q + " ÷ " + c + "/" + b + " = ?"; }
                    break;
                case 4:
                    b1 = rnd.Next(2, 7); t = rnd.Next(2, 4); dd = b1 * t; aa = rnd.Next(1, b1); ccc = dd - aa * (dd / b1);
                    ans = 1; text = aa + "/" + b1 + " + " + ccc + "/" + dd + " = ?"; break;
                default:
                    x = rnd.Next(4, 25); y = rnd.Next(4, 25); gg = GCD(x, y);
                    if (rnd.Next(2) == 0) { ans = gg; text = x + " 和 " + y + " 的最大公因数 = ?"; }
                    else { ans = x * y / gg; text = x + " 和 " + y + " 的最小公倍数 = ?"; }
                    break;
            }
        }

        // 六年级：分数/小数除法、百分数及互化、混合运算、体积表面积、圆、行程、公倍数
        static void Gen6(out string text, out int ans)
        {
            int k = rnd.Next(8), a, b, c, d, p, q, r, p2, q2, dd2, d1, q1, k2, bb, cc2, bc, kk2, x, y, gg, X, Y, rr, v, tt, g;
            switch (k)
            {
                case 0: p = rnd.Next(2, 10); q = rnd.Next(2, 10); r = rnd.Next(1, 5); p2 = p; q2 = r * q; ans = r; text = p + "/" + q + " ÷ " + p2 + "/" + q2 + " = ?"; break;
                case 1: d1 = new int[] { 12, 14, 15, 24, 25, 35, 45, 16, 18 }[rnd.Next(9)]; q1 = rnd.Next(2, 5); k2 = d1 * q1; if (k2 > 99) { q1 = 2; k2 = d1 * 2; } ans = q1; text = DecStr(k2) + " ÷ " + DecStr(d1) + " = ?"; break;
                case 2: X = new int[] { 5, 10, 20, 25, 50, 60, 75, 80 }[rnd.Next(8)]; Y = rnd.Next(4, 80); g = 0; while ((X * Y) % 100 != 0 && g < 40) { Y = rnd.Next(4, 80); g++; } if ((X * Y) % 100 != 0) { X = 25; Y = 4 * rnd.Next(1, 20); } ans = X * Y / 100; text = X + "% × " + Y + " = ?"; break;
                case 3:
                    if (rnd.Next(2) == 0) { dd2 = rnd.Next(10, 100); ans = dd2; text = "0." + dd2 + " 化成百分数 = ?%"; }
                    else { q = new int[] { 4, 5, 10, 20, 25 }[rnd.Next(5)]; p = rnd.Next(1, q); g = 0; while ((p * 100) % q != 0 && g < 20) { p = rnd.Next(1, q); g++; } if ((p * 100) % q != 0) { q = 4; p = 1; } ans = p * 100 / q; text = p + "/" + q + " 化成百分数 = ?%"; }
                    break;
                case 4:
                    switch (rnd.Next(3))
                    {
                        case 0: a = rnd.Next(11, 30); b = rnd.Next(11, 30); c = rnd.Next(2, 10); d = rnd.Next(2, 10); ans = a * b + c * d; text = a + " × " + b + " + " + c + " × " + d + " = ?"; break;
                        case 1: a = 2 * rnd.Next(1, 5); b = rnd.Next(1, 20); ans = 25 * a / 10 + b; text = "2.5 × " + a + " + " + b + " = ?"; break;
                        default: d = rnd.Next(2, 10); bb = rnd.Next(2, 10); cc2 = rnd.Next(2, 10); bc = bb * cc2; kk2 = rnd.Next(2, 10); a = d * kk2 - bc; g = 0; while (a < 0 && g < 30) { d = rnd.Next(2, 10); bb = rnd.Next(2, 10); cc2 = rnd.Next(2, 10); bc = bb * cc2; kk2 = rnd.Next(2, 10); a = d * kk2 - bc; g++; } if (a < 0) a = 0; ans = kk2; text = "(" + a + " + " + bb + " × " + cc2 + ") ÷ " + d + " = ?"; break;
                    }
                    break;
                case 5:
                    switch (rnd.Next(4))
                    {
                        case 0: a = rnd.Next(2, 10); b = rnd.Next(2, 10); c = rnd.Next(2, 10); ans = a * b * c; text = "长方体长" + a + "宽" + b + "高" + c + "，体积=？"; break;
                        case 1: a = rnd.Next(2, 8); ans = a * a * a; text = "正方体棱长" + a + "，体积=？"; break;
                        case 2: a = rnd.Next(2, 10); b = rnd.Next(2, 10); c = rnd.Next(2, 10); ans = 2 * (a * b + b * c + a * c); text = "长方体长" + a + "宽" + b + "高" + c + "，表面积=？"; break;
                        default: a = rnd.Next(2, 7); ans = 6 * a * a; text = "正方体棱长" + a + "，表面积=？"; break;
                    }
                    break;
                case 6:
                    rr = new int[] { 10, 20, 50 }[rnd.Next(3)];
                    if (rnd.Next(2) == 0) { ans = 314 * rr * rr / 100; text = "半径" + rr + "的圆面积=？(π取3.14)"; }
                    else { rr = (rr == 10) ? 50 : 100; ans = 628 * rr / 100; text = "半径" + rr + "的圆周长=？(π取3.14)"; }
                    break;
                default:
                    if (rnd.Next(2) == 0) { v = 10 * rnd.Next(4, 10); tt = rnd.Next(2, 6); ans = v * tt; text = "汽车每小时行" + v + "千米，" + tt + "小时共行多少千米？"; }
                    else { x = rnd.Next(4, 25); y = rnd.Next(4, 25); gg = GCD(x, y); if (rnd.Next(2) == 0) { ans = gg; text = x + " 和 " + y + " 的最大公因数 = ?"; } else { ans = x * y / gg; text = x + " 和 " + y + " 的最小公倍数 = ?"; } }
                    break;
            }
        }

        // k 为"十分位整数"（如 15 表示 1.5），十分位为 0 时显示整数
        static string DecStr(int k)
        {
            int ip = k / 10, fr = k % 10;
            return fr == 0 ? ip.ToString() : ip + "." + fr;
        }

        static int GCD(int x, int y)
        {
            while (y != 0) { int t = x % y; x = y; y = t; }
            return x;
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
