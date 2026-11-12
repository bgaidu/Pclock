using System;
using System.Collections.Generic;
using System.Linq;
using PCLock;
using TUnit;

/// <summary>
/// 单元测试集合。运行：见 tests/run.bat（或手动 csc 编译后执行）。
/// 仅覆盖确定性的纯逻辑（窗口解析、题库约束）；注册表/UI/锁屏需在 Windows 上手动验证。
/// </summary>

public class WindowTests
{
    [Test]
    public void EmptyMeansAlwaysOpen()
    {
        Assert.IsTrue(Store.IsWithinWindow("", new TimeSpan(3, 0, 0)));
        Assert.IsTrue(Store.IsWithinWindow("   ", new TimeSpan(0, 0, 0)));
    }

    [Test]
    public void OutsideGapIsClosed()
    {
        Assert.IsFalse(Store.IsWithinWindow("08:00-12:00;14:00-21:00", new TimeSpan(13, 0, 0)));
    }

    [Test]
    public void InsideFirstSegment()
    {
        Assert.IsTrue(Store.IsWithinWindow("08:00-12:00;14:00-21:00", new TimeSpan(10, 0, 0)));
    }

    [Test]
    public void InsideSecondSegment()
    {
        Assert.IsTrue(Store.IsWithinWindow("08:00-12:00;14:00-21:00", new TimeSpan(15, 30, 0)));
    }

    [Test]
    public void BoundaryInclusive()
    {
        Assert.IsTrue(Store.IsWithinWindow("08:00-12:00;14:00-21:00", new TimeSpan(21, 0, 0)));
        Assert.IsTrue(Store.IsWithinWindow("08:00-12:00;14:00-21:00", new TimeSpan(8, 0, 0)));
    }

    [Test]
    public void JustAfterBoundaryIsClosed()
    {
        Assert.IsFalse(Store.IsWithinWindow("08:00-12:00;14:00-21:00", new TimeSpan(21, 1, 0)));
        Assert.IsFalse(Store.IsWithinWindow("08:00-12:00;14:00-21:00", new TimeSpan(7, 59, 0)));
    }

    [Test]
    public void CommaSeparatorAlsoWorks()
    {
        Assert.IsTrue(Store.IsWithinWindow("08:00-12:00,14:00-21:00", new TimeSpan(15, 0, 0)));
    }

    [Test]
    public void InvalidSegmentIgnored()
    {
        Assert.IsFalse(Store.IsWithinWindow("25:00-26:00", new TimeSpan(3, 0, 0)));
        Assert.IsFalse(Store.IsWithinWindow("nocolon", new TimeSpan(3, 0, 0)));
    }

    // ===== 跨午夜时段（起始大于结束，如 22:00-06:00）按环绕判断 =====
    // 修复前 a<=now<=b 恒为 false，家长填跨午夜时段会导致全天硬锁。
    [Test]
    public void CrossMidnightInsideNight()
    {
        Assert.IsTrue(Store.IsWithinWindow("22:00-06:00", new TimeSpan(23, 0, 0)));
        Assert.IsTrue(Store.IsWithinWindow("22:00-06:00", new TimeSpan(3, 0, 0)));
    }

    [Test]
    public void CrossMidnightOutsideDay()
    {
        Assert.IsFalse(Store.IsWithinWindow("22:00-06:00", new TimeSpan(12, 0, 0)));
        Assert.IsFalse(Store.IsWithinWindow("22:00-06:00", new TimeSpan(6, 30, 0)));
        Assert.IsFalse(Store.IsWithinWindow("22:00-06:00", new TimeSpan(21, 59, 0)));
    }

    [Test]
    public void CrossMidnightBoundaryInclusive()
    {
        Assert.IsTrue(Store.IsWithinWindow("22:00-06:00", new TimeSpan(22, 0, 0)));
        Assert.IsTrue(Store.IsWithinWindow("22:00-06:00", new TimeSpan(6, 0, 0)));
        Assert.IsFalse(Store.IsWithinWindow("22:00-06:00", new TimeSpan(6, 1, 0)));
    }
}

