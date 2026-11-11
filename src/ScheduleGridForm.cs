using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>按周 × 小时的可视化时段编辑器（借鉴 StudyOnTV/CleanTV 的格子面板），生成多规则串。</summary>
    public class ScheduleGridForm : Form
    {
        readonly bool[,] grid = new bool[7, 24];
        readonly string[] dayNames = new string[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
        GridPanel panel;
        CheckBox quizOnly;
        TextBox result;

        public string RuleText { get { return result == null ? "" : result.Text; } }

        public ScheduleGridForm()
        {
            Text = "时段格子编辑器";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(680, 330);
            this.Icon = Program.LoadAppIcon();

            Label tip = new Label();
            tip.Text = "点击格子切换：深色 = 管控生效时段；每行一天、每列一小时。";
            tip.AutoSize = true;
            tip.Location = new Point(16, 12);
            Controls.Add(tip);

            panel = new GridPanel(grid, dayNames);
            panel.Location = new Point(16, 38);
            panel.Size = new Size(648, 210);
            panel.ClickCell += delegate { result.Text = BuildRule(); };
            Controls.Add(panel);

            quizOnly = new CheckBox();
            quizOnly.Text = "仅时段内需要答题（时段外自由使用）";
            quizOnly.AutoSize = true;
            quizOnly.Location = new Point(16, 256);
            quizOnly.CheckedChanged += delegate { result.Text = BuildRule(); };
            Controls.Add(quizOnly);

            result = new TextBox();
            result.Location = new Point(16, 282);
            result.Size = new Size(500, 24);
            result.ReadOnly = true;
            Controls.Add(result);

            Button ok = new Button();
            ok.Text = "使用此规则";
            ok.Location = new Point(524, 280);
            ok.Size = new Size(140, 28);
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);
            AcceptButton = ok;

            Program.FlattenButtons(this);
            result.Text = BuildRule();
        }

        string BuildRule() { return ConfigIo.ScheduleGridToRule(grid, quizOnly.Checked); }

        sealed class GridPanel : Panel
        {
            readonly bool[,] g;
            readonly string[] names;
            const int GX = 46, GY = 22, CellW = 25, CellH = 25;

            public event Action ClickCell;

            public GridPanel(bool[,] grid, string[] dayNames)
            {
                g = grid; names = dayNames;
                BackColor = Color.White;
                DoubleBuffered = true;
                MouseDown += OnMouse;
            }

            void OnMouse(object sender, MouseEventArgs e)
            {
                int col = (e.X - GX) / CellW;
                int row = (e.Y - GY) / CellH;
                if (row < 0 || row > 6 || col < 0 || col > 23) return;
                g[row, col] = !g[row, col];
                Invalidate();
                if (ClickCell != null) ClickCell();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics gr = e.Graphics;
                using (Font f = new Font("Microsoft YaHei", 9))
                {
                    for (int c = 0; c < 24; c++)
                    {
                        int x = GX + c * CellW;
                        if (c % 2 == 0) gr.DrawString(c.ToString(), f, Brushes.DimGray, x, 2);
                    }
                    for (int r = 0; r < 7; r++)
                    {
                        int y = GY + r * CellH;
                        gr.DrawString(names[r], f, Brushes.DimGray, 2, y + 4);
                        for (int c = 0; c < 24; c++)
                        {
                            Rectangle rect = new Rectangle(GX + c * CellW, y, CellW - 1, CellH - 1);
                            using (Brush b = new SolidBrush(g[r, c] ? Color.FromArgb(70, 130, 200) : Color.FromArgb(238, 242, 248)))
                                gr.FillRectangle(b, rect);
                            gr.DrawRectangle(Pens.LightGray, rect);
                        }
                    }
                }
            }
        }
    }
}
