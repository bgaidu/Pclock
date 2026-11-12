using System;
using System.Collections.Generic;
using PCLock;
using TUnit;

/// <summary>
/// 第七批「可靠性」回归：UI 渲染纯函数、诊断报告脱敏。
/// 重点覆盖 v1.6.4 事故里「构造期窗体未 Show」这一无法在真机外复现的场景。
/// </summary>
public class Batch6Tests
{
    // ===== QuestionView：题目 → 选项按钮渲染 =====

    static Question Choice(string[] opts)
    {
        Question q = new Question();
        q.Type = QType.ChinesePinyin;
        q.Prompt = "示例题";
        q.Options = opts;
        q.CorrectIndex = 0;
        q.Answer = -1;
        return q;
    }

    [Test]
    public void JudgeQuestionRendersTwoButtons()
    {
        OptionSlot[] s = QuestionView.Build(Choice(new string[] { "对", "错" }), 4);
        Assert.AreEqual(4, s.Length, "按钮数固定");
        Assert.IsTrue(s[0].Visible, "第 1 项可见");
        Assert.IsTrue(s[1].Visible, "第 2 项可见");
        Assert.IsFalse(s[2].Visible, "第 3 项隐藏");
        Assert.IsFalse(s[3].Visible, "第 4 项隐藏");
        Assert.AreEqual("对", s[0].Text, "第 1 项文字");
        Assert.AreEqual("错", s[1].Text, "第 2 项文字");
        Assert.AreEqual(0, s[0].Index, "第 1 项下标");
        Assert.AreEqual(1, s[1].Index, "第 2 项下标");
        Assert.AreEqual(-1, s[2].Index, "隐藏项无下标");
        Assert.AreEqual("", s[2].Text, "隐藏项文字清空");
    }

    [Test]
    public void FourChoiceQuestionRendersAllButtons()
    {
        OptionSlot[] s = QuestionView.Build(Choice(new string[] { "A", "B", "C", "D" }), 4);
        for (int i = 0; i < 4; i++)
        {
            Assert.IsTrue(s[i].Visible, "第 " + i + " 项可见");
            Assert.AreEqual(i, s[i].Index, "第 " + i + " 项下标");
        }
        Assert.AreEqual("D", s[3].Text, "第 4 项文字");
    }

    [Test]
    public void MathCalcHidesEveryButton()
    {
        Question q = new Question();
        q.Type = QType.MathCalc;
        q.Prompt = "1 + 1 = ?";
        q.Answer = 2;
        q.Options = new string[] { "1", "2", "3", "4" };   // 即使残留旧选项也必须全隐藏
        OptionSlot[] s = QuestionView.Build(q, 4);
        for (int i = 0; i < 4; i++)
        {
            Assert.IsFalse(s[i].Visible, "填空题第 " + i + " 项不应显示");
            Assert.AreEqual(-1, s[i].Index, "填空题第 " + i + " 项无下标");
        }
    }

    [Test]
    public void NullQuestionAndNullOptionsAreSafe()
    {
        OptionSlot[] a = QuestionView.Build(null, 4);
        Assert.AreEqual(4, a.Length, "null 题目仍返回完整数组");
        for (int i = 0; i < 4; i++) Assert.IsFalse(a[i].Visible, "null 题目全隐藏");

        Question q = Choice(null);
        OptionSlot[] b = QuestionView.Build(q, 4);
        for (int i = 0; i < 4; i++) Assert.IsFalse(b[i].Visible, "null 选项全隐藏");

        OptionSlot[] c = QuestionView.Build(Choice(new string[] { "对", "错" }), 0);
        Assert.AreEqual(0, c.Length, "0 个按钮不抛异常");
    }

    [Test]
    public void TagOfNeverReturnsInvalidIndexForHiddenSlot()
    {
        OptionSlot[] s = QuestionView.Build(Choice(new string[] { "对", "错" }), 4);
        Assert.AreEqual(0, QuestionView.TagOf(s[0]), "可见项返回下标");
        Assert.AreEqual(null, QuestionView.TagOf(s[2]), "隐藏项返回 null");
        Assert.AreEqual(null, QuestionView.TagOf(null), "null 槽返回 null");
        // 点击侧解析：隐藏槽的 null 必须被拒绝，绝不拆箱
        Assert.AreEqual(UiSafe.InvalidIndex, UiSafe.OptionIndex(QuestionView.TagOf(s[2]), 2), "隐藏槽不可点击");
    }