public class QuestionTests
{
    static int CountSubject(List<Question> qs, Subject s)
    {
        int c = 0;
        foreach (Question q in qs) if (q.Subject == s) c++;
        return c;
    }

    [Test]
    public void MixedBatchHasTen()
    {
        List<Question> qs = QuestionBank.GetBatch(3, Subject.Mixed, 10);
        Assert.AreEqual(10, qs.Count);
    }

    [Test]
    public void MixedEachSubjectAtMostFour()
    {
        for (int g = 1; g <= 6; g++)
        {
            List<Question> qs = QuestionBank.GetBatch(g, Subject.Mixed, 10);
            Assert.LessOrEqual(CountSubject(qs, Subject.Chinese), 4, "grade " + g + " 语文超 4");
            Assert.LessOrEqual(CountSubject(qs, Subject.Math), 4, "grade " + g + " 数学超 4");
            Assert.LessOrEqual(CountSubject(qs, Subject.English), 4, "grade " + g + " 英语超 4");
        }
    }

    [Test]
    public void TwoSubjectsEachAtLeastThree()
    {
        // 两科组合索引：4=语文+数学, 5=语文+英语, 6=数学+英语
        int[] combos = new int[] { 4, 5, 6 };
        foreach (int c in combos)
        {
            List<Question> qs = QuestionBank.GetBatch(3, (Subject)c, 10);
            Subject a = (c == 4) ? Subject.Chinese : (c == 5) ? Subject.Chinese : Subject.Math;
            Subject b = (c == 4) ? Subject.Math : (c == 5) ? Subject.English : Subject.English;
            Assert.GreaterOrEqual(CountSubject(qs, a), 3, "combo " + c);
            Assert.GreaterOrEqual(CountSubject(qs, b), 3, "combo " + c);
            Assert.AreEqual(10, qs.Count);
        }
    }

    [Test]
    public void SingleSubjectAllSame()
    {
        foreach (Subject s in new Subject[] { Subject.Chinese, Subject.Math, Subject.English })
        {
            List<Question> qs = QuestionBank.GetBatch(3, s, 10);
            Assert.AreEqual(10, CountSubject(qs, s));
        }
    }

    // ===== 以下覆盖"多选年级 + 多选科目"版 API =====
    // 这是 UI 真正走的路径（LockForm.StartBatch → GetBatch(int[], Subject[], int)）。
    // 该重载内部是"科目轮转均派"，没有单科数量硬上限，因此断言的是均派性质
    // （单科 <= ceil(总数/科目数) 且每科都出现），而不是旧 API 那个固定的 4。

    static List<Question> MultiSelect(int total)
    {
        int[] grades = new int[] { 1, 2, 3, 4, 5, 6 };
        Subject[] subs = new Subject[] { Subject.Chinese, Subject.Math, Subject.English };
        return QuestionBank.GetBatch(grades, subs, total);
    }

    [Test]
    public void MultiSelectBatchHasRequestedCount()
    {
        Assert.AreEqual(Constants.TotalQuestions,
            MultiSelect(Constants.TotalQuestions).Count, "multi-select count");
    }

    [Test]
    public void MultiSelectRotatesEvenly()
    {
        int total = Constants.TotalQuestions;
        int cap = (int)Math.Ceiling(total / 3.0);   // 三科均派时单科上限
        List<Question> qs = MultiSelect(total);
        Assert.AreEqual(total, qs.Count, "multi-select total");
        foreach (Subject s in new Subject[] { Subject.Chinese, Subject.Math, Subject.English })
        {
            int c = CountSubject(qs, s);
            Assert.LessOrEqual(c, cap, "multi-select " + s + " 超过均派上限");
            Assert.GreaterOrEqual(c, 1, "multi-select " + s + " 缺席");
        }
    }

