using System;
using System.Collections.Generic;
using PCLock;
using TUnit;

/// <summary>第十一批：星星钱包纯逻辑（家长自定义汇率、每日额度、余额与额度双重限制）。</summary>
public class Batch10Tests
{
    [Test]
    public void SecondsForUsesConfiguredRate()
    {
        Assert.AreEqual(600, StarWallet.SecondsFor(10, 60));
        Assert.AreEqual(300, StarWallet.SecondsFor(10, 30));
        Assert.AreEqual(0, StarWallet.SecondsFor(0, 60), "0 星不能兑出时间");
        Assert.AreEqual(0, StarWallet.SecondsFor(-3, 60), "负数按 0 处理");
    }

    [Test]
    public void ClampSecondsPerStarFallsBackForInvalid()
    {
        Assert.AreEqual(60, StarWallet.ClampSecondsPerStar(0), "0 秒会让兑换变空，回退默认");
        Assert.AreEqual(60, StarWallet.ClampSecondsPerStar(5));
        Assert.AreEqual(10, StarWallet.ClampSecondsPerStar(10));
        Assert.AreEqual(3600, StarWallet.ClampSecondsPerStar(99999));
    }

    [Test]
    public void RedeemableSecondsRespectsDailyLimit()
    {
        // 余额 100 星、60 秒/星 = 6000 秒；当日上限 30 分钟 → 1800 秒
        Assert.AreEqual(1800, StarWallet.RedeemableSeconds(100, 60, 0, 30));
        Assert.AreEqual(300, StarWallet.RedeemableSeconds(100, 60, 1500, 30), "只剩 300 秒额度");
        Assert.AreEqual(0, StarWallet.RedeemableSeconds(100, 60, 1800, 30), "额度用完为 0");
    }

    [Test]
    public void RedeemableSecondsHonorsBalanceAndNoLimit()
    {
        Assert.AreEqual(300, StarWallet.RedeemableSeconds(5, 60, 0, 30), "余额只有 5 星时按余额算");
        Assert.AreEqual(0, StarWallet.RedeemableSeconds(0, 60, 0, 30), "没星不能兑");
        Assert.AreEqual(6000, StarWallet.RedeemableSeconds(100, 60, 0, 0), "上限 0 = 不限");
    }

    [Test]
    public void MaxRedeemableStarsCapsByDay()
    {
        Assert.AreEqual(30, StarWallet.MaxRedeemableStars(100, 60, 0, 30), "30 分钟额度最多 30 星");
        Assert.AreEqual(5, StarWallet.MaxRedeemableStars(5, 60, 0, 30), "余额不足按余额");
        Assert.AreEqual(0, StarWallet.MaxRedeemableStars(10, 60, 1800, 30), "额度用完为 0");
        Assert.AreEqual(100, StarWallet.MaxRedeemableStars(100, 60, 0, 0), "不限额度按余额");
        Assert.AreEqual(0, StarWallet.MaxRedeemableStars(0, 60, 0, 30), "没星为 0");
    }

    [Test]
    public void TotalStarsSumsQuestionDifficulty()
    {
        List<Question> qs = new List<Question>();
        Question a = new Question();
        a.Grade = 3;
        a.Type = QType.MathCalc;          // 口算降一星：3-1=2
        Question b = new Question();
        b.Grade = 5;
        b.Type = QType.ChinesePinyin;     // 5 星
        qs.Add(a);
        qs.Add(b);
        Assert.AreEqual(7, StarReward.TotalStars(qs));
        Assert.AreEqual(0, StarReward.TotalStars(null), "空列表不能崩");
    }
}
