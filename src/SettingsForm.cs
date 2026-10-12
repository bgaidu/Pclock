using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>通用密码验证小对话框（家长PIN / 卸载密码共用）</summary>
    public class PinDialog : Form
    {
        // 跨对话框实例的全局冷却：反复打开→失败→关闭→重开无法绕过
        static DateTime cooldownUntil = DateTime.MinValue;

        public PinDialog(string title, string prompt, Func<string, bool> verify)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            ClientSize = new Size(330, 135);

            Label l = new Label();
            l.Text = prompt;
            l.AutoSize = true;
            l.Location = new Point(16, 15);

            TextBox box = new TextBox();
            box.Location = new Point(16, 42);
            box.Width = 290;
            box.PasswordChar = '*';
            box.MaxLength = Constants.PinMaxLength;
            box.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                // 与锁屏界面输入框保持一致：PIN 只允许数字
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };

            Label err = new Label();
            err.ForeColor = Color.Firebrick;
            err.AutoSize = true;
            err.Location = new Point(16, 72);

            Button ok = new Button();
            ok.Text = "确定";
            ok.Location = new Point(150, 98);
            ok.Size = new Size(76, 28);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Location = new Point(230, 98);
            cancel.Size = new Size(76, 28);

            int fails = 0;
            ok.Click += delegate
            {
                // 全局冷却：上一次 5 连败后 30 秒内所有 PIN 对话框均拒绝输入
                if (DateTime.Now < cooldownUntil)
                {
                    int left = (int)Math.Ceiling((cooldownUntil - DateTime.Now).TotalSeconds);
                    err.Text = "尝试次数过多，请 " + left + " 秒后再试";
                    box.Text = "";
                    return;
                }
                if (verify(box.Text))
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    fails++;
                    box.Text = "";
                    box.Focus();
                    if (fails >= Constants.PinMaxFails)
                    {
                        cooldownUntil = DateTime.Now.AddSeconds(Constants.PinCooldownSec);
                        DialogResult = DialogResult.Cancel;
                        Close();
                    }
                    else
                    {
                        err.Text = "密码错误（还可尝试 " + (Constants.PinMaxFails - fails) + " 次）";
                    }
                }
            };

            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(l);
            Controls.Add(box);
            Controls.Add(err);
            Controls.Add(ok);
            Controls.Add(cancel);
        }
    }

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
            AutoScroll = true;
            ClientSize = new Size(480, 600);
            this.Icon = Program.LoadAppIcon();

            // 顶部菜单：关于
            MenuStrip menu = new MenuStrip();
            ToolStripMenuItem miAbout = new ToolStripMenuItem("关于");
            miAbout.Click += delegate { using (AboutForm af = new AboutForm()) af.ShowDialog(this); };
            menu.Items.Add(miAbout);
            menu.Dock = DockStyle.Top;
            this.MainMenuStrip = menu;
            Controls.Add(menu);

            // ---- 配置对象（按 Windows 用户分别限制）----
            GroupBox guUser = new GroupBox();
            guUser.Text = "配置对象（Windows 用户）";
            guUser.Location = new Point(15, 15);
            guUser.Size = new Size(440, 55);
            Label ulbl = NewLbl("为以下用户设置：", 15, 22);
            guUser.Controls.Add(ulbl);
            ComboBox cbUser = new ComboBox();
            cbUser.Location = new Point(150, 20);
            cbUser.Size = new Size(200, 24);
            cbUser.DropDownStyle = ComboBoxStyle.DropDownList;
            cbUser.Items.AddRange(Store.ListUsers());
            cbUser.SelectedIndex = 0;
            guUser.Controls.Add(cbUser);
            Controls.Add(guUser);

            // ---- 使用时长（按用户分别）----
            GroupBox gb = new GroupBox();
            gb.Text = "每次可用时长（按用户）";
            gb.Location = new Point(15, 80);
            gb.Size = new Size(440, 90);
            int[] mins = Constants.AllowedDurations;
            RadioButton[] rbs = new RadioButton[mins.Length];
            for (int i = 0; i < mins.Length; i++)
            {
                rbs[i] = new RadioButton();
                rbs[i].Text = mins[i] + "分";
                rbs[i].AutoSize = true;
                rbs[i].Location = new Point(15 + i * 70, 25);
                rbs[i].Tag = mins[i];
                gb.Controls.Add(rbs[i]);
            }
            Controls.Add(gb);
            RadioButton[] rbRef = rbs;

            Button saveDur = new Button();
            saveDur.Text = "保存时长";
            saveDur.Location = new Point(330, 52);
            saveDur.Size = new Size(100, 30);
            saveDur.Click += delegate
            {
                for (int i = 0; i < rbRef.Length; i++)
                {
                    if (rbRef[i].Checked)
                    {
                        string u = cbUser.Text;
                        Store.SetDurationMinutes(u, (int)rbRef[i].Tag);
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

            // ---- 锁屏答题：年级 / 学科（全局共用）----
            GroupBox gq = new GroupBox();
            gq.Text = "锁屏答题（年级 / 学科，按用户）";
            gq.Location = new Point(15, 180);
            gq.Size = new Size(440, 110);
            Controls.Add(gq);

            Label gl1 = NewLbl("年级：", 15, 22);
            gq.Controls.Add(gl1);
            RadioButton[] grb = new RadioButton[Constants.AllowedGrades.Length];
            int gcur = Store.GetGrade(cbUser.Text);
            for (int i = 0; i < Constants.AllowedGrades.Length; i++)
            {
                grb[i] = new RadioButton();
                grb[i].Text = Constants.AllowedGrades[i] + "年级";
                grb[i].AutoSize = true;
                grb[i].Location = new Point(70 + i * 62, 22);
                grb[i].Tag = Constants.AllowedGrades[i];
                if (Constants.AllowedGrades[i] == gcur) grb[i].Checked = true;
                gq.Controls.Add(grb[i]);
            }
            Label gl2 = NewLbl("学科：", 15, 52);
            gq.Controls.Add(gl2);
            ComboBox cbSubj = new ComboBox();
            cbSubj.Location = new Point(70, 48);
            cbSubj.Size = new Size(150, 24);
            cbSubj.DropDownStyle = ComboBoxStyle.DropDownList;
            cbSubj.Items.AddRange(new string[] { "混合随机", "语文", "数学", "英语", "语文+数学", "语文+英语", "数学+英语" });
            cbSubj.SelectedIndex = Store.GetSubject(cbUser.Text);
            gq.Controls.Add(cbSubj);
            Button saveEdu = new Button();
            saveEdu.Text = "保存设置";
            saveEdu.Location = new Point(330, 72);
            saveEdu.Size = new Size(100, 30);
            saveEdu.Click += delegate
            {
                string u = cbUser.Text;
                int grade = Store.GetGrade(u);
                for (int i = 0; i < grb.Length; i++) if (grb[i].Checked) grade = (int)grb[i].Tag;
                Store.SetGrade(u, grade);
                Store.SetSubject(u, cbSubj.SelectedIndex);
                MessageBox.Show(this, "已为「" + u + "」保存。下次该用户锁屏生效（当前锁屏不中断）。", "电脑锁",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            gq.Controls.Add(saveEdu);

            // ---- 每日可用时段（按用户分别）----
            GroupBox gw = new GroupBox();
            gw.Text = "每日可用时段（按用户）";
            gw.Location = new Point(15, 300);
            gw.Size = new Size(440, 100);
            Label wtip = NewLbl("格式：08:00-12:00;14:00-21:00（分号分隔多段，留空=全天可用）", 15, 22);
            gw.Controls.Add(wtip);
            TextBox txtWin = new TextBox();
            txtWin.Location = new Point(15, 48);
            txtWin.Size = new Size(410, 24);
            gw.Controls.Add(txtWin);
            Button saveWin = new Button();
            saveWin.Text = "保存时段";
            saveWin.Location = new Point(325, 74);
            saveWin.Size = new Size(105, 28);
            saveWin.Click += delegate
            {
                Store.SetWindow(cbUser.Text, txtWin.Text.Trim());
                MessageBox.Show(this, "已为「" + cbUser.Text + "」保存可用时段。", "电脑锁",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            gw.Controls.Add(saveWin);
            Controls.Add(gw);

            // ---- 修改家长 PIN ----
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
            Controls.Add(gp);

            // ---- 修改卸载密码 ----
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
            Controls.Add(gu);

            // ---- 本次会话：今日已用 / 暂停 / 加时（按用户）----
            GroupBox guOps = new GroupBox();
            guOps.Text = "本次会话（今日已用 / 暂停 / 加时）";
            guOps.Location = new Point(15, 610);
            guOps.Size = new Size(440, 78);
            Label lblUsed = NewLbl("今日已用：计算中…", 12, 20);
            lblUsed.ForeColor = Color.Gray;
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
            guOps.Controls.Add(lblUsed);
            guOps.Controls.Add(btnPause);
            guOps.Controls.Add(btnAdd);
            Controls.Add(guOps);

            // ---- 切换用户时刷新时长/时段/已用 ----
            Action RefreshUser = delegate
            {
                string u = cbUser.Text;
                int dur = Store.GetDurationMinutes(u);
                for (int i = 0; i < rbs.Length; i++) rbs[i].Checked = ((int)rbs[i].Tag == dur);
                txtWin.Text = Store.GetWindow(u);
                int used = Store.GetUsedToday(u);
                lblUsed.Text = "「" + u + "」今日已用：" + (used / 60) + " 分 " + (used % 60) + " 秒";
                // 锁屏答题：年级 / 学科同样按用户刷新
                int gsel = Store.GetGrade(u);
                for (int i = 0; i < grb.Length; i++) grb[i].Checked = ((int)grb[i].Tag == gsel);
                cbSubj.SelectedIndex = Store.GetSubject(u);
            };
            cbUser.SelectedIndexChanged += delegate { RefreshUser(); };
            RefreshUser();

            // ---- 操作按钮 ----
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

    /// <summary>关于窗口：程序名+版本号、免费声明、作者，以及原设置页的功能说明。</summary>
    public class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "关于";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 270);
            this.Icon = Program.LoadAppIcon();

            Label name = new Label();
            name.Text = "电脑锁 v" + AppVersion.Version + " (" + AppVersion.Commit + ")";
            name.Font = new Font("Microsoft YaHei", 14, FontStyle.Bold);
            name.AutoSize = true;
            name.Location = new Point(20, 20);

            Label free = new Label();
            free.Text = "本程序免费。";
            free.Font = new Font("Microsoft YaHei", 11);
            free.AutoSize = true;
            free.Location = new Point(20, 66);

            Label author = new Label();
            author.Text = "作者：bgaidu、AI";
            author.Font = new Font("Microsoft YaHei", 11);
            author.AutoSize = true;
            author.Location = new Point(20, 96);

            Label desc = new Label();
            desc.Text = "功能说明：锁屏后需全部答对 " + Constants.TotalQuestions +
                " 道题（数学填答案，语文/英语四选一，答错清零重来），或由家长输入 PIN 解锁。";
            desc.AutoSize = false;
            desc.Size = new Size(400, 70);
            desc.Location = new Point(20, 130);

            Button ok = new Button();
            ok.Text = "确定";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(180, 215);
            ok.Size = new Size(80, 30);
            ok.Click += delegate { Close(); };

            Controls.Add(name);
            Controls.Add(free);
            Controls.Add(author);
            Controls.Add(desc);
            Controls.Add(ok);
            AcceptButton = ok;
        }
    }
}
