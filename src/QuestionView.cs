using System;

namespace PCLock
{
    /// <summary>一个选项按钮应有的渲染状态（纯数据，不含任何控件引用）。</summary>
    public class OptionSlot
    {
        public bool Visible;
        public string Text;
        public int Index;       // 可见时为按钮下标；隐藏时为 -1
    }

    /// <summary>
    /// 题目 → 选项按钮渲染结果的纯函数。
    ///
    /// v1.6.4 事故根因：首题在 LockForm 构造函数里渲染，窗体尚未 Show，
    /// WinForms 子控件 Visible getter 恒返回 false，导致 Text/Tag 没被写入、
    /// 点击时对 null 拆箱抛 NullReferenceException。
    /// 现在把「该显示几个按钮、每个按钮写什么」完全交给这里计算，
    /// UI 层只做无脑赋值；纯函数可被单测覆盖构造期场景。
    /// </summary>
    public static class QuestionView
    {
        public static OptionSlot[] Build(Question q, int buttonCount)
        {
            if (buttonCount < 0) buttonCount = 0;
            OptionSlot[] slots = new OptionSlot[buttonCount];
            string[] opts = (q == null || q.Options == null) ? new string[0] : q.Options;
            bool math = (q != null && q.Type == QType.MathCalc);
            int count = UiSafe.OptionCount(opts, buttonCount);

            for (int i = 0; i < buttonCount; i++)
            {
                bool show = !math && UiSafe.ShowOption(i, count);
                OptionSlot s = new OptionSlot();
                s.Visible = show;
                s.Text = show ? (opts[i] ?? "") : "";
                s.Index = show ? i : -1;
                slots[i] = s;
            }
            return slots;
        }

        /// <summary>UI 层统一用这个方法把 Tag 写成 object；隐藏按钮为 null。</summary>
        public static object TagOf(OptionSlot slot)
        {
            return (slot != null && slot.Visible) ? (object)slot.Index : null;
        }
    }
}
