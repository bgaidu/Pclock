using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>P1-10 最近 7 天使用记录：柱状图 + CSV 导出（数据全部来自本地注册表）</summary>
    public class UsageForm : Form
    {
        string user;
        int[] seconds;
        DateTime[] days;
        Panel chart;

        public UsageForm()
        {
            Text = "使用记录 - 最近 7 天";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(560, 340);
            this.Icon = Program.LoadAppIcon();

            user = (App.Instance != null) ? App.Instance.CurrentUser : Store.GetCurrentUser();
            seconds = Store.GetUsageLastDays(user, DateTime.Now, Constants.UsageHistoryDays);
            days = UsageCsv.LastNDays(DateTime.Now, Constants.UsageHistoryDays);

            Label tip = new Label();
            tip.Text = "用户「" + user + "」的实际使用时长（不含锁屏与答题时间）";
            tip.AutoSize = true;
            tip.Location = new Point(16, 14);
            Controls.Add(tip);

            chart = new Panel();
            chart.Location = new Point(16, 44);
            chart.Size = new Size(528, 210);
            chart.BackColor = Color.White;
            chart.Paint += DrawChart;
            Controls.Add(chart);

            Label total = new Label();
            total.Text = "今日已用：" + (Store.GetUsedToday(user) / 60) + " 分 " +
                (Store.GetUsedToday(user) % 60) + " 秒";
            total.AutoSize = true;
            total.Location = new Point(16, 268);
            Controls.Add(total);

            Button export = new Button();
            export.Text = "导出 CSV";
            export.Location = new Point(310, 290);
            export.Size = new Size(110, 32);
            export.Click += delegate { ExportCsv(); };
            Controls.Add(export);

            Button close = new Button();
            close.Text = "关闭";
            close.Location = new Point(430, 290);
            close.Size = new Size(110, 32);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            Program.FlattenButtons(this);
        }

        void DrawChart(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int max = 1;
            foreach (int s in seconds) if (s > max) max = s;
            int w = chart.ClientSize.Width;
            int h = chart.ClientSize.Height;
            int slot = w / seconds.Length;
            int baseY = h - 28;
            using (Pen axis = new Pen(Color.Gainsboro))
                g.DrawLine(axis, 0, baseY, w, baseY);
            using (Font f = new Font("Microsoft YaHei", 8))
            {
                for (int i = 0; i < seconds.Length; i++)
                {
                    int barH = (int)((h - 60) * (seconds[i] / (double)max));
                    int x = i * slot + 8;
                    int bw = slot - 16;
                    using (Brush b = new SolidBrush(seconds[i] > 0 ? Color.SteelBlue : Color.LightGray))
                        g.FillRectangle(b, x, baseY - Math.Max(barH, 2), bw, Math.Max(barH, 2));
                    string label = days[i].ToString("MM-dd") + "\n" + (seconds[i] / 60) + "分";
                    g.DrawString(label, f, Brushes.DimGray, x, baseY + 4);
                }
            }
        }

        void ExportCsv()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "CSV 文件|*.csv";
                d.FileName = "PCLock-usage-" + DateTime.Now.ToString("yyyyMMdd") + ".csv";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(d.FileName, UsageCsv.Build(DateTime.Now, seconds), new UTF8Encoding(true));
                    AuditLog.Write("使用记录导出 CSV：" + d.FileName);
                    MessageBox.Show(this, "已导出到：" + d.FileName, "电脑锁",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导出失败：" + ex.Message, "电脑锁");
                }
            }
        }
    }
}
