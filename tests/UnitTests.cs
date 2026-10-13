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

public class Program
{
    public static int Main()
    {
        int fails = TUnit.Runner.Run(new Type[] { typeof(WindowTests), typeof(QuestionTests) });
        return fails == 0 ? 0 : 1;
    }
}
