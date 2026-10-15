using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>App 的拆分文件：托盘与家长设置入口（同 partial class，行为不变）</summary>
    public partial class App : ApplicationContext
    {
        /// <summary>构建托盘图标与右键菜单（主程序常驻入口）</summary>
        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Icon = Program.LoadAppIcon() ?? SystemIcons.Shield;
            tray.Text = "电脑锁 - 运行中";
            tray.Visible = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("设置…（需家长PIN）", null, delegate { ShowSettings(); });
            menu.Items.Add("立即锁定", null, delegate { LockNow(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowSettings(); };
        }

        /// <summary>家长 PIN 验证通过后打开设置窗口（托盘菜单 / 双击入口）</summary>
        public void ShowSettings()
        {
            // 若处于锁屏状态，先暂停锁屏抢焦点，否则家长设置窗口会被压到底层
            if (lockForm != null) lockForm.SuspendInteraction = true;
            try
            {
                bool ok = false;
                using (PinDialog pd = new PinDialog("家长验证", "请输入家长 PIN：",
                    delegate(string s) { return Store.VerifyPin(s); }))
                {
                    ok = (pd.ShowDialog() == DialogResult.OK);
                }
                if (!ok) return;
                using (SettingsForm sf = new SettingsForm()) sf.ShowDialog();
            }
            finally
            {
                if (lockForm != null) lockForm.SuspendInteraction = false;
            }
        }
    }
}
