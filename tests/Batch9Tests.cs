using System;
using PCLock;
using TUnit;

/// <summary>第十批：锁屏运行期动态文案纯函数（硬锁/时段/PIN 提示）与家长面板层级守卫。</summary>
public class Batch9Tests
{
    static string WithLang(string lang, Func<string> f)
    {
        string saved = Strings.Lang;
        try { Strings.Lang = lang; return f(); }
        finally { Strings.Lang = saved; }
    }

    [Test]
    public void HardLockTitleTranslatesAndHandlesEmpty()
    {
        string zh = WithLang("zh", delegate { return UiText.HardLockTitle("今日使用额度已用完"); });
        string en = WithLang("en", delegate { return UiText.HardLockTitle("今日使用额度已用完"); });
        Assert.IsTrue(zh.Contains("额度"), "中文模式保留原文");
        Assert.AreEqual("Daily usage quota reached", en, "英文模式应翻译硬锁标题");
        Assert.AreEqual("", UiText.HardLockTitle(null), "空原因不应抛异常");
        Assert.AreEqual("", UiText.HardLockTitle(""), "空串原因不应抛异常");
    }

    [Test]
    public void HardLockQuestionShowsPinOrRules()
    {
        string noRules = UiText.HardLockQuestion("");
        string withRules = UiText.HardLockQuestion("1-5|17:00-21:00@quiz");
        Assert.IsTrue(noRules.Contains("PIN"), "无规则时应提示家长 PIN");
        Assert.IsTrue(withRules.Contains("当前规则："), "有规则时显示规则前缀");
        Assert.IsTrue(withRules.Contains("1-5|17:00-21:00@quiz"), "规则内容不能被改写或丢掉");
    }

    [Test]
    public void HardLockQuestionIsLocalized()
    {
        string en = WithLang("en", delegate { return UiText.HardLockQuestion("1-5|17:00-21:00"); });
        Assert.IsTrue(en.Contains("Current rules:"), "规则前缀需翻译");
        Assert.IsFalse(en.Contains("当前规则"), "不应残留中文");
        Assert.IsTrue(en.Contains("1-5|17:00-21:00"), "规则文本本身不能翻译");
    }

    [Test]
    public void WindowQuestionHandlesEmptyWindow()
    {
        Assert.IsTrue(UiText.WindowQuestion(null).Contains("当前不在允许使用时段"), "空时段给出兜底文案");
        Assert.IsTrue(UiText.WindowQuestion("  ").Contains("当前不在允许使用时段"), "纯空白也视为空");
        string s = UiText.WindowQuestion("08:00-12:00;14:00-21:00");
        Assert.IsTrue(s.Contains("允许使用时段：") && s.Contains("08:00-12:00"), "有时段时显示规则");
    }

    [Test]
    public void WindowStatusAppendsCountdownOnlyWhenGiven()
    {
        string plain = UiText.WindowStatus("");
        string timed = UiText.WindowStatus("1 小时 20 分");
        Assert.IsFalse(plain.Contains("距下次可用"), "无倒计时时不追加");
        Assert.IsTrue(timed.Contains("距下次可用还有") && timed.Contains("1 小时 20 分"), "有倒计时时追加");
        Assert.IsFalse(UiText.WindowStatus(null).Contains("距下次可用"), "null 也视为无倒计时");
    }

    [Test]
    public void WindowStatusIsLocalized()
    {
        string en = WithLang("en", delegate { return UiText.WindowStatus("2 h"); });
        Assert.IsTrue(en.Contains("Unlocks automatically") && en.Contains("Next available"), "英文状态行");
        Assert.IsTrue(en.Contains("2 h"), "倒计时数值保留");
        Assert.IsFalse(en.Contains("时段"), "不应残留中文");
    }

    [Test]
    public void PinLabelCoversThreeStates()
    {
        Assert.IsTrue(UiText.PinLabel(0, 0).Contains("家长 PIN"), "默认标签");
        Assert.IsTrue(UiText.PinLabel(0, 2).Contains("2") && UiText.PinLabel(0, 2).Contains("还可尝试"), "失败次数提示");
        Assert.IsTrue(UiText.PinLabel(9, 0).Contains("9") && UiText.PinLabel(9, 0).Contains("次数过多"), "冷却提示");
    }

    [Test]
    public void PinCooldownTakesPriorityOverFailCount()
    {
        string s = UiText.PinLabel(7, 2);
        Assert.IsTrue(s.Contains("次数过多") && s.Contains("7"), "冷却中优先显示冷却");
        Assert.IsFalse(s.Contains("还可尝试"), "冷却时不应同时显示失败次数");
    }

    [Test]
    public void PinLabelIgnoresNonPositiveValues()
    {
        Assert.AreEqual(UiText.PinLabel(0, 0), UiText.PinLabel(-1, -5), "负值等同无状态");
    }

    [Test]
    public void PinLabelIsLocalizedImmediately()
    {
        string zh = WithLang("zh", delegate { return UiText.PinLabel(0, 3); });
        string en = WithLang("en", delegate { return UiText.PinLabel(0, 3); });
        Assert.IsTrue(zh.Contains("还可尝试") && zh.Contains("3"), "中文失败提示");
        Assert.IsTrue(en.Contains("3") && en.Contains("attempt"), "英文失败提示");
        Assert.IsFalse(en.Contains("还可尝试"), "不应残留中文");
    }
}
