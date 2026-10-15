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
            // 置顶：锁屏全屏且 TopMost，PIN 验证框必须浮于其上，否则被压到底层无法输入
            TopMost = true;
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
            bool verifying = false;
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
                if (verifying) return;   // 上一次验证仍在后台进行
                string pin = box.Text;
                verifying = true;
                ok.Enabled = false;
                err.Text = "验证中…";
                // PBKDF2 验证约 0.2 秒，放后台线程避免卡住设置界面（与锁屏界面家长入口一致）
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    bool passed = false;
                    try { passed = verify(pin); }
                    catch (Exception) { }
                    try
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            verifying = false;
                            if (passed)
                            {
                                DialogResult = DialogResult.OK;
                                Close();
                                return;
                            }
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
                                ok.Enabled = true;
                            }
                        });
                    }
                    catch (Exception)
                    {
                        // 窗口已关闭等场景：仅重置状态
                        verifying = false;
                        try { ok.Enabled = true; } catch { }
                    }
                });
            };

            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(l);
            Controls.Add(box);
            Controls.Add(err);
            Controls.Add(ok);
            Controls.Add(cancel);
            Program.FlattenButtons(this);
        }
    }
}
