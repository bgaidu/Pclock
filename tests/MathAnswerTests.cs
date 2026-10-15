using System;
using System.Collections.Generic;
using PCLock;
using TUnit;

/// <summary>
/// 数学题答案一致性测试。
/// 对 QuestionBank.MakeMath 生成的每一道题，用一个**独立实现**的有理数表达式
/// 求值器复算题干，与 q.Answer 比对——生成器写错答案会被这里抓住
/// （答案错误会直接冤枉孩子，属于最高优先级正确性问题）。
///
/// 求值器覆盖 Gen1-Gen6 全部题型：
///   - 四则运算（+ - × ÷，含优先级、圆括号/方括号、连算）
///   - 小数（如 2.5 × 4）、分数（如 5/7 + 9/7）、百分数（如 25% × 4）
///   - 中文题干：最大公因数/最小公倍数、化成百分数、长方体/正方体体积表面积、
///     圆面积/圆周长（π取3.14）、行程问题
/// 全部用分数（long 分子/分母，GCD 约分）精确计算，无浮点误差。
/// </summary>
public class MathAnswerTests
{
    // ===== 小型有理数（独立实现，与 Questions.cs 无任何共享代码）=====

    struct Frac
    {
        public readonly long N, D;   // 分母恒为正，已约分

        public Frac(long n, long d)
        {
            if (d == 0) d = 1;
            if (d < 0) { n = -n; d = -d; }
            long g = Gcd(Math.Abs(n), d);
            if (g > 1) { n /= g; d /= g; }
            N = n; D = d;
        }

        public static Frac operator +(Frac a, Frac b) { return new Frac(a.N * b.D + b.N * a.D, a.D * b.D); }
        public static Frac operator -(Frac a, Frac b) { return new Frac(a.N * b.D - b.N * a.D, a.D * b.D); }
        public static Frac operator *(Frac a, Frac b) { return new Frac(a.N * b.N, a.D * b.D); }
        public static Frac operator /(Frac a, Frac b) { return new Frac(a.N * b.D, a.D * b.N); }

        public bool IsInteger { get { return D == 1; } }
        public long AsLong { get { return N; } }   // 仅 IsInteger 时有效
    }

    static long Gcd(long a, long b)
    {
        while (b != 0) { long t = a % b; a = b; b = t; }
        return a;
    }

    struct EvalResult
    {
        public bool Recognized;   // 求值器是否理解该题干（false = 未覆盖的新题型）
        public bool IsInteger;    // 复算结果是否为整数（生成器约定"结果恒为整数"）
        public long Value;        // IsInteger 时的复算值
    }

    // ===== 中文题干模式 =====

    static List<long> Ints(string s)
    {
        List<long> r = new List<long>();
        long cur = 0; bool has = false;
        foreach (char c in s)
        {
            if (c >= '0' && c <= '9') { cur = cur * 10 + (c - '0'); has = true; }
            else if (has) { r.Add(cur); cur = 0; has = false; }
        }
        if (has) r.Add(cur);
        return r;
    }

    static bool TryEvaluateChinese(string prompt, out long result)
    {
        result = 0;
        string s = prompt;

        // 圆面积/圆周长：先去掉"(π取3.14)"说明，避免 3、14 被误当参数
        if (s.IndexOf("圆面积") >= 0 || s.IndexOf("圆周长") >= 0)
        {
            s = s.Replace("(π取3.14)", "");
            List<long> v = Ints(s);
            if (v.Count != 1) return false;
            long r = v[0];
            result = (s.IndexOf("圆面积") >= 0) ? (314 * r * r / 100) : (628 * r / 100);
            return true;
        }

        if (s.IndexOf("最大公因数") >= 0 || s.IndexOf("最小公倍数") >= 0)
        {
            List<long> v = Ints(s);
            if (v.Count != 2) return false;
            long g = Gcd(v[0], v[1]);
            result = (s.IndexOf("最大公因数") >= 0) ? g : (v[0] * v[1] / g);
            return true;
        }

        if (s.IndexOf("化成百分数") >= 0)
        {
            string head = s.Substring(0, s.IndexOf("化成百分数")).Trim();   // "0.55" 或 "1/4"
            int slash = head.IndexOf('/');
            if (slash >= 0)
            {
                long p, q;
                if (!long.TryParse(head.Substring(0, slash).Trim(), out p)) return false;
                if (!long.TryParse(head.Substring(slash + 1).Trim(), out q) || q == 0) return false;
                if ((p * 100) % q != 0) return false;   // 生成器保证整除；不整除即违约
                result = p * 100 / q;
                return true;
            }
            if (!head.StartsWith("0.")) return false;
            long d;
            if (!long.TryParse(head.Substring(2).Trim(), out d)) return false;
            result = d;
            return true;
        }

        if (s.IndexOf("长方体") >= 0 || s.IndexOf("正方体") >= 0)
        {
            List<long> v = Ints(s);
            bool vol = s.IndexOf("体积") >= 0;
            if (s.IndexOf("长方体") >= 0)
            {
                if (v.Count != 3) return false;
                result = vol ? (v[0] * v[1] * v[2]) : (2 * (v[0] * v[1] + v[1] * v[2] + v[0] * v[2]));
            }
            else
            {
                if (v.Count != 1) return false;
                result = vol ? (v[0] * v[0] * v[0]) : (6 * v[0] * v[0]);
            }
            return true;
        }

        if (s.IndexOf("汽车每小时行") >= 0)
        {
            List<long> v = Ints(s);
            if (v.Count != 2) return false;
            result = v[0] * v[1];
            return true;
        }

        return false;
    }

