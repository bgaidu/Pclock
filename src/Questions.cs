using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace PCLock
{
    /// <summary>学科：混合随机 / 单科 / 两科组合</summary>
    public enum Subject { Mixed = 0, Chinese = 1, Math = 2, English = 3, ChineseMath = 4, ChineseEnglish = 5, MathEnglish = 6 }

    /// <summary>题型：数学填空 / 语文选拼音 / 英语选词 + 本批新增的三种</summary>
    public enum QType { MathCalc, ChinesePinyin, EnglishWord, MathChoice, ChineseWord, TrueFalse }

    /// <summary>一道题。口算题用 Answer（数值）；选择题用 Options + CorrectIndex。</summary>
    public class Question
    {
        public Subject Subject;
        public int Grade;
        public QType Type;
        public string Prompt;
        public string[] Options;
        public int CorrectIndex;
        public int Answer;
        public string Explanation;
        public int Term;        // 0=全学年 1=上册 2=下册（外部词库可选列）
        public string Bank;     // 题库名（外部词库可选列，空=默认）
    }

    /// <summary>语文词条</summary>
    public class WordEntry
    {
        public int Grade;
        public string Word;
        public string Pinyin;
        public string Meaning;
        public int Term;
        public string Bank;
    }

    /// <summary>英语词条</summary>
    public class EnglishEntry
    {
        public int Grade;
        public string En;
        public string Zh;
        public int Term;
        public string Bank;
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

        // 外部题库优先：chinese.txt / english.txt 与 banks\*.txt（每文件一个题库）合并后若可用则使用，
        // 否则回退到程序内置的 ChineseData.All / EnglishData.All（兜底，保证永不空题库）。
        static List<WordEntry> LoadWordEntries()
        {
            List<WordEntry> ext = BankSet.LoadChinese(AppDir());
            if (ext != null && ext.Count > 0) return ext;
            return new List<WordEntry>(ChineseData.All);
        }
        static List<EnglishEntry> LoadEnglishEntries()
        {
            List<EnglishEntry> ext = BankSet.LoadEnglish(AppDir());
            if (ext != null && ext.Count > 0) return ext;
            return new List<EnglishEntry>(EnglishData.All);
        }

        static string AppDir()
        {
            try { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
            catch { return "."; }
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
            int k = NextRnd(7), a, b, c, room, add;
            switch (k)
            {
                case 0: a = NextRnd(1, 10); b = NextRnd(1, 11 - a); ans = a + b; text = a + " + " + b + " = ?"; break;
                case 1: a = NextRnd(2, 11); b = NextRnd(1, a); ans = a - b; text = a + " - " + b + " = ?"; break;
                case 2:
                    a = NextRnd(8, 10); b = NextRnd(11 - a, 19 - a);
                    if (b > 9) b = 9; if (b < 2) b = 2;
                    ans = a + b; text = a + " + " + b + " = ?"; break;
                case 3:
                    a = NextRnd(11, 19); b = NextRnd((a % 10) + 1, 10);
                    if (b < 2) b = 2; if (b >= a) b = a - 2; if (b < 2) b = 2;
                    ans = a - b; text = a + " - " + b + " = ?"; break;
                case 4:
                    a = NextRnd(1, 10); b = NextRnd(1, 10); c = NextRnd(1, 10);
                    if (a + b + c > 20) { c = 1; if (a + b + c > 20) { b = 1; if (a + b + c > 20) a = 1; } }
                    ans = a + b + c; text = a + " + " + b + " + " + c + " = ?"; break;
                case 5:
                    a = NextRnd(10, 20); b = NextRnd(1, a); c = NextRnd(1, a - b + 1);
                    ans = a - b - c; text = a + " - " + b + " - " + c + " = ?"; break;
                default:
                    a = NextRnd(6, 15); b = NextRnd(1, a); room = 20 - (a - b); if (room < 1) room = 1;
                    add = NextRnd(1, (room < 9 ? room : 9) + 1);
                    ans = a - b + add; if (ans > 20) { add = 20 - (a - b); if (add < 1) add = 1; ans = a - b + add; }
                    text = a + " - " + b + " + " + add + " = ?"; break;
            }
        }

        // 二年级：100以内进位加/退位减、表内乘除、连减
        static void Gen2(out string text, out int ans)
        {
            int k = NextRnd(5), a, b, c, g;
            switch (k)
            {
                case 0:
                    a = NextRnd(10, 90); b = NextRnd(10, 90);
                    if (a + b > 100) { b = 100 - a; if (b < 10) { a = 40; b = 50; } }
                    ans = a + b; text = a + " + " + b + " = ?"; break;
                case 1:
                    a = NextRnd(21, 100); g = 0;
                    do { b = NextRnd(11, a); g++; } while (((a % 10) >= (b % 10) || b >= a) && g < 60);
                    if (b >= a || (a % 10) >= (b % 10)) { a = 52; b = 27; }
                    ans = a - b; text = a + " - " + b + " = ?"; break;
                case 2: a = NextRnd(2, 10); b = NextRnd(2, 10); ans = a * b; text = a + " × " + b + " = ?"; break;
                case 3: b = NextRnd(2, 10); ans = NextRnd(2, 10); a = b * ans; text = a + " ÷ " + b + " = ?"; break;
                default:
                    a = NextRnd(60, 100); b = NextRnd(10, 40); c = NextRnd(1, 20);
                    if (b + c >= a) c = 1; if (b + c >= a) b = 10;
                    ans = a - b - c; text = a + " - " + b + " - " + c + " = ?"; break;
            }
        }

        // 三年级：两三位数乘/除一位数、四则混合(小括号)、同分母分数加减、一位小数加减
        static void Gen3(out string text, out int ans)
        {
            int k = NextRnd(5), a, b, c, d, p, q, r, tot, ia, ib, f, g2, lo, hi;
            switch (k)
            {
                case 0: a = NextRnd(10, 1000); b = NextRnd(2, 10); ans = a * b; text = a + " × " + b + " = ?"; break;
                case 1:
                    {
                        b = NextRnd(2, 10); lo = (100 + b - 1) / b; if (lo < 20) lo = 20; hi = 1000 / b;
                        int[] zc = new int[] { 102, 105, 108, 120, 150, 204, 306, 408, 500, 600, 700, 800, 900 };
                        ans = (NextRnd(3) == 0) ? zc[NextRnd(zc.Length)] : NextRnd(lo, hi);
                        if (ans < lo || ans > hi) ans = NextRnd(lo, hi);
                        a = b * ans; text = a + " ÷ " + b + " = ?";
                    }
                    break;
                case 2:
                    switch (NextRnd(5))
                    {
                        case 0: a = NextRnd(10, 60); b = NextRnd(2, 10); c = NextRnd(2, 10); ans = a + b * c; text = a + " + " + b + " × " + c + " = ?"; break;
                        case 1: a = NextRnd(11, 40); b = NextRnd(2, 10); c = NextRnd(1, a * b); ans = a * b - c; text = a + " × " + b + " - " + c + " = ?"; break;
                        case 2: a = NextRnd(5, 20); b = NextRnd(5, 20); c = NextRnd(2, 10); ans = (a + b) * c; text = "(" + a + " + " + b + ") × " + c + " = ?"; break;
                        case 3: c = NextRnd(2, 10); d = c * NextRnd(2, 10); b = NextRnd(10, 50); a = b + d; ans = d / c; text = "(" + a + " - " + b + ") ÷ " + c + " = ?"; break;
                        default: a = NextRnd(2, 10); b = NextRnd(5, 20); c = NextRnd(1, b); ans = a * (b - c); text = a + " × (" + b + " - " + c + ") = ?"; break;
                    }
                    break;
                case 3:
                    q = NextRnd(2, 10); r = NextRnd(1, 3); tot = q * r; p = NextRnd(1, tot); c = tot - p;
                    ans = r; text = p + "/" + q + " + " + c + "/" + q + " = ?"; break;
                default:
                    if (NextRnd(2) == 0)
                    { ia = NextRnd(1, 10); ib = NextRnd(1, 10); f = NextRnd(1, 10); g2 = 10 - f; ans = ia + ib + 1; text = ia + "." + f + " + " + ib + "." + g2 + " = ?"; }
                    else { ia = NextRnd(3, 10); ib = NextRnd(1, ia); f = NextRnd(1, 10); ans = ia - ib; text = ia + "." + f + " - " + ib + "." + f + " = ?"; }
                    break;
            }
        }

        // 四年级：三位数×两位数、除数是两位数、四则(中括号)、运算律简便
        static void Gen4(out string text, out int ans)
        {
            int k = NextRnd(4), a, b, c, d, n, m;
            switch (k)
            {
                case 0: a = NextRnd(100, 1000); b = NextRnd(10, 100); ans = a * b; text = a + " × " + b + " = ?"; break;
                case 1: b = NextRnd(11, 100); ans = NextRnd(11, 100); a = b * ans; text = a + " ÷ " + b + " = ?"; break;
                case 2:
                    switch (NextRnd(4))
                    {
                        case 0: a = NextRnd(10, 50); b = NextRnd(2, 10); c = NextRnd(5, 15); d = NextRnd(1, c); ans = a + b * (c - d); text = a + " + " + b + " × (" + c + " - " + d + ") = ?"; break;
                        case 1: a = NextRnd(2, 10); b = NextRnd(10, 40); c = NextRnd(2, 10); d = NextRnd(2, 10); ans = a * (b + c * d); text = a + " × [" + b + " + " + c + " × " + d + "] = ?"; break;
                        case 2: a = NextRnd(10, 40); b = NextRnd(10, 40); c = NextRnd(10, 30); d = NextRnd(1, c); ans = (a + b) * (c - d); text = "(" + a + " + " + b + ") × (" + c + " - " + d + ") = ?"; break;
                        default: a = NextRnd(11, 30); b = NextRnd(11, 30); c = NextRnd(2, 10); d = NextRnd(2, 10); ans = a * b - c * d; text = a + " × " + b + " - " + c + " × " + d + " = ?"; break;
                    }
                    break;
                default:
                    switch (NextRnd(5))
                    {
                        case 0: n = NextRnd(2, 10); ans = n * 100; text = "25 × " + n + " × 4 = ?"; break;
                        case 1: n = NextRnd(2, 10); ans = n * 1000; text = "125 × " + n + " × 8 = ?"; break;
                        case 2: n = NextRnd(1, 20); m = NextRnd(2, 6); ans = (100 + n) * m; text = "(100 + " + n + ") × " + m + " = ?"; break;
                        case 3: n = NextRnd(2, 10); ans = n * 100; text = "99 × " + n + " + " + n + " = ?"; break;
                        default: n = NextRnd(2, 10); ans = n * 100; text = "101 × " + n + " - " + n + " = ?"; break;
                    }
                    break;
            }
        }

        // 五年级：小数乘除、分数乘除、异分母分数加减、最大公因数/最小公倍数
        static void Gen5(out string text, out int ans)
        {
            int k = NextRnd(6), b, c, p, q, r, n, ip, f, mm, kk, cc, d1, q1, k2, b1, t, dd, aa, ccc, x, y, gg;
            switch (k)
            {
                case 0:
                    f = new int[] { 2, 4, 5, 6, 8 }[NextRnd(5)]; ip = NextRnd(1, 10); kk = ip * 10 + f;
                    cc = (f == 5) ? (2 * NextRnd(1, 5)) : 5;
                    ans = kk * cc / 10; text = DecStr(kk) + " × " + cc + " = ?"; break;
                case 1:
                    d1 = new int[] { 12, 14, 15, 24, 25, 35, 45, 16, 18 }[NextRnd(9)]; q1 = NextRnd(2, 5); k2 = d1 * q1;
                    if (k2 > 99) { q1 = 2; k2 = d1 * 2; }
                    ans = q1; text = DecStr(k2) + " ÷ " + DecStr(d1) + " = ?"; break;
                case 2:
                    if (NextRnd(2) == 0) { q = NextRnd(2, 10); mm = NextRnd(2, 10); n = q * mm; p = NextRnd(1, 10); ans = mm * p; text = n + " × " + p + "/" + q + " = ?"; }
                    else { p = NextRnd(2, 10); q = NextRnd(2, 10); r = NextRnd(1, 5); c = r * q; b = p; ans = r; text = p + "/" + q + " × " + c + "/" + b + " = ?"; }
                    break;
                case 3:
                    if (NextRnd(2) == 0) { p = NextRnd(1, 4); q = NextRnd(2, 10); n = NextRnd(2, 12); if (p != 1 && (n * q) % p != 0) n = p; ans = n * q / p; text = n + " ÷ " + p + "/" + q + " = ?"; }
                    else { p = NextRnd(2, 10); q = NextRnd(2, 10); r = NextRnd(1, 5); c = p; b = r * q; ans = r; text = p + "/" + q + " ÷ " + c + "/" + b + " = ?"; }
                    break;
                case 4:
                    b1 = NextRnd(2, 7); t = NextRnd(2, 4); dd = b1 * t; aa = NextRnd(1, b1); ccc = dd - aa * (dd / b1);
                    ans = 1; text = aa + "/" + b1 + " + " + ccc + "/" + dd + " = ?"; break;
                default:
                    x = NextRnd(4, 25); y = NextRnd(4, 25); gg = GCD(x, y);
                    if (NextRnd(2) == 0) { ans = gg; text = x + " 和 " + y + " 的最大公因数 = ?"; }
                    else { ans = x * y / gg; text = x + " 和 " + y + " 的最小公倍数 = ?"; }
                    break;
            }
        }

        // 六年级：分数/小数除法、百分数及互化、混合运算、体积表面积、圆、行程、公倍数
        static void Gen6(out string text, out int ans)
        {
            int k = NextRnd(8), a, b, c, d, p, q, r, p2, q2, dd2, d1, q1, k2, bb, cc2, bc, kk2, x, y, gg, X, Y, rr, v, tt, g;
            switch (k)
            {
                case 0: p = NextRnd(2, 10); q = NextRnd(2, 10); r = NextRnd(1, 5); p2 = p; q2 = r * q; ans = r; text = p + "/" + q + " ÷ " + p2 + "/" + q2 + " = ?"; break;
                case 1: d1 = new int[] { 12, 14, 15, 24, 25, 35, 45, 16, 18 }[NextRnd(9)]; q1 = NextRnd(2, 5); k2 = d1 * q1; if (k2 > 99) { q1 = 2; k2 = d1 * 2; } ans = q1; text = DecStr(k2) + " ÷ " + DecStr(d1) + " = ?"; break;
                case 2: X = new int[] { 5, 10, 20, 25, 50, 60, 75, 80 }[NextRnd(8)]; Y = NextRnd(4, 80); g = 0; while ((X * Y) % 100 != 0 && g < 40) { Y = NextRnd(4, 80); g++; } if ((X * Y) % 100 != 0) { X = 25; Y = 4 * NextRnd(1, 20); } ans = X * Y / 100; text = X + "% × " + Y + " = ?"; break;
                case 3:
                    if (NextRnd(2) == 0) { dd2 = NextRnd(10, 100); ans = dd2; text = "0." + dd2 + " 化成百分数 = ?%"; }
                    else { q = new int[] { 4, 5, 10, 20, 25 }[NextRnd(5)]; p = NextRnd(1, q); g = 0; while ((p * 100) % q != 0 && g < 20) { p = NextRnd(1, q); g++; } if ((p * 100) % q != 0) { q = 4; p = 1; } ans = p * 100 / q; text = p + "/" + q + " 化成百分数 = ?%"; }
                    break;
                case 4:
                    switch (NextRnd(3))
                    {
                        case 0: a = NextRnd(11, 30); b = NextRnd(11, 30); c = NextRnd(2, 10); d = NextRnd(2, 10); ans = a * b + c * d; text = a + " × " + b + " + " + c + " × " + d + " = ?"; break;
                        case 1: a = 2 * NextRnd(1, 5); b = NextRnd(1, 20); ans = 25 * a / 10 + b; text = "2.5 × " + a + " + " + b + " = ?"; break;
                        default: d = NextRnd(2, 10); bb = NextRnd(2, 10); cc2 = NextRnd(2, 10); bc = bb * cc2; kk2 = NextRnd(2, 10); a = d * kk2 - bc; g = 0; while (a < 0 && g < 30) { d = NextRnd(2, 10); bb = NextRnd(2, 10); cc2 = NextRnd(2, 10); bc = bb * cc2; kk2 = NextRnd(2, 10); a = d * kk2 - bc; g++; } if (a < 0) a = 0; ans = kk2; text = "(" + a + " + " + bb + " × " + cc2 + ") ÷ " + d + " = ?"; break;
                    }
                    break;
                case 5:
                    switch (NextRnd(4))
                    {
                        case 0: a = NextRnd(2, 10); b = NextRnd(2, 10); c = NextRnd(2, 10); ans = a * b * c; text = "长方体长" + a + "宽" + b + "高" + c + "，体积=？"; break;
                        case 1: a = NextRnd(2, 8); ans = a * a * a; text = "正方体棱长" + a + "，体积=？"; break;
                        case 2: a = NextRnd(2, 10); b = NextRnd(2, 10); c = NextRnd(2, 10); ans = 2 * (a * b + b * c + a * c); text = "长方体长" + a + "宽" + b + "高" + c + "，表面积=？"; break;
                        default: a = NextRnd(2, 7); ans = 6 * a * a; text = "正方体棱长" + a + "，表面积=？"; break;
                    }
                    break;
                case 6:
                    rr = new int[] { 10, 20, 50 }[NextRnd(3)];
                    if (NextRnd(2) == 0) { ans = 314 * rr * rr / 100; text = "半径" + rr + "的圆面积=？(π取3.14)"; }
                    else { rr = (rr == 10) ? 50 : 100; ans = 628 * rr / 100; text = "半径" + rr + "的圆周长=？(π取3.14)"; }
                    break;
                default:
                    if (NextRnd(2) == 0) { v = 10 * NextRnd(4, 10); tt = NextRnd(2, 6); ans = v * tt; text = "汽车每小时行" + v + "千米，" + tt + "小时共行多少千米？"; }
                    else { x = NextRnd(4, 25); y = NextRnd(4, 25); gg = GCD(x, y); if (NextRnd(2) == 0) { ans = gg; text = x + " 和 " + y + " 的最大公因数 = ?"; } else { ans = x * y / gg; text = x + " 和 " + y + " 的最小公倍数 = ?"; } }
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
            if (pool.Count == 0) pool = new List<WordEntry>(chinese);   // 该年级无词条：先用全题库，避免空池取随机数异常
            if (pool.Count == 0) return MakeMathBatch(grade, count);    // 词库整体为空：退出口算题，保证题量
            Shuffle(pool);
            List<Question> list = new List<Question>();
            for (int i = 0; i < count; i++)
            {
                WordEntry e = (i < pool.Count) ? pool[i] : pool[NextRnd(pool.Count)];
                list.Add(BuildChinese(e));
            }
            return list;
        }

        static List<Question> PickEnglish(int grade, int count)
        {
            List<EnglishEntry> pool = FilterGrade(english, grade);
            if (pool.Count == 0) pool = new List<EnglishEntry>(english);  // 该年级无词条：先用全题库，避免空池取随机数异常
            if (pool.Count == 0) return MakeMathBatch(grade, count);      // 词库整体为空：退出口算题，保证题量
            Shuffle(pool);
            List<Question> list = new List<Question>();
            for (int i = 0; i < count; i++)
            {
                EnglishEntry e = (i < pool.Count) ? pool[i] : pool[NextRnd(pool.Count)];
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
                    if (w.Count == 0) w = new List<WordEntry>(chinese);
                    if (w.Count == 0) { pools.Add(MakeMathBatch(grade, total)); continue; }
                    Shuffle(w);
                    List<Question> q = new List<Question>();
                    // 取满 total：词条不足时允许重复（原先只取 min(w.Count,total)，总题数会不足）
                    for (int i = 0; i < total; i++) q.Add(BuildChinese(w[(i < w.Count) ? i : NextRnd(w.Count)]));
                    pools.Add(q);
                }
                else
                {
                    List<EnglishEntry> w = FilterGrade(english, grade);
                    if (w.Count == 0) w = new List<EnglishEntry>(english);
                    if (w.Count == 0) { pools.Add(MakeMathBatch(grade, total)); continue; }
                    Shuffle(w);
                    List<Question> q = new List<Question>();
                    for (int i = 0; i < total; i++) q.Add(BuildEnglish(w[(i < w.Count) ? i : NextRnd(w.Count)]));
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
                int p = cand[NextRnd(cand.Count)];
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

        // ===== 多选版：科目多选 + 年级多选 =====
        /// <summary>按多选科目与多选年级出 count 道题（各科目从选中年级范围内取题）。</summary>
        public static List<Question> GetBatch(int[] grades, Subject[] subjects, int count)
        {
            int[] gs = (grades != null && grades.Length > 0) ? grades : new int[] { 3 };
            Subject[] ss = (subjects != null && subjects.Length > 0)
                ? subjects : new Subject[] { Subject.Chinese, Subject.Math, Subject.English };
            return PickFromSubjects(gs, count, ss);
        }

        // 多选版：科目间轮转均派；数学按选中年级随机难度，语/英取选中年级词库
        static List<Question> PickFromSubjects(int[] grades, int total, params Subject[] subs)
        {
            List<List<Question>> pools = new List<List<Question>>();
            foreach (var s in subs)
            {
                if (s == Subject.Math) pools.Add(MakeMathBatch(grades, total));
                else if (s == Subject.Chinese) pools.Add(BuildChinesePool(grades, total));
                else pools.Add(BuildEnglishPool(grades, total));
            }
            List<Question> list = new List<Question>();
            int[] used = new int[subs.Length];
            int guard = 0;
            while (list.Count < total && guard < total * 40 + 40)
            {
                guard++;
                bool took = false;
                for (int i = 0; i < subs.Length && list.Count < total; i++)
                {
                    if (used[i] >= pools[i].Count) continue;
                    list.Add(pools[i][used[i]]);
                    used[i]++;
                    took = true;
                }
                if (!took) break;
            }
            Shuffle(list);
            return list;
        }

        // 生成 n 道互不相同（按题干）的数学题，年级在 grades 中随机选取
        static List<Question> MakeMathBatch(int[] grades, int n)
        {
            List<Question> list = new List<Question>();
            HashSet<string> seen = new HashSet<string>();
            int guard = 0;
            while (list.Count < n && guard < n * 80 + 80)
            {
                guard++;
                Question q = MakeMath(grades[NextRnd(grades.Length)]);
                if (seen.Add(q.Prompt)) list.Add(q);
            }
            return list;
        }

        static List<Question> BuildChinesePool(int[] grades, int total)
        {
            List<WordEntry> pool = FilterGrades(chinese, grades);
            if (pool.Count == 0) pool = new List<WordEntry>(chinese);   // 选中年级无词条：用全题库
            if (pool.Count == 0) return MakeMathBatch(grades, total);   // 词库整体为空：退出口算题
            Shuffle(pool);
            List<Question> q = new List<Question>();
            for (int i = 0; i < total; i++)
            {
                WordEntry e = (i < pool.Count) ? pool[i] : pool[NextRnd(pool.Count)];
                q.Add(BuildChinese(e));
            }
            return q;
        }

        static List<Question> BuildEnglishPool(int[] grades, int total)
        {
            List<EnglishEntry> pool = FilterGrades(english, grades);
            if (pool.Count == 0) pool = new List<EnglishEntry>(english);  // 选中年级无词条：用全题库
            if (pool.Count == 0) return MakeMathBatch(grades, total);     // 词库整体为空：退出口算题
            Shuffle(pool);
            List<Question> q = new List<Question>();
            for (int i = 0; i < total; i++)
            {
                EnglishEntry e = (i < pool.Count) ? pool[i] : pool[NextRnd(pool.Count)];
                q.Add(BuildEnglish(e));
            }
            return q;
        }

        static List<WordEntry> FilterGrades(List<WordEntry> src, int[] grades)
        {
            List<WordEntry> r = new List<WordEntry>();
            foreach (var x in src)
                foreach (int g in grades)
                    if (x.Grade == g) { r.Add(x); break; }
            return r;
        }
        static List<EnglishEntry> FilterGrades(List<EnglishEntry> src, int[] grades)
        {
            List<EnglishEntry> r = new List<EnglishEntry>();
            foreach (var x in src)
                foreach (int g in grades)
                    if (x.Grade == g) { r.Add(x); break; }
            return r;
        }

        static Question BuildChinese(WordEntry e)
        {
            Question q = new Question();
            q.Subject = Subject.Chinese; q.Grade = e.Grade; q.Type = QType.ChinesePinyin;
            q.Prompt = e.Word; q.Explanation = e.Meaning; q.Answer = -1;
            List<string> opts = new List<string>(); opts.Add(e.Pinyin);
            AddChineseDistractors(opts, FilterGrade(chinese, e.Grade), e);
            if (opts.Count < 4) AddChineseDistractors(opts, new List<WordEntry>(chinese), e);  // 本年级干扰项不足：从全题库补
            if (opts.Count < 2) return MakeMath(e.Grade);   // 词库退化（凑不出 1 个干扰项）：退出口算题，避免占位符或唯一选项
            Shuffle(opts);
            q.Options = opts.ToArray();
            q.CorrectIndex = IndexOfStr(q.Options, e.Pinyin);
            return q;
        }

        // 从 pool 中补干扰项（与正确项不同、互不重复），补满 4 个即止
        static void AddChineseDistractors(List<string> opts, List<WordEntry> pool, WordEntry self)
        {
            Shuffle(pool);
            foreach (var p in pool)
            {
                if (opts.Count >= 4) break;
                if (p == self) continue;
                if (p.Pinyin != self.Pinyin && !opts.Contains(p.Pinyin)) opts.Add(p.Pinyin);
            }
        }

        static Question BuildEnglish(EnglishEntry e)
        {
            Question q = new Question();
            q.Subject = Subject.English; q.Grade = e.Grade; q.Type = QType.EnglishWord;
            q.Answer = -1; q.Explanation = "";
            bool wordToZh = NextRnd(2) == 0;
            List<string> opts = new List<string>();
            string correct;
            if (wordToZh) { q.Prompt = e.En; opts.Add(e.Zh); correct = e.Zh; }
            else { q.Prompt = e.Zh; opts.Add(e.En); correct = e.En; }
            AddEnglishDistractors(opts, FilterGrade(english, e.Grade), e, wordToZh);
            if (opts.Count < 4) AddEnglishDistractors(opts, new List<EnglishEntry>(english), e, wordToZh);  // 本年级干扰项不足：从全题库补
            if (opts.Count < 2) return MakeMath(e.Grade);   // 词库退化（凑不出 1 个干扰项）：退出口算题，避免占位符或唯一选项
            Shuffle(opts);
            q.Options = opts.ToArray();
            q.CorrectIndex = IndexOfStr(q.Options, correct);
            return q;
        }

        // 从候选池 pool 中补干扰项（与正确项不同、互不重复），补满 4 个即止
        static void AddEnglishDistractors(List<string> opts, List<EnglishEntry> pool, EnglishEntry self, bool useZh)
        {
            Shuffle(pool);
            foreach (var p in pool)
            {
                if (opts.Count >= 4) break;
                if (p == self) continue;
                string v = useZh ? p.Zh : p.En;
                string selfv = useZh ? self.Zh : self.En;
                if (v != selfv && !opts.Contains(v)) opts.Add(v);
            }
        }

        // ===== 本批新增：学期/题库、难度倾向、题型扩展、线程安全随机 =====

        static readonly object RndLock = new object();
        static int NextRnd(int max) { lock (RndLock) { return rnd.Next(max); } }
        static int NextRnd(int min, int max) { lock (RndLock) { return rnd.Next(min, max); } }

        static int ParseTerm(string s)
        {
            if (s == null) return 0;
            string t = s.Trim();
            if (t == "上" || t == "1") return 1;
            if (t == "下" || t == "2") return 2;
            return 0;
        }

        static int DiffOf(Question q)
        {
            if (q == null) return 1;
            int d = q.Grade <= 2 ? 1 : (q.Grade <= 4 ? 2 : 3);
            if ((q.Type == QType.MathCalc || q.Type == QType.MathChoice) && d > 1) d--;
            return d;
        }

        /// <summary>按配置生成一批题：难度 80/20 倾向、学期/题库过滤、三种新题型混入。</summary>
        public static List<Question> GetBatchEx(int[] grades, Subject[] subjects, int count,
            int preferDifficulty, int term, string bank, bool mathChoice, bool chineseWord, bool trueFalse)
        {
            if (count < 1) count = 1;
            int poolSize = Math.Max(count * 2, count + 10);
            List<Question> pool = PickFromSubjects(grades == null ? new int[] { 3 } : grades,
                poolSize, subjects);
            pool = FilterScope(pool, term, bank);
            pool = EnrichBatch(pool, mathChoice, chineseWord, trueFalse);
            return SelectByDifficulty(pool, count, preferDifficulty);
        }

        static List<Question> FilterScope(List<Question> src, int term, string bank)
        {
            List<Question> r = new List<Question>();
            foreach (Question q in src)
            {
                if (term > 0 && q.Term != 0 && q.Term != term) continue;
                if (!string.IsNullOrEmpty(bank) &&
                    !string.IsNullOrEmpty(q.Bank) && q.Bank != bank) continue;
                r.Add(q);
            }
            return r.Count == 0 ? src : r;   // 过滤后为空则不收窄，保证永不空题库
        }

        static List<Question> EnrichBatch(List<Question> src, bool mathChoice, bool chineseWord, bool trueFalse)
        {
            for (int i = 0; i < src.Count; i++)
            {
                Question q = src[i];
                if (q.Type == QType.MathCalc && trueFalse && i % 7 == 0) src[i] = ToTrueFalse(q);
                else if (q.Type == QType.MathCalc && mathChoice && i % 3 == 0) src[i] = ToMathChoice(q);
                else if (q.Type == QType.ChinesePinyin && chineseWord && i % 2 == 0) src[i] = ToChineseWord(q);
            }
            return src;
        }

        static Question ToMathChoice(Question q)
        {
            List<string> opts = new List<string>();
            opts.Add(q.Answer.ToString());
            int[] deltas = new int[] { 1, -1, 2, -2, 10, -10 };
            foreach (int d in deltas)
            {
                if (opts.Count >= 4) break;
                string v = (q.Answer + d).ToString();
                if (!opts.Contains(v)) opts.Add(v);
            }
            int k = 1;
            while (opts.Count < 4)
            {
                string v = (q.Answer + 100 + k).ToString();
                if (!opts.Contains(v)) opts.Add(v);
                k++;
            }
            string correct = opts[0];
            Shuffle(opts);
            Question r = Clone(q);
            r.Type = QType.MathChoice;
            r.Options = opts.ToArray();
            r.CorrectIndex = IndexOfStr(opts.ToArray(), correct);
            r.Answer = -1;
            return r;
        }

        static Question ToTrueFalse(Question q)
        {
            bool statementTrue = NextRnd(2) == 0;
            int shown = statementTrue ? q.Answer : q.Answer + 1;
            // 口算题题干本身以「= ?」结尾，直接拼会变成「12 × 3 = ? = 36，对吗？」
            string stem = q.Prompt == null ? "" : q.Prompt.Replace("= ?", "").TrimEnd();
            if (stem.Length == 0) stem = q.Prompt;
            Question r = Clone(q);
            r.Type = QType.TrueFalse;
            r.Prompt = stem + " = " + shown + "，对吗？";
            r.Options = new string[] { "对", "错" };
            r.CorrectIndex = statementTrue ? 0 : 1;
            r.Answer = -1;
            return r;
        }

        static Question ToChineseWord(Question q)
        {
            string pinyin = (q.Options != null && q.CorrectIndex >= 0 && q.CorrectIndex < q.Options.Length)
                ? q.Options[q.CorrectIndex] : q.Prompt;
            List<string> opts = new List<string>();
            opts.Add(q.Prompt);
            List<WordEntry> same = FilterGrade(chinese, q.Grade);
            Shuffle(same);
            foreach (WordEntry e in same)
            {
                if (opts.Count >= 4) break;
                if (e.Word != q.Prompt && !opts.Contains(e.Word)) opts.Add(e.Word);
            }
            foreach (WordEntry e in chinese)
            {
                if (opts.Count >= 4) break;
                if (e.Word != q.Prompt && !opts.Contains(e.Word)) opts.Add(e.Word);
            }
            if (opts.Count < 2) return q;   // 词库退化时保持原题
            string correct = opts[0];
            Shuffle(opts);
            Question r = Clone(q);
            r.Type = QType.ChineseWord;
            r.Prompt = "「" + pinyin + "」是哪个词？";
            r.Options = opts.ToArray();
            r.CorrectIndex = IndexOfStr(opts.ToArray(), correct);
            return r;
        }

        static Question Clone(Question q)
        {
            Question r = new Question();
            r.Subject = q.Subject; r.Grade = q.Grade; r.Type = q.Type; r.Prompt = q.Prompt;
            r.Options = q.Options; r.CorrectIndex = q.CorrectIndex; r.Answer = q.Answer;
            r.Explanation = q.Explanation; r.Term = q.Term; r.Bank = q.Bank;
            return r;
        }

        static List<Question> SelectByDifficulty(List<Question> pool, int count, int prefer)
        {
            List<Question> src = new List<Question>(pool);
            Shuffle(src);
            List<Question> r = new List<Question>();
            if (prefer < 1 || prefer > 3)
            {
                for (int i = 0; i < count && i < src.Count; i++) r.Add(src[i]);
                return r;
            }
            while (r.Count < count && src.Count > 0)
            {
                int idx = -1;
                if (NextRnd(100) < 80)
                {
                    for (int i = 0; i < src.Count; i++) if (DiffOf(src[i]) == prefer) { idx = i; break; }
                }
                if (idx < 0) idx = NextRnd(src.Count);
                r.Add(src[idx]);
                src.RemoveAt(idx);
            }
            return r;
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
                int j = NextRnd(i + 1);
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
