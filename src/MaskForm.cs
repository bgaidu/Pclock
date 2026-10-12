using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>
    /// 渐进遮罩：锁屏前最后若干分钟，全屏渐暗 + 居中水印提示，给孩子"快到时间了"的视觉预告。
    /// 通过 WS_EX_TRANSPARENT 让鼠标点击穿透，不干扰正在进行的操作，仅做视觉提醒。
    /// </summary>
    public class MaskForm : Form
    {
        Label label;

        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x80000;
        const int WS_EX_TRANSPARENT = 0x20;

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public MaskForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Rectangle vs = SystemInformation.VirtualScreen;
            Location = vs.Location;
            Size = vs.Size;
            BackColor = Color.Black;
            Opacity = 0;
            TopMost = true;
            ShowInTaskbar = false;

            label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.ForeColor = Color.White;
            label.BackColor = Color.Transparent;
            label.Font = new Font("Microsoft YaHei", 40, FontStyle.Bold);
            Controls.Add(label);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // 分层 + 鼠标点击穿透：视觉提醒但不挡操作
            int ex = GetWindowLong(this.Handle, GWL_EXSTYLE);
            SetWindowLong(this.Handle, GWL_EXSTYLE, ex | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }

        /// <summary>根据剩余秒数更新遮罩浓度与提示文字（由 App.UpdateMask 调用）</summary>
        public void SetLevel(int remainSec)
        {
            int start = Constants.MaskStartSec;
            double op = (remainSec >= start)
                ? 0
                : Constants.MaskMaxOpacity * (1.0 - (double)remainSec / start);
            if (op < 0) op = 0;
            if (op > Constants.MaskMaxOpacity) op = Constants.MaskMaxOpacity;
            Opacity = op;

            int m = remainSec / 60;
            int s = remainSec % 60;
            label.Text = "时间快到了\n还剩 " + m + " 分 " + s + " 秒";
            label.Visible = op > 0.02;
        }
    }
}
