using System;
using System.Collections.Generic;
using PCLock;
using TUnit;

/// <summary>本批新增策略的单测：间隔重复、统计、难度倾向、下次可用、格子、配置与题型。</summary>
public class LearningTests
{
    [Test]
    public void Sm2IntervalsGrowAndResetOnWrong()
    {
        long t0 = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc).Ticks;
        ReviewState s = SpacedRepetition.Initial();
        Assert.AreEqual(2.5, s.Ease);
        s = SpacedRepetition.Review(s, true, t0);
        Assert.AreEqual(1, s.IntervalDays);
        Assert.IsTrue(s.DueUtcTicks > t0);
        s = SpacedRepetition.Review(s, true, t0);
        Assert.AreEqual(6, s.IntervalDays);
        s = SpacedRepetition.Review(s, true, t0);
        Assert.IsTrue(s.IntervalDays >= 6);
        ReviewState bad = SpacedRepetition.Review(s, false, t0);
        Assert.AreEqual(0, bad.Repetitions);
        Assert.IsTrue(bad.DueUtcTicks <= t0 + 10 * TimeSpan.TicksPerMinute);
        Assert.IsTrue(bad.Ease < s.Ease);
    }

    [Test]
    public void Sm2SerializeRoundtrip()
    {
        long t0 = 638000000000000000L;
        ReviewState s = SpacedRepetition.Review(SpacedRepetition.Initial(), true, t0);
        ReviewState back = SpacedRepetition.Parse(SpacedRepetition.Serialize(s));
        Assert.AreEqual(s.Ease, back.Ease);
        Assert.AreEqual(s.IntervalDays, back.IntervalDays);
        Assert.AreEqual(s.DueUtcTicks, back.DueUtcTicks);
        Assert.AreEqual(s.Repetitions, back.Repetitions);
        Assert.IsTrue(SpacedRepetition.IsDue(SpacedRepetition.Initial(), t0));
    }

    [Test]
    public void QuestionStatsTrackAccuracy()
    {
        Question q = new Question { Prompt = "2+3=?", Type = QType.MathCalc, Answer = 5 };
        string k1 = QuestionStats.KeyOf(q), k2 = QuestionStats.KeyOf(q);
        Assert.AreEqual(k1, k2);
        string v = "";
        v = QuestionStats.Update(v, true);
        v = QuestionStats.Update(v, false);
        Assert.AreEqual(50, QuestionStats.Accuracy(v));
        Assert.AreEqual(-1, QuestionStats.Accuracy(""));
    }

    [Test]
    public void WeakestPicksLowestAccuracy()
    {
        string[] keys = new string[] { "a", "b", "c" };
        string[] vals = new string[] { "5|5|5", "5|5|1", "1|1|0" };
        Assert.AreEqual("b", QuestionStats.Weakest(keys, vals, 3)); // c 作答 1 次不达阈值
    }

    [Test]
    public void DifficultyBiasUses80Percent()
    {
        int[] d = new int[] { 1, 2, 3, 2, 1 };
        Random rnd = new Random(7);
        int hits = 0;
        for (int i = 0; i < 200; i++)
            if (d[DifficultyBias.PickIndex(d, 2, rnd, 80)] == 2) hits++;
        // 偏好难度占 2/5，80% 倾向时命中率 ≈88%，远高于纯随机 40%
        Assert.GreaterOrEqual(hits, 100);
        Assert.AreEqual(-1, DifficultyBias.PickIndex(new int[0], 2, rnd, 80));
    }

    [Test]
    public void NextWindowFindsNextAllowedSlot()
    {
        DateTime now = new DateTime(2026, 11, 11, 13, 0, 0); // 周三
        List<WindowRule> rules = WindowRules.Parse("1-5|08:00-12:00");
        int mins = NextWindow.MinutesUntil(rules, now);
        Assert.IsTrue(mins > 0);
        Assert.IsTrue(mins <= 7 * 24 * 60);
        Assert.IsTrue(NextWindow.Format(90).Contains("小时"));
        Assert.AreEqual(-1, NextWindow.MinutesUntil(new List<WindowRule>(), now));
    }
}

