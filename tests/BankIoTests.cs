using System;
using System.Collections.Generic;
using PCLock;
using TUnit;

/// <summary>P1 题库 JSON / CSV 与使用记录纯函数单测</summary>
public class BankIoTests
{
    [Test]
    public void MiniJsonParsesScalarsAndContainers()
    {
        Dictionary<string, object> d = MiniJson.Parse(
            "{\"n\":12,\"f\":1.5,\"t\":true,\"z\":null,\"s\":\"a\\\"b\\n\\u4e2d\",\"a\":[1,\"x\",false]}")
            as Dictionary<string, object>;
        Assert.IsTrue(d != null);
        Assert.AreEqual(12.0, (double)d["n"]);
        Assert.AreEqual(1.5, (double)d["f"]);
        Assert.AreEqual(true, (bool)d["t"]);
        Assert.IsTrue(d["z"] == null);
        Assert.AreEqual("a\"b\n中", (string)d["s"]);
        List<object> a = d["a"] as List<object>;
        Assert.AreEqual(3, a.Count);
        Assert.AreEqual("x", (string)a[1]);
        Assert.AreEqual(false, (bool)a[2]);
    }

    [Test]
    public void MiniJsonRejectsMalformedInput()
    {
        Assert.Throws<FormatException>(delegate { MiniJson.Parse("{\"a\":}"); });
        Assert.Throws<FormatException>(delegate { MiniJson.Parse("[1,]"); });
        Assert.Throws<FormatException>(delegate { MiniJson.Parse("{\"a\":1} trailing"); });
        Assert.Throws<FormatException>(delegate { MiniJson.Parse("{\"a\":\"unterminated}"); });
    }

    [Test]
    public void ChineseJsonRoundTrip()
    {
        string json = "{\"items\":[" +
            "{\"grade\":1,\"word\":\"太阳\",\"pinyin\":\"tai yang\",\"meaning\":\"恒星\"}," +
            "{\"grade\":3,\"word\":\"勇敢\",\"pinyin\":\"yong gan\",\"meaning\":\"不怕危险\"}," +
            "{\"grade\":9,\"word\":\"越界\",\"pinyin\":\"yue jie\",\"meaning\":\"\"}," +
            "{\"grade\":1,\"word\":\"缺拼音\",\"pinyin\":\"\",\"meaning\":\"\"}," +
            "{\"grade\":1,\"word\":\"太阳\",\"pinyin\":\"tai yang\",\"meaning\":\"重复\"}]}";
        List<WordEntry> items = BankIo.ImportChinese(json);
        Assert.AreEqual(2, items.Count);               // 越界 / 缺字段 / 重复全部剔除
        Assert.AreEqual("太阳", items[0].Word);
        Assert.AreEqual("恒星", items[0].Meaning);
        Assert.IsTrue(BankIo.ValidateChinese(items) == null);

        string exported = BankIo.ExportChinese(items);
        List<WordEntry> again = BankIo.ImportChinese(exported);
        Assert.AreEqual(2, again.Count);
        Assert.AreEqual("yong gan", again[1].Pinyin);
    }

    [Test]
    public void EnglishJsonRoundTrip()
    {
        string json = "{\"items\":[{\"grade\":2,\"en\":\"apple\",\"zh\":\"苹果\"}," +
            "{\"grade\":2,\"en\":\"apple\",\"zh\":\"苹果\"}," +
            "{\"grade\":7,\"en\":\"bad\",\"zh\":\"越界\"}]}";
        List<EnglishEntry> items = BankIo.ImportEnglish(json);
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual("苹果", items[0].Zh);
        Assert.AreEqual(1, BankIo.ImportEnglish(BankIo.ExportEnglish(items)).Count);
    }

    [Test]
    public void JsonEscapeRoundTrip()
    {
        List<WordEntry> items = new List<WordEntry>();
        items.Add(new WordEntry { Grade = 2, Word = "引号\"反斜杠\\", Pinyin = "yin hao",
            Meaning = "换行\n制表\t" });
        string exported = BankIo.ExportChinese(items);
        List<WordEntry> back = BankIo.ImportChinese(exported);
        Assert.AreEqual(1, back.Count);
        Assert.AreEqual("引号\"反斜杠\\", back[0].Word);
        Assert.AreEqual("换行\n制表\t", back[0].Meaning);
    }

    [Test]
    public void CombinedExportCanBeImportedBack()
    {
        // 高级设置「导出内置题库 JSON」使用的合并格式，两个分库都要能读回
        string json = "{\"chinese\":" + BankIo.ExportChinese(ChineseData.All) +
            ",\"english\":" + BankIo.ExportEnglish(EnglishData.All) + "}";
        Assert.IsTrue(BankIo.ImportChinese(json).Count > 0);
        Assert.IsTrue(BankIo.ImportEnglish(json).Count > 0);
    }

    [Test]
    public void ValidateChineseCatchesBadBanks()
    {
        Assert.IsTrue(BankIo.ValidateChinese(null) != null);
        Assert.IsTrue(BankIo.ValidateChinese(new List<WordEntry>()) != null);
        List<WordEntry> bad = new List<WordEntry>();
        bad.Add(new WordEntry { Grade = 7, Word = "越界", Pinyin = "yue jie" });
        Assert.IsTrue(BankIo.ValidateChinese(bad) != null);
        List<WordEntry> placeholder = new List<WordEntry>();
        placeholder.Add(new WordEntry { Grade = 1, Word = "占位", Pinyin = "?" });
        Assert.IsTrue(BankIo.ValidateChinese(placeholder) != null);
    }

    [Test]
    public void UsageCsvCoversSevenDays()
    {
        DateTime today = new DateTime(2026, 11, 11);
        DateTime[] days = UsageCsv.LastNDays(today, 7);
        Assert.AreEqual(7, days.Length);
        Assert.AreEqual("2026-11-05", days[0].ToString("yyyy-MM-dd"));
        Assert.AreEqual("2026-11-11", days[6].ToString("yyyy-MM-dd"));

        int[] seconds = new int[] { 60, 0, 120, 0, 0, 0, 3600 };
        string csv = UsageCsv.Build(today, seconds);
        Assert.IsTrue(csv.StartsWith("date,seconds,minutes"));
        Assert.IsTrue(csv.Contains("2026-11-05,60,1"));
        Assert.IsTrue(csv.Contains("2026-11-11,3600,60"));
    }
}
