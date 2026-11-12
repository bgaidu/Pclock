using System;
using PCLock;
using TUnit;

/// <summary>第九批：答错反馈流程（题与答案不错位、手动继续）与硬锁/PIN 翻译缺口的纯逻辑回归。</summary>
public class Batch8Tests
{
    static string WithLang(string lang, Func<string> f)
    {
        string saved = Strings.Lang;
        try { Strings.Lang = lang; return f(); }
        finally { Strings.Lang = saved; }
    }

    [Test]
    public void FeedbackStatusWhileCoolingTellsChildToWait()
    {
        string s = AnswerFeedback.StatusText(true, 5);
        Assert.IsTrue(s.Contains("5"), "冷却中应显示剩余秒数");
        Assert.IsTrue(s.Contains("继续答题"), "反馈开启应引导孩子点「继续答题」");
        Assert.IsTrue(s.Contains("扣 1 星"), "必须提示答错扣 1 星");
    }

    [Test]
    public void FeedbackStatusAfterCoolingStillWaitsForManualContinue()
    {
        string s = AnswerFeedback.StatusText(true, 0);
        Assert.IsTrue(s.Contains("继续答题"), "冷却结束仍必须等孩子手动点继续，不能自动跳题");
        Assert.IsFalse(s.Contains("{0}"), "冷却结束后不应残留未替换占位符");
        Assert.IsFalse(s.Contains("共 "), "不能用 UpdateStatus 的「共 N 题」覆盖解析提示");
    }

    [Test]
    public void PlainStatusWhileCoolingKeepsRetryWording()
    {
        string s = AnswerFeedback.StatusText(false, 4);
        Assert.IsTrue(s.Contains("4"), "未开反馈时显示冷却秒数");
        Assert.IsTrue(s.Contains("再试"), "未开反馈时提示稍后再试");
        Assert.IsFalse(s.Contains("继续答题"), "未开反馈时不应出现继续按钮文案");
    }

    [Test]
    public void PlainStatusAfterCoolingResetsWithoutPlaceholder()
    {
        string s = AnswerFeedback.StatusText(false, 0);
        Assert.IsTrue(s.Contains("扣 1 星"), "冷却结束仍要提示扣星而不是清零");
        Assert.IsFalse(s.Contains("{0}"), "不应残留占位符");
    }

    [Test]
    public void AnswerInputStaysBlockedUntilManualContinue()
    {
        // 负向变异：若实现成「冷却结束即恢复答题输入」，孩子能对着屏幕上的正确答案
        // 反复点旧题刷判定，扣星与补题的语义被绕过。
        Assert.IsTrue(AnswerFeedback.BlocksAnswerInput(true), "反馈停留期必须拦截答题输入");
        Assert.IsFalse(AnswerFeedback.BlocksAnswerInput(false), "非反馈期不应拦截");
    }

    [Test]
    public void FeedbackStatusSwitchesLanguageImmediately()
    {
        string zh = WithLang("zh", delegate { return AnswerFeedback.StatusText(true, 0); });
        string en = WithLang("en", delegate { return AnswerFeedback.StatusText(true, 0); });
        Assert.IsTrue(zh.Contains("继续答题"), "中文模式应为中文提示");
        Assert.IsTrue(en.Contains("Continue"), "英文模式应即时出现 Continue");
        Assert.IsFalse(en.Contains("继续答题"), "英文模式不应残留中文");
    }

    [Test]
    public void HardLockReasonsAreTranslated()
    {
        string[] keys = new string[] {
            "今日使用额度已用完", "护眼休息时间", "系统时间异常，保护性锁定",
            "当前规则：", "允许使用时段：", "请使用家长 PIN 解锁"
        };
        foreach (string k in keys)
        {
            Assert.IsTrue(Strings.Contains(k), "硬锁文案未登记词典：" + k);
            string en = WithLang("en", delegate { return Strings.Get(k); });
            Assert.IsFalse(en == k, "硬锁文案缺英文翻译：" + k);
            Assert.IsFalse(en.Contains("额度") || en.Contains("休息") || en.Contains("规则"),
                "硬锁文案英文中混入中文：" + k);
        }
    }

    [Test]
    public void PinErrorTemplatesAreTranslated()
    {
        string zh = WithLang("zh", delegate { return Strings.Fmt("PIN 错误（还可尝试 {0} 次）", 2); });
        string en = WithLang("en", delegate { return Strings.Fmt("PIN 错误（还可尝试 {0} 次）", 2); });
        Assert.IsTrue(zh.Contains("2"), "中文模板应替换占位符");
        Assert.IsTrue(en.Contains("2") && en.Contains("attempt"), "英文模板应即时生效");
        Assert.IsFalse(en.Contains("错误"), "英文模板不应残留中文");

        string cool = WithLang("en", delegate { return Strings.Fmt("PIN 错误次数过多，请 {0} 秒后再试", 9); });
        Assert.IsTrue(cool.Contains("9") && !cool.Contains("错误"), "PIN 冷却提示也需即时翻译");
    }

    [Test]
    public void AnswerExplanationIsRebuildableAfterLanguageSwitch()
    {
        Question q = new Question();
        q.Type = QType.MathCalc;
        q.Answer = 42;
        q.Prompt = "6 × 7 = ?";
        string zh = WithLang("zh", delegate { return AnswerFeedback.Build(q); });
        string en = WithLang("en", delegate { return AnswerFeedback.Build(q); });
        Assert.IsTrue(zh.Contains("正确答案") && zh.Contains("42"), "中文解析应含答案");
        Assert.IsTrue(en.Contains("Correct answer") && en.Contains("42"), "切语言后重算解析应为英文");
        Assert.IsFalse(en.Contains("正确答案"), "解析不应残留中文");
    }

    [Test]
    public void StatusTextNeverReturnsEmpty()
    {
        foreach (bool on in new bool[] { true, false })
            foreach (int sec in new int[] { 0, 1, 5, 30 })
                Assert.IsTrue(AnswerFeedback.StatusText(on, sec).Length > 0,
                    "状态行不能为空（feedback=" + on + ", sec=" + sec + "）");
    }

    [Test]
    public void WrongAnswerCostsOneStarButNeverGoesNegative()
    {
        Assert.AreEqual(2, AnswerFlow.StarsAfterWrong(3), "答错扣 1 星，不是清零");
        Assert.AreEqual(0, AnswerFlow.StarsAfterWrong(1), "1 星答错扣到 0");
        Assert.AreEqual(0, AnswerFlow.StarsAfterWrong(0), "扣完为止，不能变成负数");
    }

    [Test]
    public void CorrectAnswerAddsOneStarAndReachesTarget()
    {
        Assert.AreEqual(1, AnswerFlow.StarsAfterCorrect(0));
        Assert.AreEqual(5, AnswerFlow.StarsAfterCorrect(4));
        Assert.IsTrue(AnswerFlow.Reached(10, 10), "达到目标星数应判定达标");
        Assert.IsFalse(AnswerFlow.Reached(9, 10), "差一星未达标");
        Assert.IsFalse(AnswerFlow.Reached(0, 0), "目标为 0 时不应判达标");
    }

    [Test]
    public void WrongAnswerKeepsProgressInsteadOfResetting()
    {
        // 负向变异守卫：若有人把答错改回「整批清零」，连错 3 题后星数会变成 0
        int stars = 7;
        for (int i = 0; i < 3; i++) stars = AnswerFlow.StarsAfterWrong(stars);
        Assert.AreEqual(4, stars, "连错 3 题只扣 3 星，进度不能归零");
    }
}