public class ConfigIoTests
{
    [Test]
    public void GridToRuleProducesParsableSegments()
    {
        bool[,] g = new bool[7, 24];
        for (int h = 8; h < 12; h++) { g[0, h] = true; g[2, h] = true; }
        string rule = ConfigIo.ScheduleGridToRule(g, true);
        Assert.IsTrue(rule.Length > 0);
        Assert.IsTrue(rule.Contains("@quiz"));
        List<WindowRule> parsed = WindowRules.Parse(rule);
        Assert.AreEqual(2, parsed.Count);
        Assert.IsTrue(parsed[0].QuizOnly);
        Assert.IsTrue(WindowRules.Evaluate(rule, new DateTime(2026, 11, 9, 9, 0, 0)).FreeUse ||
            WindowRules.Evaluate(rule, new DateTime(2026, 11, 9, 9, 0, 0)).QuizControlled);
    }

    [Test]
    public void GridToRuleRejectsInvalidGrid()
    {
        Assert.AreEqual("", ConfigIo.ScheduleGridToRule(null, false));
        Assert.AreEqual("", ConfigIo.ScheduleGridToRule(new bool[3, 24], false));
    }

    [Test]
    public void CrashSummaryCountsKnownKinds()
    {
        string log = "UI THREAD EXCEPTION:\n" +
                     "System.Exception\nUI THREAD EXCEPTION:\n" +
                     "Store.GetStr(x) 失败\n";
        string[] r = ConfigIo.CrashSummary(log);
        Assert.IsTrue(r.Length >= 2);
        Assert.IsTrue(string.Join(";", r).Contains("UI THREAD EXCEPTION × 2"));
        Assert.IsTrue(ConfigIo.CrashSummary("")[0].Contains("无异常"));
    }

    [Test]
    public void ConfigJsonRejectsForeignRoot()
    {
        Assert.IsTrue(ConfigIo.ImportConfig("{\"app\":\"other\",\"values\":{}}") != null);
        Assert.IsTrue(ConfigIo.ImportConfig("not json") != null);
    }

    [Test]
    public void ReviewBankExportIsParseableJson()
    {
        Question q = new Question { Prompt = "1+1=?", Type = QType.MathCalc,
            Grade = 1, Subject = Subject.Math, Answer = 2, CorrectIndex = -1 };
        List<ReviewItem> items = new List<ReviewItem>();
        items.Add(new ReviewItem { Key = "abc", Q = q, DueUtcTicks = 123 });
        string json = ReviewBank.Export(items);
        Dictionary<string, object> d = MiniJson.Parse(json) as Dictionary<string, object>;
        Assert.IsTrue(d != null);
        Assert.IsTrue(d.ContainsKey("items"));
        Assert.IsTrue(json.Contains("1+1=?"));
    }

    [Test]
    public void StringsFallsBackToChinese()
    {
        Strings.Lang = "zh";
        Assert.AreEqual("家长入口", Strings.Get("家长入口"));
        Strings.Lang = "en";
        Assert.AreEqual("Parent entry", Strings.Get("家长入口"));
        Assert.AreEqual("未收录", Strings.Get("未收录"));
        Strings.Lang = "zh";
    }
}

public class NewQuestionTypeTests
{
    [Test]
    public void EnrichedBatchHasValidOptionsAndExactCount()
    {
        List<Question> qs = QuestionBank.GetBatchEx(new int[] { 1, 3, 5 },
            new Subject[] { Subject.Chinese, Subject.Math, Subject.English }, 10,
            2, 0, "", true, true, true);
        Assert.AreEqual(10, qs.Count);
        foreach (Question q in qs)
        {
            Assert.IsTrue(q.Prompt != null && q.Prompt.Length > 0);
            if (q.Type == QType.MathCalc) Assert.IsTrue(q.Options == null);
            else
            {
                Assert.IsTrue(q.Options != null && q.Options.Length >= 2 && q.Options.Length <= 4);
                Assert.IsTrue(q.CorrectIndex >= 0 && q.CorrectIndex < q.Options.Length);
            }
        }
    }

    [Test]
    public void NewTypesActuallyAppear()
    {
        bool mathChoice = false, chineseWord = false, trueFalse = false;
        for (int round = 0; round < 8; round++)
        {
            List<Question> qs = QuestionBank.GetBatchEx(new int[] { 3 },
                new Subject[] { Subject.Chinese, Subject.Math }, 12,
                0, 0, "", true, true, true);
            foreach (Question q in qs)
            {
                if (q.Type == QType.MathChoice) mathChoice = true;
                if (q.Type == QType.ChineseWord) chineseWord = true;
                if (q.Type == QType.TrueFalse) trueFalse = true;
            }
        }
        Assert.IsTrue(mathChoice && chineseWord && trueFalse);
    }
}
