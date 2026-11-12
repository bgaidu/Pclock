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
        Action langRefresh;

        public UsageForm()
        {
            Text = "使用记录 - 最近 7 天";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(560, 420);
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
            total.Text = Strings.Fmt("今日已用：{0} 分 {1} 秒", Store.GetUsedToday(user) / 60,
                Store.GetUsedToday(user) % 60);
            total.AutoSize = true;
            total.Location = new Point(16, 262);
            Controls.Add(total);

            Button export = new Button();
            export.Text = "导出 CSV";
            export.Location = new Point(310, 376);
            export.Size = new Size(110, 32);
            export.Click += delegate { ExportCsv(); };
            Controls.Add(export);

            Button close = new Button();
            close.Text = "关闭";
            close.Location = new Point(430, 376);
            close.Size = new Size(110, 32);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            // 30 天汇总 / 弱项 / 崩溃日志本地聚合（不上传）
            int[] month = Store.GetUsageLastDays(user, DateTime.Now, 30);
            long monthTotal = 0;
            foreach (int s in month) monthTotal += s;
            string[] keys = Store.ListStatKeys(user);
            string[] vals = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++) vals[i] = Store.GetQuestionStat(user, keys[i]);
            string weak = QuestionStats.Weakest(keys, vals, 3);
            string crashes = "";
            try
            {
                string errPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "err.txt");
                if (File.Exists(errPath))
                    crashes = string.Join("；", ConfigIo.CrashSummary(File.ReadAllText(errPath)));
            }
            catch (Exception) { }
            Label summary = new Label();
            summary.AutoSize = false;
            summary.Size = new Size(528, 78);
            summary.Location = new Point(16, 288);
            summary.ForeColor = Color.DimGray;
            summary.Text = SummaryText(monthTotal, weak, crashes);
            Controls.Add(summary);

            Program.Localize(this);
            Program.FlattenButtons(this);

            langRefresh = delegate
            {
                total.Text = Strings.Fmt("今日已用：{0} 分 {1} 秒", Store.GetUsedToday(user) / 60,
                    Store.GetUsedToday(user) % 60);
                summary.Text = SummaryText(monthTotal, weak, crashes);
            };
            Program.RegisterDynamicRefresh(langRefresh);
        }

        /// <summary>30 天汇总用整句模板，避免英文模式下出现「Past 30 days: 12 小时」式中英混排</summary>
        static string SummaryText(long monthTotal, string weak, string crashes)
        {
            return Strings.Fmt("近 30 天合计：{0} 小时 {1} 分\n最弱题目（作答≥3 次）：{2}\n本地异常摘要：{3}",
                monthTotal / 3600, (monthTotal % 3600) / 60,
                (weak == "" ? Strings.Get("暂无数据") : weak),
                (crashes == "" ? Strings.Get("无 err.txt") : crashes));
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
                    MessageBox.Show(this, Strings.Fmt("已导出到：{0}", d.FileName), Strings.Get("电脑锁"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Strings.Get("导出失败：") + ex.Message, Strings.Get("电脑锁"));
                }
            }
        }
    }
}