    [Test]
    public void MultiSelectSingleSubjectIsPure()
    {
        int[] grades = new int[] { 3 };
        foreach (Subject s in new Subject[] { Subject.Chinese, Subject.Math, Subject.English })
        {
            List<Question> qs = QuestionBank.GetBatch(grades, new Subject[] { s }, 10);
            Assert.AreEqual(10, CountSubject(qs, s), "multi-select single " + s);
        }
    }
}

/// <summary>
/// 题库边界测试：小型/退化/缺年级词库下的出题健壮性。
/// 通过反射注入私有静态词库（chinese/english），并在 finally 中恢复，避免影响其他测试。
/// </summary>
public class QuestionBankEdgeTests
{
    static readonly System.Reflection.FieldInfo ZhField =
        typeof(QuestionBank).GetField("chinese",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    static readonly System.Reflection.FieldInfo EnField =
        typeof(QuestionBank).GetField("english",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

    static List<WordEntry> OrigZh() { return (List<WordEntry>)ZhField.GetValue(null); }
    static List<EnglishEntry> OrigEn() { return (List<EnglishEntry>)EnField.GetValue(null); }

    static void WithBanks(List<WordEntry> zh, List<EnglishEntry> en, Action body)
    {
        List<WordEntry> oz = OrigZh();
        List<EnglishEntry> oe = OrigEn();
        try
        {
            ZhField.SetValue(null, zh);
            EnField.SetValue(null, en);
            body();
        }
        finally
        {
            ZhField.SetValue(null, oz);
            EnField.SetValue(null, oe);
        }
    }

    /// <summary>校验单题合法：选项 2-4 个、互不重复、无占位符（不再出现"拼音?"）、CorrectIndex 有效</summary>
    static void AssertQuestionValid(Question q, string ctx)
    {
        Assert.IsTrue(q != null, ctx + ": 题目为空");
        Assert.IsTrue(!string.IsNullOrEmpty(q.Prompt), ctx + ": 题干为空");
        if (q.Type == QType.MathCalc)
        {
            Assert.IsTrue(q.Options == null, ctx + ": 口算题不应有选项");
            Assert.GreaterOrEqual(q.Answer, 0, ctx + ": 口算题答案应 >= 0");
            return;
        }
        Assert.IsTrue(q.Options != null, ctx + ": 选择题缺少选项");
        Assert.GreaterOrEqual(q.Options.Length, 2, ctx + ": 选项少于 2 个（占位符或唯一选项）");
        Assert.LessOrEqual(q.Options.Length, 4, ctx + ": 选项超过 4 个");
        for (int i = 0; i < q.Options.Length; i++)
        {
            Assert.IsTrue(!string.IsNullOrEmpty(q.Options[i]), ctx + ": 存在空选项");
            Assert.IsTrue(q.Options[i].IndexOf('?') < 0, ctx + ": 存在占位符选项 [" + q.Options[i] + "]");
            for (int j = i + 1; j < q.Options.Length; j++)
                Assert.IsTrue(q.Options[i] != q.Options[j], ctx + ": 选项重复 [" + q.Options[i] + "]");
        }
        Assert.IsTrue(q.CorrectIndex >= 0 && q.CorrectIndex < q.Options.Length, ctx + ": CorrectIndex 越界");
    }

    static void AssertBatchValid(List<Question> qs, int expected, string ctx)
    {
        Assert.AreEqual(expected, qs.Count, ctx + ": 题量");
        for (int i = 0; i < qs.Count; i++) AssertQuestionValid(qs[i], ctx);
    }

    static List<WordEntry> TinyChinese()
    {
        List<WordEntry> zh = new List<WordEntry>();
        zh.Add(new WordEntry { Grade = 3, Word = "苹果", Pinyin = "ping guo", Meaning = "水果" });
        zh.Add(new WordEntry { Grade = 3, Word = "香蕉", Pinyin = "xiang jiao", Meaning = "水果" });
        return zh;
    }

    static List<EnglishEntry> TinyEnglish()
    {
        List<EnglishEntry> en = new List<EnglishEntry>();
        en.Add(new EnglishEntry { Grade = 3, En = "apple", Zh = "苹果" });
        en.Add(new EnglishEntry { Grade = 3, En = "banana", Zh = "香蕉" });
        return en;
    }

    // 小型词库：每科只有 2 个词条，干扰项凑不满 4 个。
    // 修复前会插入 "拼音?" 占位符充当选项；修复后应为 2 个真实选项，无占位符。
    [Test]
    public void TinyBankNoPlaceholderOptions()
    {
        WithBanks(TinyChinese(), TinyEnglish(), delegate
        {
            for (int round = 0; round < 20; round++)
            {
                List<Question> qs = QuestionBank.GetBatch(new int[] { 3 },
                    new Subject[] { Subject.Chinese, Subject.English }, 10);
                AssertBatchValid(qs, 10, "小型词库 round " + round);
            }
        });
    }

    // 极端退化：词库只有 1 个语文词条，凑不出任何干扰项。
    // 应退出口算题（而非唯一选项/占位符），且题量仍为 10。
    [Test]
    public void DegenerateBankFallsBackToMath()
    {
        List<WordEntry> zh = new List<WordEntry>();
        zh.Add(new WordEntry { Grade = 3, Word = "苹果", Pinyin = "ping guo", Meaning = "水果" });
        WithBanks(zh, new List<EnglishEntry>(), delegate
        {
            List<Question> qs = QuestionBank.GetBatch(new int[] { 3 }, new Subject[] { Subject.Chinese }, 10);
            AssertBatchValid(qs, 10, "退化词库");
        });
    }

    // 选中年级在词库中不存在（外部词库只配了其他年级）：不应抛异常，题量仍为 10。
    // 修复前 BuildChinesePool 直接返回空列表，会导致整批只有少量题目甚至空批。
    [Test]
    public void MissingGradeDoesNotCrash()
    {
        List<WordEntry> zh = new List<WordEntry>();
        zh.Add(new WordEntry { Grade = 5, Word = "苹果", Pinyin = "ping guo", Meaning = "水果" });
        zh.Add(new WordEntry { Grade = 5, Word = "香蕉", Pinyin = "xiang jiao", Meaning = "水果" });
        WithBanks(zh, new List<EnglishEntry>(), delegate
        {
            List<Question> qs = QuestionBank.GetBatch(new int[] { 3 }, new Subject[] { Subject.Chinese }, 10);
            AssertBatchValid(qs, 10, "缺年级");
        });
    }

    // 内置词库（未注入）全年级全口径：选项必须干净（无占位符、无重复、CorrectIndex 有效）
    [Test]
    public void BuiltInBankOptionsClean()
    {
        for (int g = 1; g <= 6; g++)
        {
            foreach (Subject s in new Subject[] { Subject.Chinese, Subject.Math, Subject.English })
            {
                List<Question> qs = QuestionBank.GetBatch(g, s, 10);
                AssertBatchValid(qs, 10, "内置词库 " + g + " " + s);
            }
        }
    }
}

public class Program
{
    public static int Main()
    {
        int fails = TUnit.Runner.Run(new Type[] {
            typeof(WindowTests), typeof(QuestionTests), typeof(QuestionBankEdgeTests),
            typeof(HashTests), typeof(MathAnswerTests), typeof(OfflineDeductTests),
            typeof(PolicyTests), typeof(MutationTests), typeof(BankIoTests), typeof(QuestionPolicyTests),
            typeof(LearningTests), typeof(ConfigIoTests), typeof(NewQuestionTypeTests),
            typeof(Batch4Tests), typeof(Batch5Tests), typeof(Batch6Tests) });
        return fails == 0 ? 0 : 1;
    }
}
