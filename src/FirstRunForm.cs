using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>
    /// P0-1 首次运行强制设置家长 PIN 与卸载密码（消除默认 1234）。
    /// 旧安装（PinSetupDone 缺省为 1）不会弹此窗口；新安装不设置完成就不进入主程序。
    /// </summary>
    public class FirstRunForm : Form
    {
        TextBox pin1, pin2, un1, un2;
        bool done;

        public FirstRunForm()
        {
            Text = "电脑锁 - 首次使用，请设置密码";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(430, 300);

            Label tip = new Label();
            tip.Text = "首次使用必须设置两个密码，且不能沿用默认 1234。\n" + Constants.PinRuleText;
            tip.AutoSize = false;
            tip.Size = new Size(400, 46);
            tip.Location = new Point(15, 12);
            Controls.Add(tip);

            AddRow("家长 PIN", 70, out pin1);
            AddRow("再输一遍", 120, out pin2);
            AddRow("卸载密码", 170, out un1);
            AddRow("再输一遍", 220, out un2);

            Button ok = new Button();
            ok.Text = "保存并开始使用";
            ok.Location = new Point(245, 258);
            ok.Size = new Size(170, 32);
            ok.Click += delegate { Save(); };
            Controls.Add(ok);
            AcceptButton = ok;
            Program.Localize(this);
            Program.FlattenButtons(this);
        }

        void AddRow(string caption, int y, out TextBox box)
        {
            Label l = new Label();
            l.Text = caption + "：";
            l.AutoSize = true;
            l.Location = new Point(15, y + 4);
            Controls.Add(l);

            TextBox t = new TextBox();
            t.Location = new Point(120, y);
            t.Size = new Size(295, 24);
            t.PasswordChar = '*';
            t.MaxLength = Constants.PinMaxLength;
            Controls.Add(t);
            box = t;
        }

        void Save()
        {
            string p = pin1.Text, p2 = pin2.Text, u = un1.Text, u2 = un2.Text;
            if (!Constants.IsValidPin(p) || !Constants.IsValidPin(u))
            {
                MessageBox.Show(this, Strings.Get(Constants.PinRuleText), Strings.Get("电脑锁")); return;
            }
            if (p != p2 || u != u2)
            {
                MessageBox.Show(this, Strings.Get("两次输入不一致，请重新确认。"), Strings.Get("电脑锁")); return;
            }
            if (p == Constants.DefaultPin || u == Constants.DefaultUninstallPin)
            {
                MessageBox.Show(this, Strings.Get("不能继续使用默认密码 1234，请设置自己的密码。"), Strings.Get("电脑锁")); return;
            }
            if (p == u)
            {
                MessageBox.Show(this, Strings.Get("家长 PIN 与卸载密码建议设置为不同值。"), Strings.Get("电脑锁")); return;
            }
            Store.SetPin(p);
            Store.SetUninstallPin(u);
            Store.MarkFirstPinSetupDone();
            AuditLog.Write("首次运行：家长 PIN 与卸载密码已由家长设置");
            done = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 不允许用右上角关闭跳过强制设置（除非关机）
            if (!done && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                MessageBox.Show(this, Strings.Get("首次使用必须设置密码后才能开始使用电脑锁。"), Strings.Get("电脑锁"));
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
