using System;

namespace PCLock
{
    /// <summary>
    /// UI 事件参数的安全解析（纯逻辑，可单测）。
    /// 起因：v1.6.4 线上事故——LockForm 首题在构造函数里渲染，此时窗体尚未 Show，
    /// WinForms 中父窗体不可见时子控件的 Visible getter 恒返回 false，
    /// 于是 `if (optBtns[i].Visible)` 永不成立，按钮的 Text/Tag 没被写入，
    /// 用户点击时 `(int)((Button)sender).Tag` 对 null 拆箱抛 NullReferenceException。
    /// 这里把「Tag → 选项下标」与「按钮是否显示」两件事收成纯函数，
    /// 调用方不再直接拆箱、也不再用控件可见性做逻辑判断。
    /// </summary>
    public static class UiSafe
    {
        /// <summary>非法下标（Tag 缺失 / 类型不对 / 越界），调用方应直接忽略这次点击。</summary>
        public const int InvalidIndex = -1;

        /// <summary>
        /// 解析选项按钮 Tag 中的下标。Tag 必须是非负 int 且小于实际选项数，
        /// 否则返回 <see cref="InvalidIndex"/>。
        /// </summary>
        public static int OptionIndex(object tag, int optionCount)
        {
            if (!(tag is int)) return InvalidIndex;
            int i = (int)tag;
            if (i < 0 || i >= optionCount) return InvalidIndex;
            return i;
        }

        /// <summary>第 index 个选项按钮是否应显示（选项数组可能为 null 或少于按钮数）。</summary>
        public static bool ShowOption(int index, int optionCount)
        {
            return index >= 0 && index < optionCount;
        }

        /// <summary>选项数量：null 或越界时统一钳到 [0, 4]，供布局与渲染共用。</summary>
        public static int OptionCount(string[] options, int buttonCount)
        {
            int n = (options == null) ? 0 : options.Length;
            if (n < 0) n = 0;
            if (n > buttonCount) n = buttonCount;
            return n;
        }
    }
}