    // ===== 算术题干：剥掉尾部"= ?"后按表达式解析 =====

    static string StripArithmeticSuffix(string prompt)
    {
        int eq = prompt.LastIndexOf('=');
        if (eq < 0) return "";
        string tail = prompt.Substring(eq + 1).Trim();
        if (tail != "?" && tail != "？" && tail != "?%" && tail != "？%") return "";
        return prompt.Substring(0, eq).Trim();
    }

    static void SkipSpaces(string s, ref int pos)
    {
        while (pos < s.Length && s[pos] == ' ') pos++;
    }

    // expr := term (('+'|'-') term)*
    static bool TryParseExpr(string s, ref int pos, out Frac value)
    {
        value = new Frac(0, 1);
        if (!TryParseTerm(s, ref pos, out value)) return false;
        while (true)
        {
            SkipSpaces(s, ref pos);
            if (pos >= s.Length) return true;
            char c = s[pos];
            if (c != '+' && c != '-') return true;
            pos++;
            Frac rhs;
            if (!TryParseTerm(s, ref pos, out rhs)) return false;
            value = (c == '+') ? (value + rhs) : (value - rhs);
        }
    }

    // term := factor (('×'|'÷') factor)*
    static bool TryParseTerm(string s, ref int pos, out Frac value)
    {
        value = new Frac(0, 1);
        if (!TryParseFactor(s, ref pos, out value)) return false;
        while (true)
        {
            int save = pos;
            SkipSpaces(s, ref pos);
            if (pos < s.Length && (s[pos] == '×' || s[pos] == '÷'))
            {
                char op = s[pos];
                pos++;
                Frac rhs;
                if (!TryParseFactor(s, ref pos, out rhs)) return false;
                value = (op == '×') ? (value * rhs) : (value / rhs);
            }
            else
            {
                pos = save;   // 不属于本层，交回上层（不消耗空白）
                return true;
            }
        }
    }

    // factor := 数字['.'数字]['/'数字]['%'] | '(' expr ')' | '[' expr ']'
    // 题干中的数字均为非负，无需处理一元负号
    static bool TryParseFactor(string s, ref int pos, out Frac value)
    {
        value = new Frac(0, 1);
        SkipSpaces(s, ref pos);
        if (pos >= s.Length) return false;
        char c = s[pos];

        if (c == '(' || c == '[')
        {
            char close = (c == '(') ? ')' : ']';
            pos++;
            if (!TryParseExpr(s, ref pos, out value)) return false;
            SkipSpaces(s, ref pos);
            if (pos >= s.Length || s[pos] != close) return false;
            pos++;
            return true;
        }

        if (c < '0' || c > '9') return false;
        long n = 0;
        while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9') { n = n * 10 + (s[pos] - '0'); pos++; }
        Frac v = new Frac(n, 1);

        if (pos < s.Length && s[pos] == '.')   // 小数部分
        {
            pos++;
            long f = 0, scale = 1; bool any = false;
            while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
            { f = f * 10 + (s[pos] - '0'); scale *= 10; pos++; any = true; }
            if (!any) return false;
            v = new Frac(n * scale + f, scale);
        }

        if (pos < s.Length && s[pos] == '/')   // 分数部分 p/q
        {
            pos++;
            long q = 0; bool any = false;
            while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
            { q = q * 10 + (s[pos] - '0'); pos++; any = true; }
            if (!any || q == 0) return false;
            v = new Frac(v.N, v.D * q);
        }

        if (pos < s.Length && s[pos] == '%')   // 百分数 X% = X/100
        {
            pos++;
            v = new Frac(v.N, v.D * 100);
        }

        value = v;
        return true;
    }

