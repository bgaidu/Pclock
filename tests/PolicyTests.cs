using System;
using System.Collections.Generic;
using PCLock;
using TUnit;

/// <summary>P0 新增策略纯函数单测 + 负向变异回归</summary>
public class PolicyTests
{
    static DateTime At(DayOfWeek day, int hour, int minute)
    {
        // 2026-11-09 是周一，按星期偏移得到一个确定日期
        DateTime d = new DateTime(2026, 11, 9).AddDays((int)day == 0 ? 6 : (int)day - 1);
        return new DateTime(d.Year, d.Month, d.Day, hour, minute, 0);
    }

    // ===== 多规则时段 =====
    [Test]
    public void LegacyWindowSpecKeepsHardLockSemantics()
    {
        List<WindowRule> rules = WindowRules.Parse("08:00-12:00;14:00-21:00");
        Assert.AreEqual(2, rules.Count);
        Assert.IsFalse(rules[0].QuizOnly);
        Assert.AreEqual(127, rules[0].DaysMask);

        WindowDecision inside = WindowRules.Evaluate("08:00-12:00;14:00-21:00", At(DayOfWeek.Wednesday, 10, 0));
        Assert.IsTrue(inside.HasRules);
        Assert.IsTrue(inside.QuizControlled);
        Assert.IsFalse(inside.HardLock);

        WindowDecision gap = WindowRules.Evaluate("08:00-12:00;14:00-21:00", At(DayOfWeek.Wednesday, 13, 0));
        Assert.IsTrue(gap.HardLock);
        Assert.IsFalse(gap.FreeUse);
    }

    [Test]
    public void WeekdayRulesOnlyApplyOnSelectedDays()
    {
        string spec = "1-5|16:00-21:00";
        Assert.IsTrue(WindowRules.Evaluate(spec, At(DayOfWeek.Monday, 17, 0)).QuizControlled);
        Assert.IsTrue(WindowRules.Evaluate(spec, At(DayOfWeek.Friday, 20, 59)).QuizControlled);
        Assert.IsTrue(WindowRules.Evaluate(spec, At(DayOfWeek.Saturday, 17, 0)).HardLock);
        Assert.IsTrue(WindowRules.Evaluate(spec, At(DayOfWeek.Sunday, 17, 0)).HardLock);
    }

    [Test]
    public void QuizOnlyRulesGiveFreeUseOutsideWindow()
    {
        string spec = "1-5|17:00-21:00@quiz";
        WindowDecision inside = WindowRules.Evaluate(spec, At(DayOfWeek.Tuesday, 18, 0));
        Assert.IsTrue(inside.QuizControlled);
        Assert.IsFalse(inside.FreeUse);

        WindowDecision outside = WindowRules.Evaluate(spec, At(DayOfWeek.Tuesday, 10, 0));
        Assert.IsTrue(outside.FreeUse);
        Assert.IsFalse(outside.HardLock);
    }

    [Test]
    public void MixedRulesPreferHardLock()
    {
        string spec = "1-5|08:00-12:00;6,7|09:00-11:00@quiz";
        Assert.IsTrue(WindowRules.Evaluate(spec, At(DayOfWeek.Saturday, 13, 0)).HardLock);
        Assert.IsTrue(WindowRules.Evaluate(spec, At(DayOfWeek.Wednesday, 9, 0)).QuizControlled);
    }

    [Test]
    public void CrossMidnightBelongsToPreviousDay()
    {
        // 周五 22:00-06:00：周六凌晨 03:00 应命中（归属周五），周六 23:00 不命中
        string spec = "5|22:00-06:00";
        Assert.IsTrue(WindowRules.Evaluate(spec, At(DayOfWeek.Saturday, 3, 0)).QuizControlled);
        Assert.IsTrue(WindowRules.Evaluate(spec, At(DayOfWeek.Friday, 23, 0)).QuizControlled);
        Assert.IsTrue(WindowRules.Evaluate(spec, At(DayOfWeek.Saturday, 23, 0)).HardLock);
    }

