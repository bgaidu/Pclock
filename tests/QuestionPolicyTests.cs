using System;
using System.Collections.Generic;
using PCLock;
using TUnit;

/// <summary>P1 题库治理门禁：难度分档、年级防超纲、批次质量</summary>
public class QuestionPolicyTests
{
    [Test]
    public void DifficultyIsBucketedByGrade()
    {
        Assert.AreEqual(1, QuestionPolicy.DifficultyOf(new Question { Grade = 1, Type = QType.ChinesePinyin }));
        Assert.AreEqual(2, QuestionPolicy.DifficultyOf(new Question { Grade = 4, Type = QType.ChinesePinyin }));
        Assert.AreEqual(3, QuestionPolicy.DifficultyOf(new Question { Grade = 6, Type = QType.ChinesePinyin }));
        Assert.AreEqual(2, QuestionPolicy.DifficultyOf(new Question { Grade = 6, Type = QType.MathCalc }));
    }

    [Test]
    public void GradeCapRejectsOutOfScopeQuestions()
    {
        List<Question> qs = new List<Question>();
        qs.Add(new Question { Grade = 3, Type = QType.MathCalc });
        qs.Add(new Question { Grade = 5, Type = QType.MathCalc });
        Assert.IsTrue(QuestionPolicy.IsGradeCapped(qs, new int[] { 3, 5 }));
        Assert.IsFalse(QuestionPolicy.IsGradeCapped(qs, new int[] { 3 }));
        Assert.IsFalse(QuestionPolicy.IsGradeCapped(null, new int[] { 3 }));
    }

    [Test]
    public void ValidateBatchAcceptsCleanBatch()
    {
        List<Question> qs = new List<Question>();
        qs.Add(new Question { Grade = 3, Type = QType.MathCalc, Prompt = "1+1=?", Answer = 2 });
        qs.Add(new Question { Grade = 3, Type = QType.ChinesePinyin, Prompt = "太阳",
            Options = new string[] { "tai yang", "yue liang", "xing xing" }, CorrectIndex = 0 });
        Assert.IsTrue(QuestionPolicy.ValidateBatch(qs, 2) == null);
    }

    [Test]
    public void ValidateBatchRejectsBadQuestions()
    {
        List<Question> dup = new List<Question>();
        dup.Add(new Question { Grade = 1, Type = QType.MathCalc, Prompt = "1+1=?", Answer = 2 });
        dup.Add(new Question { Grade = 1, Type = QType.MathCalc, Prompt = "1+1=?", Answer = 2 });
        Assert.IsTrue(QuestionPolicy.ValidateBatch(dup, 2) != null);
        Assert.IsTrue(QuestionPolicy.ValidateBatch(dup, 3) != null);

        List<Question> placeholder = new List<Question>();
        placeholder.Add(new Question { Grade = 1, Type = QType.ChinesePinyin, Prompt = "太阳",
            Options = new string[] { "?", "yue liang" }, CorrectIndex = 0 });
        Assert.IsTrue(QuestionPolicy.ValidateBatch(placeholder, 1) != null);

        List<Question> badIndex = new List<Question>();
        badIndex.Add(new Question { Grade = 1, Type = QType.ChinesePinyin, Prompt = "太阳",
            Options = new string[] { "a", "b" }, CorrectIndex = 7 });
        Assert.IsTrue(QuestionPolicy.ValidateBatch(badIndex, 1) != null);
    }

    [Test]
    public void BuiltInBankPassesQualityGate()
    {
        for (int g = 1; g <= 6; g++)
        {
            List<Question> qs = QuestionBank.GetBatch(g, Subject.Chinese, 10);
            string err = QuestionPolicy.ValidateBatch(qs, 10);
            Assert.IsTrue(err == null, "语文 " + g + " 年级门禁：" + err);
            Assert.IsTrue(QuestionPolicy.IsGradeCapped(qs, new int[] { g }));

            List<Question> qe = QuestionBank.GetBatch(g, Subject.English, 10);
            string erre = QuestionPolicy.ValidateBatch(qe, 10);
            Assert.IsTrue(erre == null, "英语 " + g + " 年级门禁：" + erre);
            Assert.IsTrue(QuestionPolicy.IsGradeCapped(qe, new int[] { g }));
        }
    }
}