    /// <summary>复算题干。Recognized=false 表示求值器未覆盖该题型（测试会失败并要求扩展）。</summary>
    static EvalResult Evaluate(string prompt)
    {
        EvalResult er = new EvalResult();
        long cn;
        if (TryEvaluateChinese(prompt, out cn))
        {
            er.Recognized = true; er.IsInteger = true; er.Value = cn;
            return er;
        }
        string expr = StripArithmeticSuffix(prompt);
        if (expr.Length == 0) return er;
        int pos = 0;
        Frac v;
        if (!TryParseExpr(expr, ref pos, out v)) return er;
        SkipSpaces(expr, ref pos);
        if (pos != expr.Length) return er;   // 尾部有未识别内容
        er.Recognized = true;
        er.IsInteger = v.IsInteger;
        er.Value = v.AsLong;
        return er;
    }

    // ===== 测试 =====

    // 主测试：每道数学题的标称答案必须与独立复算一致；复算覆盖率必须 100%
    // （出现未覆盖题干时要求扩展求值器，而不是悄悄跳过——跳过会掩盖生成器错误）。
    [Test]
    public void EveryMathQuestionAnswerMatchesPrompt()
    {
        int total = 0, recognized = 0;
        List<string> problems = new List<string>();

        for (int g = 1; g <= 6; g++)
        {
            for (int i = 0; i < 300; i++)
            {
                Question q = QuestionBank.MakeMath(g);
                total++;
                EvalResult er = Evaluate(q.Prompt);
                if (!er.Recognized)
                {
                    if (problems.Count < 8) problems.Add("G" + g + " 无法复算: [" + q.Prompt + "]");
                    continue;
                }
                recognized++;
                if (!er.IsInteger)
                {
                    if (problems.Count < 8) problems.Add("G" + g + " 非整数结果: [" + q.Prompt + "]");
                }
                else if (er.Value != q.Answer)
                {
                    if (problems.Count < 8)
                        problems.Add("G" + g + " 答案不一致: [" + q.Prompt + "] 标称 " + q.Answer + " ≠ 复算 " + er.Value);
                }
            }
        }

        // 年级越界回退分支（switch default → Gen6 内容）
        foreach (int g in new int[] { 0, 7 })
        {
            for (int i = 0; i < 100; i++)
            {
                Question q = QuestionBank.MakeMath(g);
                total++;
                EvalResult er = Evaluate(q.Prompt);
                if (!er.Recognized)
                {
                    if (problems.Count < 8) problems.Add("G" + g + " 无法复算: [" + q.Prompt + "]");
                    continue;
                }
                recognized++;
                if (!er.IsInteger || er.Value != q.Answer)
                {
                    if (problems.Count < 8)
                        problems.Add("G" + g + " 答案不一致: [" + q.Prompt + "] 标称 " + q.Answer + " ≠ 复算 " + er.Value);
                }
            }
        }

        foreach (string p in problems) Console.WriteLine("    [bad] " + p);
        Assert.AreEqual(total, recognized, "存在无法复算的题干（应扩展求值器覆盖新题型，而非跳过）");
        Assert.IsTrue(problems.Count == 0, "发现 " + problems.Count + " 个问题（详见上方 [bad] 行）");
    }

    // 年级越界不抛异常、题目形态合法
    [Test]
    public void GradeOutOfRangeStillProducesValidQuestion()
    {
        foreach (int g in new int[] { 0, 7, -1, 100 })
        {
            for (int i = 0; i < 50; i++)
            {
                Question q = QuestionBank.MakeMath(g);
                Assert.AreEqual(Subject.Math, q.Subject, "grade " + g + " 应为数学题");
                Assert.AreEqual(QType.MathCalc, q.Type, "grade " + g + " 题型应为口算");
                Assert.IsTrue(q.Answer >= 0, "grade " + g + " 答案应 >= 0");
                Assert.IsTrue(!string.IsNullOrEmpty(q.Prompt), "grade " + g + " 题干不应为空");
            }
        }
    }

    // 题干必须是"问句"形态：包含问号（半角 ? 或全角 ？）且位于结尾附近。
    // 允许问号后带简短后缀：百分互化题的 "?%"（% 是单位提示，见 Gen6 case 3）、
    // 圆题的 "？(π取3.14)"（取值约束备注，见 Gen6 case 5）。
    [Test]
    public void AllPromptsEndWithQuestionMark()
    {
        for (int g = 1; g <= 6; g++)
        {
            for (int i = 0; i < 50; i++)
            {
                string p = QuestionBank.MakeMath(g).Prompt;
                int qi = p.LastIndexOf('?');
                int fqi = p.LastIndexOf('？');
                int q = qi > fqi ? qi : fqi;
                Assert.IsTrue(q >= 0 && q >= p.Length - 10,
                    "题干应包含问号且位于结尾附近（允许 ?% / ？(π取3.14) 后缀）: [" + p + "]");
            }
        }
    }
}
