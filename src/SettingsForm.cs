using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>家长设置窗口（进入前已验证家长PIN）</summary>
    public class SettingsForm : Form
    {
        public SettingsForm()
        {
            Text = "电脑锁 v" + AppVersion.Version + " - 家长设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            // 置顶：锁屏全屏且 TopMost，家长设置必须浮于其上，否则被压到底层无法操作
            TopMost = true;
            AutoScroll = true;
            ClientSize = new Size(480, 600);
            this.Icon = Program.LoadAppIcon();

            // 顶部菜单：关于
            MenuStrip menu = BuildMenuStrip();
            Controls.Add(menu);

            // ---- 配置对象（按 Windows 用户分别限制）----
            ComboBox cbUser;
            Controls.Add(BuildUserGroup(out cbUser));

            // ---- 使用时长（按用户分别）----
            RadioButton[] rbs;
            Controls.Add(BuildDurationGroup(cbUser, out rbs));

            // ---- 锁屏答题：年级 / 科目（可多选，按用户）----
            CheckBox[] gcb, scb;
            Controls.Add(BuildQuizGroup(cbUser, out gcb, out scb));

            // ---- 每日可用时段（按用户分别）----
            TextBox txtWin;
            Controls.Add(BuildWindowGroup(cbUser, out txtWin));

            // ---- 修改家长 PIN ----
            Controls.Add(BuildPinGroup());

            // ---- 修改卸载密码 ----
            Controls.Add(BuildUninstallPinGroup());

            // ---- 本次会话：今日已用 / 暂停 / 加时（按用户）----
            Label lblUsed;
            Controls.Add(BuildSessionGroup(out lblUsed));

            // ---- 切换用户时刷新时长/时段/已用 ----
            cbUser.SelectedIndexChanged += delegate { RefreshUserControls(cbUser, rbs, txtWin, lblUsed, gcb, scb); };
            RefreshUserControls(cbUser, rbs, txtWin, lblUsed, gcb, scb);

            // ---- 操作按钮 ----
            BuildActionButtons();

            // 将菜单以外的内容整体放入可滚动面板，面板位于菜单下方，避免与菜单重叠
            Panel content = new Panel();
            content.Location = new Point(0, menu.Height);
            content.Size = new Size(ClientSize.Width, ClientSize.Height - menu.Height);
            content.AutoScroll = true;
            for (int i = Controls.Count - 1; i >= 0; i--)
            {
                Control c = Controls[i];
                if (c is MenuStrip) continue;
                Controls.RemoveAt(i);
                content.Controls.Add(c);
            }
            Controls.Add(content);
            this.AutoScroll = false;
            menu.BringToFront();
            Program.FlattenButtons(this);
        }

        MenuStrip BuildMenuStrip()
        {
            MenuStrip menu = new MenuStrip();
            ToolStripMenuItem miAbout = new ToolStripMenuItem("关于");
            miAbout.Click += delegate { using (AboutForm af = new AboutForm()) af.ShowDialog(this); };
            menu.Items.Add(miAbout);
            menu.Dock = DockStyle.Top;
            this.MainMenuStrip = menu;
            return menu;
        }

        GroupBox BuildUserGroup(out ComboBox cbUser)
        {
            GroupBox guUser = new GroupBox();
            guUser.Text = "配置对象（Windows 用户）";
            guUser.Location = new Point(15, 15);
            guUser.Size = new Size(440, 55);
            Label ulbl = NewLbl("为以下用户设置：", 15, 22);
            guUser.Controls.Add(ulbl);
            ComboBox cb = new ComboBox();
            cb.Location = new Point(150, 20);
            cb.Size = new Size(200, 24);
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.Items.AddRange(Store.ListUsers());
            cb.SelectedIndex = 0;
            guUser.Controls.Add(cb);
            cbUser = cb;
            return guUser;
        }

        GroupBox BuildDurationGroup(ComboBox cbUser, out RadioButton[] rbs)
        {
            GroupBox gb = new GroupBox();
            gb.Text = "每次可用时长（按用户）";
            gb.Location = new Point(15, 80);
            gb.Size = new Size(440, 90);
            int[] mins = Constants.AllowedDurations;
            RadioButton[] arr = new RadioButton[mins.Length];
            for (int i = 0; i < mins.Length; i++)
            {
                arr[i] = new RadioButton();
                arr[i].Text = mins[i] + "分";
                arr[i].AutoSize = true;
                arr[i].Location = new Point(15 + i * 70, 25);
                arr[i].Tag = mins[i];
                gb.Controls.Add(arr[i]);
            }
            rbs = arr;

            Button saveDur = new Button();
            saveDur.Text = "保存时长";
            saveDur.Location = new Point(330, 52);
            saveDur.Size = new Size(100, 30);
            saveDur.Click += delegate
            {
                // 注意：匿名方法内不能引用外层 out 参数，这里捕获局部数组 arr
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i].Checked)
                    {
                        string u = cbUser.Text;
                        Store.SetDurationMinutes(u, (int)arr[i].Tag);
                        if (u == Store.GetCurrentUser())
                        {
                            if (App.Instance != null) App.Instance.ResetRemaining();
                            MessageBox.Show(this, "已保存。本次剩余时间已重置。", "电脑锁",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(this, "已为「" + u + "」保存。下次该用户登录时生效。", "电脑锁",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        break;
                    }
                }
            };
            gb.Controls.Add(saveDur);
            return gb;
        }

        GroupBox BuildQuizGroup(ComboBox cbUser, out CheckBox[] gcb, out CheckBox[] scb)
        {
            GroupBox gq = new GroupBox();
            gq.Text = "锁屏答题（年级 / 科目，可多选）";
            gq.Location = new Point(15, 180);
            gq.Size = new Size(440, 122);

            Label gl1 = NewLbl("年级：", 15, 24);
            gq.Controls.Add(gl1);
            CheckBox[] gs = new CheckBox[Constants.AllowedGrades.Length];
            for (int i = 0; i < Constants.AllowedGrades.Length; i++)
            {
                gs[i] = new CheckBox();
                gs[i].Text = Constants.AllowedGrades[i] + "年级";
                gs[i].AutoSize = true;
                gs[i].Location = new Point(70 + i * 62, 22);
                gs[i].Tag = Constants.AllowedGrades[i];
                gq.Controls.Add(gs[i]);
            }
            gcb = gs;
            Label gl2 = NewLbl("科目：", 15, 62);
            gq.Controls.Add(gl2);
            int[] subjVals = new int[] { 1, 2, 3 };
            string[] subjNames = new string[] { "语文", "数学", "英语" };
            CheckBox[] ss = new CheckBox[3];
            for (int i = 0; i < 3; i++)
            {
                ss[i] = new CheckBox();
                ss[i].Text = subjNames[i];
                ss[i].AutoSize = true;
                ss[i].Location = new Point(70 + i * 72, 60);
                ss[i].Tag = subjVals[i];
                gq.Controls.Add(ss[i]);
            }
            scb = ss;
            Button saveEdu = new Button();
            saveEdu.Text = "保存设置";
            saveEdu.Location = new Point(330, 86);
            saveEdu.Size = new Size(100, 30);
            saveEdu.Click += delegate
            {
                string u = cbUser.Text;
                List<int> gg = new List<int>();
                for (int i = 0; i < gs.Length; i++) if (gs[i].Checked) gg.Add((int)gs[i].Tag);
                List<int> s2 = new List<int>();
                for (int i = 0; i < ss.Length; i++) if (ss[i].Checked) s2.Add((int)ss[i].Tag);
                if (gg.Count == 0) { MessageBox.Show(this, "请至少选择一个年级。", "电脑锁"); return; }
                if (s2.Count == 0) { MessageBox.Show(this, "请至少选择一个科目。", "电脑锁"); return; }
                Store.SetGrades(u, gg.ToArray());
                Store.SetSubjects(u, s2.ToArray());
                MessageBox.Show(this, "已为「" + u + "」保存。下次该用户锁屏生效（当前锁屏不中断）。", "电脑锁",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            gq.Controls.Add(saveEdu);
            return gq;
        }

        GroupBox BuildWindowGroup(ComboBox cbUser, out TextBox txtWin)
        {
            GroupBox gw = new GroupBox();
            gw.Text = "每日可用时段（按用户）";
            gw.Location = new Point(15, 300);
            gw.Size = new Size(440, 100);
            Label wtip = NewLbl("格式：08:00-12:00;14:00-21:00（分号分隔多段，留空=全天可用；支持跨午夜如 22:00-06:00）", 15, 22);
            gw.Controls.Add(wtip);
            TextBox tb = new TextBox();
            tb.Location = new Point(15, 48);
            tb.Size = new Size(410, 24);
            gw.Controls.Add(tb);
            txtWin = tb;
            Button saveWin = new Button();
            saveWin.Text = "保存时段";
            saveWin.Location = new Point(325, 74);
            saveWin.Size = new Size(105, 28);
            saveWin.Click += delegate
            {
                Store.SetWindow(cbUser.Text, tb.Text.Trim());
                MessageBox.Show(this, "已为「" + cbUser.Text + "」保存可用时段。", "电脑锁",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            gw.Controls.Add(saveWin);
            return gw;
        }

        GroupBox BuildPinGroup()
        {
            GroupBox gp = new GroupBox();
            gp.Text = "修改家长 PIN（锁屏界面\"家长入口\"用）";
            gp.Location = new Point(15, 410);
            gp.Size = new Size(440, 90);
            TextBox pin1 = NewBox(90, 26);
            TextBox pin2 = NewBox(300, 26);
            Label pl1 = NewLbl("新 PIN：", 15, 30);
            Label pl2 = NewLbl("再输一遍：", 215, 30);
            Button btnPin = new Button();
            btnPin.Text = "修改 PIN";
            btnPin.Location = new Point(315, 55);
            btnPin.Size = new Size(110, 28);
            btnPin.Click += delegate
            {
                if (pin1.Text.Length < Constants.PinMinLength)
                {
                    MessageBox.Show(this, "PIN 至少 " + Constants.PinMinLength + " 位。", "电脑锁");
                    return;
                }
                if (pin1.Text != pin2.Text)
                {
                    MessageBox.Show(this, "两次输入的 PIN 不一致。", "电脑锁");
                    return;
                }
                Store.SetPin(pin1.Text);
                pin1.Text = "";
                pin2.Text = "";
                MessageBox.Show(this, "家长 PIN 已修改。", "电脑锁",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            gp.Controls.Add(pl1);
            gp.Controls.Add(pin1);
            gp.Controls.Add(pl2);
            gp.Controls.Add(pin2);
            gp.Controls.Add(btnPin);
            return gp;
        }

        GroupBox BuildUninstallPinGroup()
        {
            GroupBox gu = new GroupBox();
            gu.Text = "修改卸载密码（卸载本程序时必须输入）";
            gu.Location = new Point(15, 510);
            gu.Size = new Size(440, 90);
            TextBox up1 = NewBox(90, 26);
            TextBox up2 = NewBox(300, 26);
            Label ul1 = NewLbl("新密码：", 15, 30);
            Label ul2 = NewLbl("再输一遍：", 215, 30);
            Button btnUn = new Button();
            btnUn.Text = "修改密码";
            btnUn.Location = new Point(315, 55);
            btnUn.Size = new Size(110, 28);
            btnUn.Click += delegate
            {
                if (up1.Text.Length < Constants.PinMinLength)
                {
                    MessageBox.Show(this, "卸载密码至少 " + Constants.PinMinLength + " 位。", "电脑锁");
                    return;
                }
                if (up1.Text != up2.Text)
                {
                    MessageBox.Show(this, "两次输入的密码不一致。", "电脑锁");
                    return;
                }
                Store.SetUninstallPin(up1.Text);
                up1.Text = "";
                up2.Text = "";
                MessageBox.Show(this, "卸载密码已修改。", "电脑锁",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            gu.Controls.Add(ul1);
            gu.Controls.Add(up1);
            gu.Controls.Add(ul2);
            gu.Controls.Add(up2);
            gu.Controls.Add(btnUn);
            return gu;
        }

        GroupBox BuildSessionGroup(out Label lblUsed)
        {
            GroupBox guOps = new GroupBox();
            guOps.Text = "本次会话（今日已用 / 暂停 / 加时）";
            guOps.Location = new Point(15, 610);
            guOps.Size = new Size(440, 78);
            Label lu = NewLbl("今日已用：计算中…", 12, 20);
            lu.ForeColor = Color.Gray;
            Button btnPause = new Button();
            btnPause.Text = "暂停计时";
            btnPause.Location = new Point(12, 44);
            btnPause.Size = new Size(200, 28);
            btnPause.Click += delegate
            {
                if (App.Instance == null)
                {
                    MessageBox.Show(this, "请先启动电脑锁主程序后再操作。", "电脑锁");
                    return;
                }
                App.Instance.SetPaused(!App.Instance.IsPaused());
                btnPause.Text = App.Instance.IsPaused() ? "继续计时" : "暂停计时";
                MessageBox.Show(this, App.Instance.IsPaused()
                    ? "已暂停计时：孩子仍可使用电脑，但剩余时间不再减少。"
                    : "已继续计时。", "电脑锁", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            Button btnAdd = new Button();
            btnAdd.Text = "本次 +15 分钟";
            btnAdd.Location = new Point(228, 44);
            btnAdd.Size = new Size(200, 28);
            btnAdd.Click += delegate
            {
                if (App.Instance == null)
                {
                    MessageBox.Show(this, "请先启动电脑锁主程序后再操作。", "电脑锁");
                    return;
                }
                App.Instance.AddTime(15);
                MessageBox.Show(this, "已为本次会话增加 15 分钟使用时间。", "电脑锁",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            guOps.Controls.Add(lu);
            guOps.Controls.Add(btnPause);
            guOps.Controls.Add(btnAdd);
            lblUsed = lu;
            return guOps;
        }

        void BuildActionButtons()
        {
            Button btnLock = new Button();
            btnLock.Text = "立即锁定";
            btnLock.Location = new Point(15, 705);
            btnLock.Size = new Size(140, 34);
            btnLock.Click += delegate
            {
                if (App.Instance == null)
                {
                    MessageBox.Show(this, "请先启动电脑锁主程序后再锁定。", "电脑锁");
                    return;
                }
                Close();
                App.Instance.LockNow();
            };

            Button btnExit = new Button();
            btnExit.Text = "退出程序";
            btnExit.Location = new Point(165, 705);
            btnExit.Size = new Size(140, 34);
            btnExit.Click += delegate
            {
                if (App.Instance == null)
                {
                    MessageBox.Show(this, "主程序未运行，无需退出。", "电脑锁");
                    return;
                }
                if (MessageBox.Show(this,
                    "确定退出电脑锁？退出后本次不再计时，下次开机自动重新开始。",
                    "电脑锁", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Close();
                    App.Instance.Shutdown(false);
                }
            };

            Button btnRemove = new Button();
            btnRemove.Text = "卸载并删除程序";
            btnRemove.Location = new Point(315, 705);
            btnRemove.Size = new Size(140, 34);
            btnRemove.Click += delegate
            {
                if (App.Instance == null)
                {
                    MessageBox.Show(this, "请先启动电脑锁主程序后再卸载。", "电脑锁");
                    return;
                }
                if (MessageBox.Show(this,
                    "确定卸载并删除电脑锁的全部文件和设置吗？",
                    "电脑锁", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
                bool ok = false;
                using (PinDialog pd = new PinDialog("卸载验证", "请输入卸载密码：",
                    delegate(string s) { return Store.VerifyUninstallPin(s); }))
                {
                    ok = (pd.ShowDialog() == DialogResult.OK);
                }
                if (!ok) return;
                Close();
                App.Instance.Shutdown(true);
            };

            Controls.Add(btnLock);
            Controls.Add(btnExit);
            Controls.Add(btnRemove);
        }

        /// <summary>切换用户时刷新时长/时段/已用/年级科目多选状态</summary>
        static void RefreshUserControls(ComboBox cbUser, RadioButton[] rbs, TextBox txtWin,
            Label lblUsed, CheckBox[] gcb, CheckBox[] scb)
        {
            string u = cbUser.Text;
            int dur = Store.GetDurationMinutes(u);
            for (int i = 0; i < rbs.Length; i++) rbs[i].Checked = ((int)rbs[i].Tag == dur);
            txtWin.Text = Store.GetWindow(u);
            int used = Store.GetUsedToday(u);
            lblUsed.Text = "「" + u + "」今日已用：" + (used / 60) + " 分 " + (used % 60) + " 秒";
            // 锁屏答题：年级 / 科目（多选）按用户刷新
            int[] gsel = Store.GetGrades(u);
            for (int i = 0; i < gcb.Length; i++) gcb[i].Checked = Array.IndexOf(gsel, (int)gcb[i].Tag) >= 0;
            int[] ssel = Store.GetSubjects(u);
            for (int i = 0; i < scb.Length; i++) scb[i].Checked = Array.IndexOf(ssel, (int)scb[i].Tag) >= 0;
        }

        static TextBox NewBox(int x, int y)
        {
            TextBox b = new TextBox();
            b.Location = new Point(x, y);
            b.Width = 120;
            b.PasswordChar = '*';
            b.MaxLength = Constants.PinMaxLength;
            // 锁屏界面 PIN 输入框只接受数字，这里必须一致，否则含字母的 PIN 在锁屏时无法输入
            b.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            return b;
        }

        static Label NewLbl(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            return l;
        }
    }
}