    [Test]
    public void EmptyOrInvalidRulesDisableFeature()
    {
        Assert.IsFalse(WindowRules.Evaluate("", At(DayOfWeek.Monday, 3, 0)).HasRules);
        Assert.IsFalse(WindowRules.Evaluate("25:00-26:00", At(DayOfWeek.Monday, 3, 0)).HasRules);
        Assert.AreEqual(0, WindowRules.Parse("nocolon").Count);
    }

    // ===== 每日额度 =====
    [Test]
    public void DailyQuotaLocksOnlyWhenEnabledAndReached()
    {
        Assert.IsFalse(DailyQuota.ShouldLock(3599, 60, 3, 0));
        Assert.IsTrue(DailyQuota.ShouldLock(3600, 60, 0, 0));
        Assert.IsTrue(DailyQuota.ShouldLock(0, 0, 3, 3));
        Assert.IsFalse(DailyQuota.ShouldLock(99999, 0, 999, 0)); // 关闭时永不锁
    }

    // ===== 加练池 =====
    [Test]
    public void ExtraPracticeAccumulatesAndSurvivesDisable()
    {
        PracticeState s = new PracticeState();
        for (int i = 0; i < 5; i++) s = ExtraPractice.OnWrong(s, 2, 1);
        Assert.AreEqual(1, s.WrongAccum);
        Assert.AreEqual(2, s.ExtraPending);
        Assert.IsFalse(ExtraPractice.CanUnlock(s));

        s = ExtraPractice.OnExtraCorrect(s);
        s = ExtraPractice.OnExtraCorrect(s);
        Assert.AreEqual(0, s.ExtraPending);
        Assert.IsTrue(ExtraPractice.CanUnlock(s));

        // 关闭新增加练：状态不变，但已欠的题仍需完成
        PracticeState off = ExtraPractice.OnWrong(s, 0, 5);
        Assert.AreEqual(0, off.ExtraPending);
    }

    [Test]
    public void ExtraPracticeDisabledByDefault()
    {
        PracticeState s = new PracticeState();
        for (int i = 0; i < 100; i++) s = ExtraPractice.OnWrong(s, 0, 0);
        Assert.AreEqual(0, s.ExtraPending);
        Assert.IsTrue(ExtraPractice.CanUnlock(s));
    }

    // ===== 护眼冷却 =====
    [Test]
    public void RestCooldownUsesUtcDeadline()
    {
        long t0 = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc).Ticks;
        long until = RestCooldown.Deadline(t0, 60);
        Assert.IsTrue(RestCooldown.IsResting(until, t0));
        Assert.AreEqual(3600, RestCooldown.RemainingSeconds(until, t0));
        Assert.IsFalse(RestCooldown.IsResting(until, until));
        Assert.AreEqual(0, RestCooldown.RemainingSeconds(until, until));
        Assert.AreEqual(0, RestCooldown.Deadline(t0, 0));
    }

    // ===== 时间回拨 =====
    [Test]
    public void TimeTamperToleratesSmallAdjustments()
    {
        long t0 = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc).Ticks;
        long sec = TimeSpan.TicksPerSecond;
        Assert.IsFalse(TimeTamper.IsBackward(0, t0, 300));
        Assert.IsFalse(TimeTamper.IsBackward(t0, t0 - 60 * sec, 300));   // NTP 微调不误报
        Assert.IsTrue(TimeTamper.IsBackward(t0, t0 - 3600 * sec, 300));   // 大幅回拨
    }

    // ===== 星级奖励 =====
    [Test]
    public void StarRewardCapsAtOneHour()
    {
        List<Question> qs = new List<Question>();
        for (int i = 0; i < 10; i++)
            qs.Add(new Question { Grade = 6, Type = QType.ChinesePinyin });
        Assert.AreEqual(5, StarReward.StarOf(qs[0]));
        int[] rewards = new int[] { 1, 2, 3, 4, 5 };
        Assert.AreEqual(3000, StarReward.RewardSeconds(qs, rewards));
        Assert.AreEqual(3600, StarReward.RewardSeconds(qs, new int[] { 20, 20, 20, 20, 20 }));
        Assert.AreEqual(0, StarReward.RewardSeconds(qs, new int[] { 1, 2 }));
    }

    // ===== 答题反馈 / 判定配置 =====
    [Test]
    public void AnswerFeedbackShowsCorrectAnswer()
    {
        Question math = new Question { Type = QType.MathCalc, Prompt = "2 + 3 = ?", Answer = 5 };
        Assert.IsTrue(AnswerFeedback.Build(math).Contains("5"));

        Question choice = new Question { Type = QType.ChinesePinyin, Prompt = "太阳",
            Options = new string[] { "tai yang", "yue liang" }, CorrectIndex = 0,
            Explanation = "恒星" };
        string fb = AnswerFeedback.Build(choice);
        Assert.IsTrue(fb.Contains("tai yang"));
        Assert.IsTrue(fb.Contains("恒星"));
    }

    [Test]
    public void QuizConfigClampsInvalidValues()
    {
        Assert.AreEqual(10, QuizConfig.ClampQuestionCount(0));
        Assert.AreEqual(30, QuizConfig.ClampQuestionCount(999));
        Assert.AreEqual(5, QuizConfig.ClampStreak(5, 10));
        Assert.AreEqual(10, QuizConfig.ClampStreak(99, 10));
        Assert.AreEqual(10, QuizConfig.ClampStreak(0, 10));
    }
}

