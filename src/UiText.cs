using System;

namespace PCLock
{
    /// <summary>
    /// 锁屏运行期动态文案的纯函数集合：不引用 WinForms，可单测。
    /// 语言切换时的重算逻辑集中在这里，LockForm.langRefresh 只负责调用。
    /// </summary>
    public static class UiText
    {
        /// <summary>硬锁标题：App 运行期传入的中文短语过词典（字段本身保留中文供业务逻辑判断）</summary>
        public static string HardLockTitle(string reason)
        {
            return string.IsNullOrEmpty(reason) ? "" : Strings.Get(reason);
        }

        /// <summary>硬锁说明：护眼休息显示护眼口号；没有规则时提示用 PIN 解锁，否则列出当前规则</summary>
        public static string HardLockQuestion(string rules)
        {
            return HardLockQuestion(rules, null);
        }

        /// <summary>带硬锁原因的版本：护眼休息用「保护眼睛 从我做起」替代 PIN 提示</summary>
        public static string HardLockQuestion(string rules, string reason)
        {
            if (RestCooldown.IsRestReason(reason))
                return Strings.Get("保护眼睛 从我做起");
            return string.IsNullOrWhiteSpace(rules)
                ? Strings.Get("请使用家长 PIN 解锁")
                : Strings.Get("当前规则：") + "\n" + rules;
        }

        /// <summary>普通时段锁的说明文案</summary>
        public static string WindowQuestion(string window)
        {
            return string.IsNullOrWhiteSpace(window)
                ? Strings.Get("当前不在允许使用时段")
                : Strings.Get("允许使用时段：") + "\n" + window;
        }

        /// <summary>时段状态行；nextIn 为空时不追加「距下次可用」</summary>
        public static string WindowStatus(string nextIn)
        {
            string s = Strings.Get("时段开始后自动解锁，或由家长输入 PIN 解锁。");
            return string.IsNullOrEmpty(nextIn)
                ? s
                : s + Strings.Fmt("　距下次可用还有 {0}", nextIn);
        }

        /// <summary>家长 PIN 面板标签：冷却提示优先于失败次数提示，否则显示默认标签</summary>
        public static string PinLabel(int cooldownLeft, int failLeft)
        {
            if (cooldownLeft > 0)
                return Strings.Fmt("PIN 错误次数过多，请 {0} 秒后再试", cooldownLeft);
            if (failLeft > 0)
                return Strings.Fmt("PIN 错误（还可尝试 {0} 次）", failLeft);
            return Strings.Get("家长 PIN：");
        }
    }
}
