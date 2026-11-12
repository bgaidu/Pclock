using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>App 的拆分文件：托盘与家长设置入口（同 partial class，行为不变）</summary>
    public partial class App : ApplicationContext
    {
        // 强引用：Program 只以 WeakReference 持有动态刷新委托
        Action langRefresh;

        /// <summary>构建托盘图标与右键菜单（主程序常驻入口）</summary>
        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Icon = Program.LoadAppIcon() ?? SystemIcons.Shield;
            tray.Text = Strings.Get("电脑锁 - 运行中");
            tray.Visible = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            // 菜单项写中文原文，由 LocalizeItems 翻译并缓存，切换语言时可即时重刷
            menu.Items.Add("设置…（需家长PIN）", null, delegate { ShowSettings(); });
            menu.Items.Add("立即锁定", null, delegate { LockNow(); });
            // P1-11 星星钱包：孩子可自助把星星换成使用时间（默认关闭，走钱包模式才显示）
            if (Store.IsStarRewardEnabled(currentUser) && Store.IsStarWalletEnabled(currentUser))
                menu.Items.Add("⭐ 星星兑换时间…", null, delegate { OpenStarRedeem(); });
            // P1-9 练习模式：不解锁、不消耗额度，主倒计时照常运行（默认关闭时不显示）
            if (Store.IsPracticeEnabled(currentUser))
                menu.Items.Add("练习模式（不解锁）", null, delegate { OpenPractice(); });
            // P1-10 使用记录
            menu.Items.Add("使用记录（7 天）", null, delegate
            {
                using (UsageForm uf = new UsageForm()) uf.ShowDialog();
            });
            // 一键诊断包：不含 PIN / 密码哈希，家长可直接把生成的 txt 发给维护者
            menu.Items.Add("导出诊断包…", null, delegate { ExportDiagnostics(); });
            Program.LocalizeItems(menu.Items);
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowSettings(); };

            // 托盘文字随倒计时每 500ms 重算，这里只负责切换语言当次立即生效
            langRefresh = delegate { tray.Text = Strings.Get("电脑锁 - 运行中"); };
            Program.RegisterDynamicRefresh(langRefresh);
        }

        /// <summary>孩子自助兑换星星（不需要 PIN；每次兑换写审计日志，家长可在使用记录里核对）</summary>
        void OpenStarRedeem()
        {
            if (lockForm != null) lockForm.SuspendInteraction = true;
            try
            {
                using (StarRedeemForm f = new StarRedeemForm()) f.ShowDialog();
            }
            finally
            {
                if (lockForm != null) lockForm.SuspendInteraction = false;
            }
        }

        /// <summary>导出诊断包：版本、文件清单、错误日志尾部、近期日志、脱敏配置摘要。</summary>
        void ExportDiagnostics()
        {
            if (lockForm != null) lockForm.SuspendInteraction = true;
            try
            {
                using (SaveFileDialog d = new SaveFileDialog())
                {
                    d.Filter = "文本文件 (*.txt)|*.txt";
                    d.FileName = Diagnostics.FileName(DateTime.Now);
                    d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (d.ShowDialog() != DialogResult.OK) return;

                    string dir = AppDomain.CurrentDomain.BaseDirectory;
                    string summary = "当前用户=" + currentUser +
                        "\r\n每次时长(分)=" + Store.GetDurationMinutes(currentUser) +
                        "\r\n可用时段=" + Store.GetWindow(currentUser) +
                        "\r\n多规则=" + Store.GetRules(currentUser) +
                        "\r\n题量=" + Store.GetQuestionCount(currentUser);
                    string path = Diagnostics.CollectAndSave(dir, AppVersion.Version,
                        AppVersion.Commit, summary, d.FileName);
                    MessageBox.Show(Strings.Fmt("诊断包已导出：{0}", path), Strings.Get("电脑锁"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Fmt("诊断包导出失败：{0}", ex.Message), Strings.Get("电脑锁"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (lockForm != null) lockForm.SuspendInteraction = false;
            }
        }

        /// <summary>家长 PIN 验证通过后打开设置窗口（托盘菜单 / 双击入口）</summary>
        public void ShowSettings()
        {
            // 若处于锁屏状态，先暂停锁屏抢焦点，否则家长设置窗口会被压到底层
            if (lockForm != null) lockForm.SuspendInteraction = true;
            try
            {
                bool ok = false;
                using (PinDialog pd = new PinDialog(Strings.Get("家长验证"), Strings.Get("请输入家长 PIN："),
                    delegate(string s) { return Store.VerifyPin(s); }))
                {
                    ok = (pd.ShowDialog() == DialogResult.OK);
                }
                if (!ok) return;
                // P0-1：旧安装可能仍在用默认 PIN，进设置时提醒家长立即修改
                if (Store.IsDefaultPinStillInUse())
                    MessageBox.Show(Strings.Get("当前仍在使用默认密码 1234，请立即修改家长 PIN 与卸载密码。"),
                        Strings.Get("电脑锁"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                using (SettingsForm sf = new SettingsForm()) sf.ShowDialog();
            }
            finally
            {
                if (lockForm != null) lockForm.SuspendInteraction = false;
            }
        }
    }
}
