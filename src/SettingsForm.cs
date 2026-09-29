using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>通用密码验证小对话框（家长PIN / 卸载密码共用）</summary>
    public class PinDialog : Form
    {
        public PinDialog(string title, string prompt, Func<string, bool> verify)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
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
                    err.Text = "密码错误（已尝试 " + fails + " 次）";
                    if (fails >= 5)
                    {
                        DialogResult = DialogResult.Cancel;
                        Close();
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
            Text = "电脑锁 - 家长设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            ClientSize = new Size(470, 480);

            // ---- 使用时长 ----
            GroupBox gb = new GroupBox();
            gb.Text = "每次可用时长";
            gb.Location = new Point(15, 15);
            gb.Size = new Size(440, 60);
            string[] names = new string[] { "15", "30", "45", "60", "90", "120" };
            int[] mins = new int[] { 15, 30, 45, 60, 90, 120 };
            RadioButton[] rbs = new RadioButton[6];
            int cur = Store.GetDurationMinutes();
            for (int i = 0; i < 6; i++)
            {
                rbs[i] = new RadioButton();
                rbs[i].Text = names[i] + "分";
                rbs[i].AutoSize = true;
                rbs[i].Location = new Point(15 + i * 70, 25);
                rbs[i].Tag = mins[i];
                if (mins[i] == cur) rbs[i].Checked = true;
                gb.Controls.Add(rbs[i]);
            }
            Controls.Add(gb);
            RadioButton[] rbRef = rbs;

            Button saveDur = new Button();
            saveDur.Text = "保存时长";
            saveDur.Location = new Point(340, 84);
            saveDur.Size = new Size(115, 30);
            saveDur.Click += delegate
            {
                for (int i = 0; i < rbRef.Length; i++)
                {
                    if (rbRef[i].Checked)
                    {
                        Store.SetDurationMinutes((int)rbRef[i].Tag);
                        App.Instance.ResetRemaining();
                        MessageBox.Show(this, "已保存。本次剩余时间已重置。", "电脑锁",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    }
                }
            };
            Controls.Add(saveDur);

            // ---- 修改家长 PIN ----
            GroupBox gp = new GroupBox();
            gp.Text = "修改家长 PIN（锁屏界面\"家长入口\"用）";
            gp.Location = new Point(15, 124);
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
                if (pin1.Text.Length < 4)
                {
                    MessageBox.Show(this, "PIN 至少 4 位。", "电脑锁");
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
            gu.Location = new Point(15, 224);
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
                if (up1.Text.Length < 4)
                {
                    MessageBox.Show(this, "卸载密码至少 4 位。", "电脑锁");
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

            // ---- 操作按钮 ----
            Button btnLock = new Button();
            btnLock.Text = "立即锁定";
            btnLock.Location = new Point(15, 330);
            btnLock.Size = new Size(140, 34);
            btnLock.Click += delegate
            {
                Close();
                App.Instance.LockNow();
            };

            Button btnExit = new Button();
            btnExit.Text = "退出程序";
            btnExit.Location = new Point(165, 330);
            btnExit.Size = new Size(140, 34);
            btnExit.Click += delegate
            {
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
            btnRemove.Location = new Point(315, 330);
            btnRemove.Size = new Size(140, 34);
            btnRemove.Click += delegate
            {
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

            Label tip = new Label();
            tip.Text = "说明：锁屏后需全部答对 10 道口算题（答错清零重来），或由家长输入 PIN 解锁。";
            tip.ForeColor = Color.Gray;
            tip.AutoSize = true;
            tip.Location = new Point(15, 400);
            Controls.Add(tip);
        }

        static TextBox NewBox(int x, int y)
        {
            TextBox b = new TextBox();
            b.Location = new Point(x, y);
            b.Width = 120;
            b.PasswordChar = '*';
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
