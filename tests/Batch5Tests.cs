using System;
using System.Collections.Generic;
using PCLock;
using TUnit;

/// <summary>
/// 第五批：v1.6.4 锁屏点选项崩溃（NullReferenceException）的回归。
/// 根因是「窗体未 Show 时子控件 Visible getter 恒为 false」导致 Tag 未写入，
/// 用户点击时对 null 拆箱。这里把 Tag 解析与选项可见性固化成纯函数并逐条断言。
/// </summary>
public class Batch5Tests
{
    // ===== Tag 解析：任何非法输入都必须返回 InvalidIndex，绝不抛异常 =====

    [Test]
    public void NullTagIsRejected()
    {
        // v1.6.4 事故现场：Tag 为 null，旧代码 (int)null 直接抛 NullReferenceException
        Assert.AreEqual(UiSafe.InvalidIndex, UiSafe.OptionIndex(null, 4), "null Tag 必须被拒绝");
    }

    [Test]
    public void NonIntTagIsRejected()
    {
        Assert.AreEqual(UiSafe.InvalidIndex, UiSafe.OptionIndex("1", 4), "字符串 Tag 必须被拒绝");
        Assert.AreEqual(UiSafe.InvalidIndex, UiSafe.OptionIndex(1.0, 4), "double Tag 必须被拒绝");
        Assert.AreEqual(UiSafe.InvalidIndex, UiSafe.OptionIndex(new object(), 4), "任意对象必须被拒绝");
    }

    [Test]
    public void OutOfRangeTagIsRejected()
    {
        Assert.AreEqual(UiSafe.InvalidIndex, UiSafe.OptionIndex(-1, 4), "负下标必须被拒绝");
        Assert.AreEqual(UiSafe.InvalidIndex, UiSafe.OptionIndex(4, 4), "等于选项数必须被拒绝");
        Assert.AreEqual(UiSafe.InvalidIndex, UiSafe.OptionIndex(3, 2), "判断题只有 2 个选项时第 3 项必须被拒绝");
    }

    [Test]
    public void ValidTagResolvesToIndex()
    {
        Assert.AreEqual(0, UiSafe.OptionIndex(0, 4), "第 1 项");
        Assert.AreEqual(3, UiSafe.OptionIndex(3, 4), "第 4 项");
        Assert.AreEqual(1, UiSafe.OptionIndex(1, 2), "判断题第 2 项");
    }

    // ===== 选项可见性：判断题（2 项）/ 数学四选一（4 项）/ 退化（null） =====

    [Test]
    public void ShowOptionRespectsActualCount()
    {
        Assert.IsTrue(UiSafe.ShowOption(0, 2), "判断题第 1 项应显示");
        Assert.IsTrue(UiSafe.ShowOption(1, 2), "判断题第 2 项应显示");
        Assert.IsFalse(UiSafe.ShowOption(2, 2), "判断题第 3 项不应显示");
        Assert.IsFalse(UiSafe.ShowOption(3, 2), "判断题第 4 项不应显示");
        Assert.IsTrue(UiSafe.ShowOption(3, 4), "四选一第 4 项应显示");
    }

    [Test]
    public void OptionCountClampsNullAndOverflow()
    {
        Assert.AreEqual(0, UiSafe.OptionCount(null, 4), "null 选项数组 → 0（按钮全隐藏，不越界）");
        Assert.AreEqual(2, UiSafe.OptionCount(new string[] { "对", "错" }, 4), "判断题 → 2");
        Assert.AreEqual(4, UiSafe.OptionCount(new string[] { "a", "b", "c", "d" }, 4), "四选一 → 4");
        Assert.AreEqual(4, UiSafe.OptionCount(new string[] { "a", "b", "c", "d", "e" }, 4), "超出按钮数 → 钳到 4");
        Assert.AreEqual(0, UiSafe.OptionCount(new string[0], 4), "空数组 → 0");
    }

    /// <summary>
    /// 组合场景还原：首题是判断题时，第 3/4 个按钮隐藏但对象仍存在，
    /// 隐藏按钮的旧 Tag 可能是之前一道四选一题留下的 2/3 —— 必须不越界、不误判。
    /// </summary>
    [Test]
    public void HiddenButtonStaleTagCannotIndexPastOptions()
    {
        string[] judge = new string[] { "对", "错" };
        int optionCount = UiSafe.OptionCount(judge, 4);
        for (int i = 0; i < 4; i++)
        {
            bool show = UiSafe.ShowOption(i, optionCount);
            object tag = show ? (object)i : null;      // 渲染时应写入的 Tag
            int resolved = UiSafe.OptionIndex(tag, optionCount);
            if (show) Assert.AreEqual(i, resolved, "可见按钮 " + i + " 应解析为自身下标");
            else Assert.AreEqual(UiSafe.InvalidIndex, resolved, "隐藏按钮 " + i + " 必须解析为非法");
        }
    }
}
