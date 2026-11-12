using System;
using PCLock;
using TUnit;

/// <summary>第八批：多语言即时刷新（语言事件、幂等切换、词典判定）的纯逻辑回归。</summary>
public class Batch7Tests
{
    static string saved;

    static void Save() { saved = Strings.Lang; }

    static void Restore()
    {
        Strings.Lang = saved;
        saved = null;
    }

    [Test]
    public void SetLanguageChangesLangAndFiresEvent()
    {
        Save();
        int fired = 0;
        Action h = delegate { fired++; };
        Strings.LanguageChanged += h;
        try
        {
            Strings.Lang = "zh";                       // 先归零到中文
            fired = 0;
            Assert.IsTrue(Strings.SetLanguage("en"), "zh -> en 应报告变化");
            Assert.AreEqual("en", Strings.Lang, "Lang 应切换");
            Assert.IsTrue(Strings.IsEnglish, "IsEnglish 应为 true");
            Assert.AreEqual(1, fired, "应恰好触发一次事件");
        }
        finally { Strings.LanguageChanged -= h; Restore(); }
    }

    [Test]
    public void RepeatedSetLanguageIsIdempotent()
    {
        Save();
        int fired = 0;
        Action h = delegate { fired++; };
        Strings.LanguageChanged += h;
        try
        {
            Strings.Lang = "zh";
            fired = 0;
            Strings.SetLanguage("en");
            Assert.IsFalse(Strings.SetLanguage("en"), "重复设置同值不应报告变化");
            Assert.AreEqual(1, fired, "重复设置不应再次触发事件");
        }
        finally { Strings.LanguageChanged -= h; Restore(); }
    }

    [Test]
    public void LangSetterGoesThroughSetLanguage()
    {
        Save();
        int fired = 0;
        Action h = delegate { fired++; };
        Strings.LanguageChanged += h;
        try
        {
            Strings.Lang = "en";
            fired = 0;
            Strings.Lang = "zh";                       // 旧代码直接赋值也要能触发事件
            Assert.AreEqual("zh", Strings.Lang, "应切回中文");
            Assert.AreEqual(1, fired, "直接赋值同样要通知订阅者");
        }
        finally { Strings.LanguageChanged -= h; Restore(); }
    }

    [Test]
    public void UnknownLanguageFallsBackToZh()
    {
        Save();
        try
        {
            Strings.SetLanguage("fr");
            Assert.AreEqual("zh", Strings.Lang, "非 zh/en 一律回落中文");
            Assert.IsFalse(Strings.IsEnglish, "回落中文后 IsEnglish 为 false");
        }
        finally { Restore(); }
    }

    [Test]
    public void GetAndFmtSwitchImmediately()
    {
        Save();
        try
        {
            Strings.SetLanguage("zh");
            Assert.AreEqual("电脑锁", Strings.Get("电脑锁"), "中文模式返回原文");
            Strings.SetLanguage("en");
            Assert.AreEqual("PCLock", Strings.Get("电脑锁"), "切换后立刻返回英文");
            Assert.AreEqual("Used today: 12 min 30 s",
                Strings.Fmt("今日已用：{0} 分 {1} 秒", 12, 30), "模板整句替换，不中英混排");
            Strings.SetLanguage("zh");
            Assert.AreEqual("今日已用：12 分 30 秒",
                Strings.Fmt("今日已用：{0} 分 {1} 秒", 12, 30), "切回中文立刻恢复");
        }
        finally { Restore(); }
    }

    [Test]
    public void ContainsOnlyReportsDictionaryKeys()
    {
        Assert.IsTrue(Strings.Contains("电脑锁"), "已收录文案");
        Assert.IsTrue(Strings.Contains("今日已用：{0} 分 {1} 秒"), "占位模板也是 key");
        Assert.IsFalse(Strings.Contains(""), "空串");
        Assert.IsFalse(Strings.Contains(null), "null");
        Assert.IsFalse(Strings.Contains("这是一段没收录的运行期动态文案 12 分"), "动态文案不应被缓存翻译");
    }

    [Test]
    public void UnsubscribedHandlerStopsFiring()
    {
        Save();
        int fired = 0;
        Action h = delegate { fired++; };
        Strings.LanguageChanged += h;
        try
        {
            Strings.Lang = "zh";
            Strings.LanguageChanged -= h;
            fired = 0;
            Strings.SetLanguage("en");
            Assert.AreEqual(0, fired, "取消订阅后不应再收到通知");
        }
        finally { Strings.LanguageChanged -= h; Restore(); }
    }
}
