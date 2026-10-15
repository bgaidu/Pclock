using System;
using PCLock;
using TUnit;

/// <summary>
/// 硬断电离线折算纯函数测试（Store.DeductOffline）。
/// 覆盖：正常退出不扣、时钟回拨不扣、30 秒阈值边界（含向上取整）、
/// 扣到 0、超大间隔不溢出。对应 README"休眠/关机不扣时长"安全边界。
/// </summary>
public class OfflineDeductTests
{
    static long TicksFromSeconds(double sec) { return (long)(sec * TimeSpan.TicksPerSecond); }

    // 上次正常退出（Shutdown 会清时间戳为 0）：不折算
    [Test]
    public void ZeroTimestampNoDeduct()
    {
        Store.OfflineDeductResult r = Store.DeductOffline(3600, 0, TicksFromSeconds(100000));
        Assert.AreEqual(3600, r.Remaining, "剩余应不变");
        Assert.IsFalse(r.Deducted, "不应折算");
        Assert.AreEqual(0, r.DeductedSeconds, "扣除应为 0");
    }

    // 负时间戳（脏数据）：不折算，按正常退出处理
    [Test]
    public void NegativeTimestampNoDeduct()
    {
        Store.OfflineDeductResult r = Store.DeductOffline(3600, -5, TicksFromSeconds(100000));
        Assert.AreEqual(3600, r.Remaining, "剩余应不变");
        Assert.IsFalse(r.Deducted, "不应折算");
    }

    // 时钟回拨（now < last）：不折算——防止改系统时间"加时"
    [Test]
    public void ClockRollbackNoDeduct()
    {
        long now = TicksFromSeconds(100000);
        Store.OfflineDeductResult r = Store.DeductOffline(3600, now + TicksFromSeconds(60), now);
        Assert.AreEqual(3600, r.Remaining, "剩余应不变");
        Assert.IsFalse(r.Deducted, "不应折算");
        Assert.AreEqual(0.0, r.ElapsedSeconds, "回拨时 ElapsedSeconds 应为 0");
    }

    // now == last：离线 0 秒，不折算
    [Test]
    public void ExactlyNowNoDeduct()
    {
        long now = TicksFromSeconds(100000);
        Store.OfflineDeductResult r = Store.DeductOffline(3600, now, now);
        Assert.AreEqual(3600, r.Remaining, "剩余应不变");
        Assert.IsFalse(r.Deducted, "不应折算");
    }

    // 离线 29.999 秒（低于阈值）：视为正常关机延迟，不折算，但记录真实时长
    [Test]
    public void JustBelowThresholdNoDeduct()
    {
        Store.OfflineDeductResult r = Store.DeductOffline(3600,
            TicksFromSeconds(1000), TicksFromSeconds(1000 + 29.999));
        Assert.AreEqual(3600, r.Remaining, "剩余应不变");
        Assert.IsFalse(r.Deducted, "不应折算");
        Assert.GreaterOrEqual(r.ElapsedSeconds, 29.0, "应记录真实离线时长");
        Assert.LessOrEqual(r.ElapsedSeconds, 30.0, "应记录真实离线时长");
    }

    // 恰好 30 秒（到达阈值）：折算（阈值判断是"小于才不扣"）
    [Test]
    public void AtThresholdDeducts()
    {
        Store.OfflineDeductResult r = Store.DeductOffline(3600,
            TicksFromSeconds(1000), TicksFromSeconds(1030));
        Assert.IsTrue(r.Deducted, "到达阈值应折算");
        Assert.AreEqual(30, r.DeductedSeconds, "应扣 30 秒");
        Assert.AreEqual(3570, r.Remaining, "剩余应为 3570");
    }

    // 30.5 秒：向上取整扣 31（零点几秒也不给"零成本重启"留空间）
    [Test]
    public void PartialSecondCeilsUpward()
    {
        Store.OfflineDeductResult r = Store.DeductOffline(3600,
            TicksFromSeconds(1000), TicksFromSeconds(1030.5));
        Assert.IsTrue(r.Deducted, "应折算");
        Assert.AreEqual(31, r.DeductedSeconds, "应向上取整扣 31 秒");
        Assert.AreEqual(3569, r.Remaining, "剩余应为 3569");
    }

    // 90.2 秒：向上取整扣 91
    [Test]
    public void FractionalMinutesCeil()
    {
        Store.OfflineDeductResult r = Store.DeductOffline(3600,
            TicksFromSeconds(1000), TicksFromSeconds(1090.2));
        Assert.AreEqual(91, r.DeductedSeconds, "应向上取整扣 91 秒");
        Assert.AreEqual(3509, r.Remaining, "剩余应为 3509");
    }

    // 扣除量超过剩余：扣到 0（不能为负）
    [Test]
    public void ClampsToZero()
    {
        Store.OfflineDeductResult r = Store.DeductOffline(60,
            TicksFromSeconds(1000), TicksFromSeconds(1000 + 3600));
        Assert.IsTrue(r.Deducted, "应折算");
        Assert.AreEqual(3600, r.DeductedSeconds, "扣除量按真实离线时长计");
        Assert.AreEqual(0, r.Remaining, "剩余应扣到 0");
    }

    // 超大离线间隔（约 100 年）：扣除量封顶 int.MaxValue，不溢出、不变负
    [Test]
    public void HugeElapsedNoOverflow()
    {
        double hundredYears = 100.0 * 365.25 * 24 * 3600;   // ≈ 3.156e9 秒 > int.MaxValue
        Store.OfflineDeductResult r = Store.DeductOffline(int.MaxValue,
            TicksFromSeconds(1000), TicksFromSeconds(1000 + hundredYears));
        Assert.IsTrue(r.Deducted, "应折算");
        Assert.AreEqual(int.MaxValue, r.DeductedSeconds, "扣除量应封顶 int.MaxValue");
        Assert.AreEqual(0, r.Remaining, "剩余应为 0");
    }

    // 用真实 Ticks 构造的典型场景：离线 45 秒扣 45
    [Test]
    public void RealisticFortyFiveSecondOutage()
    {
        long now = DateTime.UtcNow.Ticks;
        Store.OfflineDeductResult r = Store.DeductOffline(3600,
            now - 45 * TimeSpan.TicksPerSecond, now);
        Assert.IsTrue(r.Deducted, "应折算");
        Assert.AreEqual(45, r.DeductedSeconds, "应扣 45 秒");
        Assert.AreEqual(3555, r.Remaining, "剩余应为 3555");
    }

    // 剩余为 0 时再折算：仍为 0（幂等，不会变负）
    [Test]
    public void ZeroRemainingStaysZero()
    {
        Store.OfflineDeductResult r = Store.DeductOffline(0,
            TicksFromSeconds(1000), TicksFromSeconds(2000));
        Assert.IsTrue(r.Deducted, "应折算");
        Assert.AreEqual(1000, r.DeductedSeconds, "应扣 1000 秒");
        Assert.AreEqual(0, r.Remaining, "剩余应保持 0");
    }
}