    /// <summary>
    /// 负向变异：还原 v1.6.4 的错误写法（用控件 Visible getter 判断），
    /// 证明它与正确实现可被测试区分 —— 这类 bug 以后会直接红，不会静默上线。
    /// </summary>
    [Test]
    public void Mutation_OldVisibleGetterPatternIsDetectable()
    {
        bool formShown = false;                       // 构造期：窗体尚未 Show
        string[] opts = new string[] { "对", "错" };
        string[] oldText = new string[4];
        object[] oldTag = new object[4];
        for (int i = 0; i < 4; i++)
        {
            bool visibleGetter = formShown && i < opts.Length;   // WinForms 构造期恒 false
            if (visibleGetter) { oldText[i] = opts[i]; oldTag[i] = i; }
        }
        Assert.AreEqual(null, oldText[0], "旧写法在构造期写不出文字");
        Assert.AreEqual(null, oldTag[0], "旧写法在构造期写不出 Tag");

        OptionSlot[] s = QuestionView.Build(Choice(opts), 4);
        Assert.AreEqual("对", s[0].Text, "新实现必须写出文字");
        Assert.AreEqual(0, QuestionView.TagOf(s[0]), "新实现必须写出 Tag");
    }

    // ===== Diagnostics：一键诊断包（脱敏 / 截断 / 组装） =====

    [Test]
    public void RedactKeepsNormalLogLines()
    {
        Assert.IsFalse(Diagnostics.IsSensitive("[12:00:00] EnsureWatchdog: 看门狗已启动"));
        Assert.AreEqual("abc" + Environment.NewLine, Diagnostics.Redact("abc"), "普通行原样保留");
    }

    [Test]
    public void RedactHidesCredentialLines()
    {
        Assert.IsTrue(Diagnostics.IsSensitive("PinHash=9f8e7d6c"), "哈希键应被识别");
        Assert.IsTrue(Diagnostics.IsSensitive("密码哈希: abc"), "中文键也应被识别");
        string r = Diagnostics.Redact("normal\r\nPinHash=supersecret\r\ntail");
        Assert.IsTrue(r.IndexOf("supersecret", StringComparison.Ordinal) < 0, "敏感值不得进入报告");
        Assert.IsTrue(r.IndexOf("normal", StringComparison.Ordinal) >= 0, "普通行保留");
        Assert.IsTrue(r.IndexOf("[已脱敏", StringComparison.Ordinal) >= 0, "留下脱敏标记");
    }

    [Test]
    public void TailKeepsOnlyTheEnd()
    {
        Assert.AreEqual("(空)", Diagnostics.Tail("", 10), "空文本");
        Assert.AreEqual("abc", Diagnostics.Tail("abc", 10), "短文本不截断");
        string t = Diagnostics.Tail(new string('x', 20), 5);
        Assert.IsTrue(t.EndsWith("xxxxx", StringComparison.Ordinal), "保留末尾 5 字符");
        Assert.IsTrue(t.IndexOf("前面已省略", StringComparison.Ordinal) >= 0, "标注省略量");
    }

    [Test]
    public void ReportHasAllSectionsAndNoCredentials()
    {
        string r = Diagnostics.BuildReport("1.6.6", "abc1234", @"C:\Program Files\TVLock",
            new string[] { "PCLock.exe  500000 B" }, "line1\nPinHash=supersecret",
            new string[] { "2026-11-11.log\nOK" }, "题量=10",
            new DateTime(2026, 11, 12, 10, 0, 0));
        Assert.IsTrue(r.IndexOf("[1] 安装目录文件清单", StringComparison.Ordinal) >= 0, "段 1");
        Assert.IsTrue(r.IndexOf("[2] err.txt", StringComparison.Ordinal) >= 0, "段 2");
        Assert.IsTrue(r.IndexOf("[3] 最近", StringComparison.Ordinal) >= 0, "段 3");
        Assert.IsTrue(r.IndexOf("[4] 配置摘要", StringComparison.Ordinal) >= 0, "段 4");
        Assert.IsTrue(r.IndexOf("v1.6.6 (abc1234)", StringComparison.Ordinal) >= 0, "版本与 commit");
        Assert.IsTrue(r.IndexOf("supersecret", StringComparison.Ordinal) < 0, "凭据被脱敏");
    }

    [Test]
    public void FileNameIsSortableAndTxt()
    {
        Assert.AreEqual("PCLock-diag-20261112-090503.txt",
            Diagnostics.FileName(new DateTime(2026, 11, 12, 9, 5, 3)));
    }
}
