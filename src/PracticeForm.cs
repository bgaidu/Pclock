using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>
    /// P1-9 练习模式：自由答题、即时反馈与统计。
    /// 关键约束：不解锁、不重置倒计时、不消耗额度；模态对话框期间主计时器照常运行，
    /// 因此练习不会变成零成本绕过口子（借鉴 PoHW「练习页挂起弹题但守护不停」）。
    /// </summary>
    public class PracticeForm : Form
    {
        Label promptLabel, feedbackLabel, statsLabel, meaningLabel;
        TextBox answerBox;
        Button submitBtn;
        Button[] optBtns = new Button[4];
        List<Question> batch;
        int idx;
        int practiced, correct, streak;
        Question current;
        int[] grades;
        Subject[] subs;

        public PracticeForm()
        {
            Text = "练习模式（不计入解锁，倒计时继续）";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(620, 430);
            this.Icon = Program.LoadAppIcon();

            string user = Store.GetCurrentUser();
            int[] gi = Store.GetGrades(user);
            grades = gi;
            int[] si = Store.GetSubjects(user);
            subs = new Subject[si.Length];
            for (int i = 0; i < si.Length; i++) subs[i] = (Subject)si[i];

            Label tip = new Label();
            tip.Text = "练习模式：答对/答错都会显示正确答案，可随时换题或退出。";
            tip.AutoSize = true;
            tip.Location = new Point(18, 12);
            Controls.Add(tip);

            promptLabel = new Label();
            promptLabel.Font = new Font("Microsoft YaHei", 22, FontStyle.Bold);
            promptLabel.AutoSize = true;
            promptLabel.Location = new Point(18, 55);
            Controls.Add(promptLabel);

            meaningLabel = new Label();
            meaningLabel.Font = new Font("Microsoft YaHei", 12);
            meaningLabel.ForeColor = Color.DimGray;
            meaningLabel.AutoSize = true;
            meaningLabel.Location = new Point(18, 110);
            Controls.Add(meaningLabel);

            answerBox = new TextBox();
            answerBox.Font = new Font("Microsoft YaHei", 16);
            answerBox.Size = new Size(150, 32);
            answerBox.Location = new Point(18, 150);
            answerBox.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
                if (e.KeyChar == (char)13) Submit();
            };
            Controls.Add(answerBox);

            submitBtn = new Button();
            submitBtn.Text = "确定";
            submitBtn.Location = new Point(180, 148);
            submitBtn.Size = new Size(90, 34);
            submitBtn.Click += delegate { Submit(); };
            Controls.Add(submitBtn);

            for (int i = 0; i < 4; i++)
            {
                optBtns[i] = new Button();
                optBtns[i].Size = new Size(560, 40);
                optBtns[i].Location = new Point(18, 148 + i * 48);
                optBtns[i].Tag = i;
                optBtns[i].Click += OnOption;
                Controls.Add(optBtns[i]);
            }

            feedbackLabel = new Label();
            feedbackLabel.Font = new Font("Microsoft YaHei", 12);
            feedbackLabel.AutoSize = true;
            feedbackLabel.Location = new Point(18, 348);
            Controls.Add(feedbackLabel);

            statsLabel = new Label();
            statsLabel.Font = new Font("Microsoft YaHei", 11);
            statsLabel.AutoSize = true;
            statsLabel.Location = new Point(18, 378);
            Controls.Add(statsLabel);

            Button next = new Button();
            next.Text = "换一题";
            next.Location = new Point(410, 374);
            next.Size = new Size(80, 30);
            next.Click += delegate { Next(); };
            Controls.Add(next);

            Button close = new Button();
            close.Text = "退出练习";
            close.Location = new Point(500, 374);
            close.Size = new Size(100, 30);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            Program.Localize(this);
            Program.FlattenButtons(this);
            Next();
        }

        void Next()
        {
            if (batch == null || idx >= batch.Count)
            {
                batch = QuestionBank.GetBatch(grades, subs, 10);
                idx = 0;
            }
            current = batch[idx++];
            promptLabel.Text = current.Prompt;
            meaningLabel.Text = (current.Type == QType.ChinesePinyin) ? current.Explanation : "";
            feedbackLabel.Text = "";
            answerBox.Text = "";
            bool math = current.Type == QType.MathCalc;
            answerBox.Visible = math;
            submitBtn.Visible = math;
            for (int i = 0; i < 4; i++)
            {
                optBtns[i].Visible = !math && i < (current.Options == null ? 0 : current.Options.Length);
                if (optBtns[i].Visible) optBtns[i].Text = current.Options[i];
            }
            UpdateStats();
        }

        void Submit()
        {
            if (current == null || current.Type != QType.MathCalc) return;
            int v;
            if (!int.TryParse(answerBox.Text.Trim(), out v))
            {
                feedbackLabel.Text = "请输入数字答案";
                return;
            }
            Judge(v == current.Answer);
        }

        void OnOption(object sender, EventArgs e)
        {
            if (current == null || current.Type == QType.MathCalc) return;
            int i = (int)((Button)sender).Tag;
            Judge(i == current.CorrectIndex);
        }

        void Judge(bool ok)
        {
            Question answered = current;
            practiced++;
            if (ok) { correct++; streak++; }
            else streak = 0;
            // 答对自动下一题（先换题再写反馈，避免 Next 清空反馈）；答错保留反馈由「换一题」继续
            if (ok) Next();
            feedbackLabel.Text = (ok ? "答对了！" : "答错了。") + " " + AnswerFeedback.Build(answered);
            feedbackLabel.ForeColor = ok ? Color.ForestGreen : Color.Firebrick;
            UpdateStats();
        }

        void UpdateStats()
        {
            statsLabel.Text = Strings.Fmt("已练 {0} 题 · 答对 {1} 题 · 连对 {2} 题", practiced, correct, streak);
        }
    }
}