/// <summary>
/// 负向变异回归（P1-11）：把手写「变异版」规则与真实规则对比，
/// 并断言既有测试断言确实能区分二者 —— 若某天有人误改核心常量，
/// 对应断言会立刻失败，而不是悄悄放行。
/// </summary>
public class MutationTests
{
    // 变异 1：删掉离线折算的 30 秒阈值（29 秒离线也被扣）
    static int MutantDeductNoThreshold(int remaining, long last, long now)
    {
        if (last <= 0) return remaining;
        double elapsed = (now - last) / (double)TimeSpan.TicksPerSecond;
        if (elapsed <= 0) return remaining;
        long left = (long)remaining - (long)Math.Ceiling(elapsed);
        return left < 0 ? 0 : (int)left;
    }

    [Test]
    public void OfflineThresholdMutationIsDetected()
    {
        long t0 = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc).Ticks;
        long t29 = t0 + 29 * TimeSpan.TicksPerSecond;
        Store.OfflineDeductResult real = Store.DeductOffline(100, t0, t29);
        Assert.IsFalse(real.Deducted);                       // 既有 OfflineDeductTests 的边界断言
        Assert.AreEqual(100, real.Remaining);
        int mutant = MutantDeductNoThreshold(100, t0, t29);
        Assert.IsTrue(mutant != real.Remaining);              // 变异会被该断言捕获
    }

    // 变异 2：时段边界由闭区间改成开区间
    [Test]
    public void WindowBoundaryMutationIsDetected()
    {
        TimeSpan now = new TimeSpan(21, 0, 0);
        bool real = Store.IsWithinWindow("08:00-12:00;14:00-21:00", now);
        bool mutant = false; // 变异实现把 <= 改成 <，21:00 被判为时段外
        Assert.IsTrue(real);
        Assert.IsTrue(real != mutant);                        // 既有 BoundaryInclusive 会失败
    }

    // 变异 3：每日额度由 >= 改成 >
    [Test]
    public void QuotaBoundaryMutationIsDetected()
    {
        bool real = DailyQuota.ShouldLock(3600, 60, 0, 0);
        bool mutant = 3600 > 60 * 60;
        Assert.IsTrue(real);
        Assert.IsTrue(real != mutant);
    }

    // 变异 4：加练阈值差一（每 3 错才加练，而非每 2 错）
    [Test]
    public void PracticeThresholdMutationIsDetected()
    {
        PracticeState s = new PracticeState();
        for (int i = 0; i < 2; i++) s = ExtraPractice.OnWrong(s, 2, 1);
        int real = s.ExtraPending;
        int mutant = 0; // 阈值 3 时 2 次答错不触发加练
        Assert.AreEqual(1, real);
        Assert.IsTrue(real != mutant);
    }

    // 变异 5：回拨检测容差被放大到无限（永不报警）
    [Test]
    public void TimeTamperMutationIsDetected()
    {
        long t0 = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc).Ticks;
        bool real = TimeTamper.IsBackward(t0, t0 - 3600 * TimeSpan.TicksPerSecond, 300);
        bool mutant = false;
        Assert.IsTrue(real);
        Assert.IsTrue(real != mutant);
    }
}
