using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
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
            // 置顶：从置顶的家长设置窗口内打开，必须同样 TopMost 才不会被压到底层
            TopMost = true;
            ClientSize = new Size(440, 270);
            this.Icon = Program.LoadAppIcon();

            Label name = new Label();
            name.Text = Strings.Fmt("电脑锁 v{0} ({1})", AppVersion.Version, AppVersion.Commit);
            name.Font = new Font("Microsoft YaHei", 14, FontStyle.Bold);
            name.AutoSize = true;
            name.Location = new Point(20, 20);

            Label free = new Label();
            free.Text = Strings.Get("本程序免费。");
            free.Font = new Font("Microsoft YaHei", 11);
            free.AutoSize = true;
            free.Location = new Point(20, 66);

            Label author = new Label();
            author.Text = Strings.Get("作者：bgaidu、AI");
            author.Font = new Font("Microsoft YaHei", 11);
            author.AutoSize = true;
            author.Location = new Point(20, 96);

            Label desc = new Label();
            desc.Text = Strings.Fmt("功能说明：锁屏后需全部答对 {0} 道题（数学填答案，语文/英语四选一，答错清零重来），或由家长输入 PIN 解锁。",
                Constants.TotalQuestions);
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
            Program.Localize(this);
            Program.FlattenButtons(this);
        }
    }
}
