using System;
using PCLock;
using TUnit;

/// <summary>
/// 第十一批：护眼休息结束必须转答题锁屏（不能靠等满休息时间免答题拿时长）。
/// </summary>
public class Batch11Tests
{
    [Test]
    public void RestReasonDetectedForRestLock()
    {
        Assert.IsTrue(RestCooldown.IsRestReason("护眼休息时间"), "护眼休息硬锁必须被识别");
    }

    [Test]
    public void RestReasonRejectsOtherHardLocks()
    {
        Assert.IsFalse(RestCooldown.IsRestReason(null), "null 不能崩");
        Assert.IsFalse(RestCooldown.IsRestReason(""));
        Assert.IsFalse(RestCooldown.IsRestReason("今日使用额度已用完"), "额度锁不能被当成休息");
        Assert.IsFalse(RestCooldown.IsRestReason("系统时间异常，保护性锁定"), "时间异常锁不能被当成休息");
    }

    [Test]
    public void RestDeadlineEndsExactlyAtDeadline()
    {
        long now = 5000L * TimeSpan.TicksPerSecond;
        long until = RestCooldown.Deadline(now, 1);
        Assert.AreEqual(60, RestCooldown.RemainingSeconds(until, now));
        Assert.IsTrue(RestCooldown.IsResting(until, until - 1), "截止前 1 tick 仍在休息");
        Assert.IsFalse(RestCooldown.IsResting(until, until), "到点即结束，交由答题锁屏接手");
        Assert.AreEqual(0, RestCooldown.RemainingSeconds(until, until));
        Assert.AreEqual(0, RestCooldown.Deadline(now, 0), "0 分钟 = 不休息");
    }
}
