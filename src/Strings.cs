using System;
using System.Collections.Generic;

namespace PCLock
{
    /// <summary>
    /// 轻量多语言层：以中文原文为 key，未收录的条目原样返回，
    /// 因此可以逐个界面渐进接入，不必一次性抽空所有字符串。
    /// 语言偏好存注册表，默认中文；切换后新打开的窗口生效。
    /// </summary>
    public static class Strings
    {
        static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "时间已到，已锁定", "Time is up - locked" },
            { "当前不在使用时段", "Outside allowed hours" },
            { "时段开始后自动解锁，或由家长输入 PIN 解锁。", "Unlocks automatically when the window starts, or with the parent PIN." },
            { "家长入口", "Parent entry" },
            { "家长设置", "Parent settings" },
            { "练习模式", "Practice" },
            { "继续答题", "Continue" },
            { "确 定", "OK" },
            { "解 锁", "Unlock" },
            { "解锁", "Unlock" },
            { "返回", "Back" },
            { "关闭", "Close" },
            { "保存全部", "Save all" },
            { "使用记录", "Usage" },
            { "高级规则", "Advanced rules" },
            { "关于", "About" },
            { "回答错误", "Wrong answer" },
            { "答对了！", "Correct!" },
            { "当前不在允许使用时段", "Currently outside allowed hours" },
            { "请使用家长 PIN 解锁", "Use the parent PIN to unlock" },
            { "休息结束后自动解锁，或由家长输入 PIN / 临时放行。", "Unlocks when rest ends, or with the parent PIN / temporary pass." }
        };

        public static string Lang { get; set; }

        static Strings() { Lang = "zh"; }

        public static string Get(string zh)
        {
            if (Lang == "en" && !string.IsNullOrEmpty(zh))
            {
                string v;
                if (En.TryGetValue(zh, out v)) return v;
            }
            return zh;
        }
    }
}
