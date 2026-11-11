using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>
    /// 全屏锁屏窗口：覆盖所有显示器、置顶、屏蔽 Win/Alt+Tab/Ctrl+Esc 等系统快捷键。
    /// 一次锁屏共 10 道互不重复的三年级口算题，全部答对才能解锁；右下角"家长入口"用 PIN 快速解锁。
    /// </summary>
    public class LockForm : Form
    {
        public event EventHandler UnlockRequested;
        public bool AllowClose;
        /// <summary>本次解锁方式（答题/PIN/时段自动），由 App.Unlock 写入审计日志</summary>
        public string UnlockReason = "未知";
        /// <summary>家长对话框（PIN 验证 / 家长设置）打开期间为 true：暂停锁屏的"抢焦点/抢置顶"，
        /// 否则 UiTick 每 500ms 的 Activate() 会把家长设置窗口重新压到锁屏底层、导致无法操作。</summary>
        public bool SuspendInteraction { get; set; }

        Label titleLabel;
        Label questionLabel;
        Label statusLabel;
        TextBox answerBox;
        Button submitBtn;
        Button[] optBtns = new Button[4];
        Label meaningLabel;
        Button parentBtn;
        Button settingsBtn;   // 锁屏界面上的"家长设置"入口（验证 PIN 后打开设置窗口）
        Panel parentPanel;
        Label pinLabel;
        TextBox pinBox;
        Button pinOkBtn;
        Button pinBackBtn;

        int correctCount;   // 本轮连续答对题数
        int qIndex;         // 当前第几题
        List<Question> questions;
        List<Question> correctQuestions = new List<Question>(); // 本轮答对题（星级奖励结算用）
        int baseQuestionCount;  // 普通题数量；之后的是加练题
        int requiredStreak;     // 需连续答对多少道（默认 = 题量，即全部答对）
        string user;
        string hardReason;       // 非空 = 额度用尽/护眼休息/时间异常等硬锁（不自动按时段解锁）
        int wrongLeft;      // 答错后的冷却秒数
        int pinFails;
        int pinCooldown;
        bool pinThreadRunning = false;   // PIN 验证在后台线程进行，避免 PBKDF2 卡 UI
        int lastCooldownTick;   // Environment.TickCount：冷却按真实秒数递减（uiTimer 每 500ms 触发）
        LowLevelHook hook;
        Timer uiTimer;
        bool dueToWindow;   // true=因"不在每日可用时段"而锁（硬锁）
        bool autoUnlocked;  // 自动解锁只触发一次

        Label feedbackLabel;    // 答错后的正确答案 / 解析
        Button continueBtn;    // 孩子确认反馈后继续
        Button practiceBtn;    // 练习模式（不解锁、不消耗额度）
        Button tempPassBtn;     // 家长临时放行 N 分钟
        Label restLabel;        // 护眼休息倒计时

        public LockForm(bool dueToWindow = false, string hardReason = null)
        {
            this.dueToWindow = dueToWindow;
            this.hardReason = hardReason;
            this.user = (App.Instance != null) ? App.Instance.CurrentUser : Store.GetCurrentUser();
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Rectangle vs = SystemInformation.VirtualScreen;
            Location = vs.Location;
            Size = vs.Size;
            BackColor = Color.FromArgb(12, 18, 32);
            TopMost = true;
            ShowInTaskbar = false;

            BuildTitleLabels();
            BuildAnswerControls();
            BuildParentEntry();
            BuildParentSettingsEntry();
            BuildOptionButtons();
            BuildExtraControls();
            AddCoreControls();

            Resize += delegate { LayoutControls(); };

            uiTimer = new Timer();
            uiTimer.Interval = Constants.UiTimerIntervalMs;
            uiTimer.Tick += UiTick;
            uiTimer.Start();

            if (dueToWindow)
            {
                ApplyDueToWindowUI();
            }
            else
            {
                StartBatch();
            }
            LayoutControls();

            // 低级钩子初始化放在最后，确保前面初始化失败时也能正确清理
            hook = new LowLevelHook();

            // 扁平化按钮，规避视觉样式绘制期偶发的"创建窗口句柄"失败
            Program.FlattenButtons(this);
        }

        void BuildTitleLabels()
        {
            titleLabel = MakeLabel("时间已到，已锁定", 30, FontStyle.Bold, Color.White);
            questionLabel = MakeLabel("", 44, FontStyle.Bold, Color.FromArgb(120, 200, 255));
            statusLabel = MakeLabel("", 14, FontStyle.Regular, Color.Silver);
        }

        void BuildAnswerControls()
        {
            answerBox = new TextBox();
            answerBox.Font = new Font("Microsoft YaHei", 20);
            answerBox.Size = new Size(180, 48);
            answerBox.TextAlign = HorizontalAlignment.Center;
            answerBox.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
                if (e.KeyChar == (char)13) SubmitAnswer();
            };

            submitBtn = new Button();
            submitBtn.Text = "确 定";
            submitBtn.Font = new Font("Microsoft YaHei", 13, FontStyle.Bold);
            submitBtn.Size = new Size(110, 48);
            // 白底黑字，锁屏深色背景上高对比度易识别
            submitBtn.FlatStyle = FlatStyle.Flat;
            submitBtn.FlatAppearance.BorderColor = Color.FromArgb(60, 70, 90);
            submitBtn.FlatAppearance.BorderSize = 1;
            submitBtn.BackColor = Color.White;
            submitBtn.ForeColor = Color.Black;
            submitBtn.Click += delegate { SubmitAnswer(); };
        }

        void BuildParentEntry()
        {
            parentBtn = new Button();
            parentBtn.Text = "家长入口";
            parentBtn.Font = new Font("Microsoft YaHei", 9);
            parentBtn.AutoSize = true;
            parentBtn.ForeColor = Color.DimGray;
            parentBtn.Click += delegate { parentPanel.Visible = true; pinBox.Focus(); };

            parentPanel = new Panel();
            parentPanel.BorderStyle = BorderStyle.FixedSingle;
            parentPanel.Size = new Size(380, 195);
            parentPanel.BackColor = Color.FromArgb(30, 38, 60);

            pinLabel = MakeLabel("家长 PIN：", 12, FontStyle.Regular, Color.White);
            pinLabel.Location = new Point(20, 18);

            pinBox = new TextBox();
            pinBox.Font = new Font("Microsoft YaHei", 14);
            pinBox.Location = new Point(20, 52);
            pinBox.Width = 330;
            pinBox.PasswordChar = '*';
            pinBox.MaxLength = Constants.PinMaxLength;
            pinBox.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                // 允许数字/字母/特殊符号（规则见 Constants.IsValidPin），仅回车触发验证
                if (e.KeyChar == (char)13) TryParentPin();
            };

            pinOkBtn = new Button();
            pinOkBtn.Text = "解锁";
            pinOkBtn.Location = new Point(20, 100);
            pinOkBtn.Size = new Size(120, 34);
            pinOkBtn.Click += delegate { TryParentPin(); };

            pinBackBtn = new Button();
            pinBackBtn.Text = "返回";
            pinBackBtn.Location = new Point(230, 100);
            pinBackBtn.Size = new Size(120, 34);
            pinBackBtn.Click += delegate { parentPanel.Visible = false; };

            // P0-5：家长可只临时放行 N 分钟，而不是重置为完整一轮时长
            tempPassBtn = new Button();
            tempPassBtn.Text = "临时放行 " + Store.GetTempPassMinutes(user) + " 分钟";
            tempPassBtn.Font = new Font("Microsoft YaHei", 9);
            tempPassBtn.Location = new Point(20, 145);
            tempPassBtn.Size = new Size(330, 34);
            tempPassBtn.Click += delegate { TryParentPin(true); };

            parentPanel.Controls.Add(pinLabel);
            parentPanel.Controls.Add(pinBox);
            parentPanel.Controls.Add(pinOkBtn);
            parentPanel.Controls.Add(pinBackBtn);
            parentPanel.Controls.Add(tempPassBtn);
            parentPanel.Visible = false;
        }

        /// <summary>
        /// 锁屏界面左下角的"家长设置"入口：验证家长 PIN 后打开设置窗口（置顶于锁屏之上）。
        /// 锁屏全屏覆盖且 TopMost，普通对话框会被压到底层，故打开期间暂停锁屏抢焦点
        /// （SuspendInteraction），并让对话框自身 TopMost，保证家长设置清晰可见、可操作。
        /// </summary>
        void BuildParentSettingsEntry()
        {
            settingsBtn = new Button();
            settingsBtn.Text = "家长设置";
            settingsBtn.Font = new Font("Microsoft YaHei", 9);
            settingsBtn.AutoSize = true;
            settingsBtn.ForeColor = Color.DimGray;
            settingsBtn.Click += delegate
            {
                this.SuspendInteraction = true;
                try
                {
                    bool ok = false;
                    using (PinDialog pd = new PinDialog("家长验证", "请输入家长 PIN：",
                        delegate(string s) { return Store.VerifyPin(s); }))
                    {
                        ok = (pd.ShowDialog(this) == DialogResult.OK);
                    }
                    if (ok)
                    {
                        using (SettingsForm sf = new SettingsForm()) sf.ShowDialog(this);
                    }
                }
                finally
                {
                    this.SuspendInteraction = false;
                }
            };
        }

        void BuildOptionButtons()
        {
            // 选择题的 4 个选项按钮（拼音/汉语），默认隐藏，数学题时不显示
            for (int i = 0; i < 4; i++)
            {
                optBtns[i] = new Button();
                optBtns[i].Font = new Font("Microsoft YaHei", 16);
                optBtns[i].Size = new Size(380, 50);
                optBtns[i].FlatStyle = FlatStyle.Flat;
                optBtns[i].FlatAppearance.BorderColor = Color.FromArgb(60, 70, 90);
                optBtns[i].BackColor = Color.FromArgb(28, 38, 60);
                optBtns[i].ForeColor = Color.White;
                optBtns[i].Visible = false;
                optBtns[i].Click += OnOptionClick;
                Controls.Add(optBtns[i]);
            }
            // 语文词义解释
            meaningLabel = MakeLabel("", 16, FontStyle.Regular, Color.FromArgb(170, 210, 170));
            meaningLabel.Visible = false;
            Controls.Add(meaningLabel);
        }

        /// <summary>P0-2 / P0-5 / P1-9：答题反馈、继续按钮、练习入口、休息倒计时</summary>
        void BuildExtraControls()
        {
            feedbackLabel = MakeLabel("", 15, FontStyle.Regular, Color.FromArgb(255, 214, 140));
            feedbackLabel.Visible = false;
            Controls.Add(feedbackLabel);

            continueBtn = new Button();
            continueBtn.Text = "继续答题";
            continueBtn.Font = new Font("Microsoft YaHei", 11);
            continueBtn.Size = new Size(140, 34);
            continueBtn.Visible = false;
            continueBtn.Click += delegate
            {
                feedbackLabel.Visible = false;
                continueBtn.Visible = false;
                ShowCurrentQuestion();
                UpdateStatus();
            };
            Controls.Add(continueBtn);

            practiceBtn = new Button();
            practiceBtn.Text = "练习模式";
            practiceBtn.Font = new Font("Microsoft YaHei", 9);
            practiceBtn.AutoSize = true;
            practiceBtn.ForeColor = Color.DimGray;
            practiceBtn.Visible = Store.IsPracticeEnabled(user);
            practiceBtn.Click += delegate
            {
                this.SuspendInteraction = true;
                try { if (App.Instance != null) App.Instance.OpenPractice(); }
                finally { this.SuspendInteraction = false; }
            };
            Controls.Add(practiceBtn);

            restLabel = MakeLabel("", 18, FontStyle.Bold, Color.FromArgb(170, 220, 255));
            restLabel.Visible = false;
            Controls.Add(restLabel);
        }

        void AddCoreControls()
        {
            Controls.Add(titleLabel);
            Controls.Add(questionLabel);
            Controls.Add(statusLabel);
            Controls.Add(answerBox);
            Controls.Add(submitBtn);
            Controls.Add(parentBtn);
            Controls.Add(settingsBtn);
            Controls.Add(parentPanel);
        }

        void ApplyDueToWindowUI()
        {
            if (!string.IsNullOrEmpty(hardReason))
            {
                titleLabel.Text = hardReason;
                string rules = Store.GetRules(user);
                questionLabel.Text = string.IsNullOrWhiteSpace(rules)
                    ? "请使用家长 PIN 解锁" : ("当前规则：\n" + rules);
                statusLabel.Text = "休息结束后自动解锁，或由家长输入 PIN / 临时放行。";
                restLabel.Visible = hardReason.IndexOf("休息") >= 0;
            }
            else
            {
                titleLabel.Text = "当前不在使用时段";
                string w = Store.GetWindow(user);
                questionLabel.Text = string.IsNullOrWhiteSpace(w) ? "当前不在允许使用时段" : ("允许使用时段：\n" + w);
                statusLabel.Text = "时段开始后自动解锁，或由家长输入 PIN 解锁。";
            }
            answerBox.Visible = false;
            submitBtn.Visible = false;
            meaningLabel.Visible = false;
            foreach (Button b in optBtns) b.Visible = false;
        }

        static Label MakeLabel(string text, float size, FontStyle style, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Microsoft YaHei", size, style);
            l.ForeColor = color;
            l.AutoSize = true;
            l.BackColor = Color.Transparent;
            return l;
        }

        void LayoutControls()
        {
            int cx = ClientSize.Width / 2;
            int y = ClientSize.Height * 18 / 100;
            titleLabel.Left = cx - titleLabel.Width / 2;
            titleLabel.Top = y;
            questionLabel.Left = cx - questionLabel.Width / 2;
            questionLabel.Top = y + 85;

            meaningLabel.Left = cx - meaningLabel.Width / 2;
            meaningLabel.Top = questionLabel.Bottom + 14;

            int bottomOfInput;
            // 注意：不能用 answerBox.Visible 判断分支——首题在构造函数里布局，此时窗体尚未 Show，
            // WinForms 中子控件 Visible 恒返回 false，会导致第一题（数学）的答题框停留在默认 (0,0)。
            // 因此按"当前题目类型"判断，而不是控件可见性。
            bool mathMode = false, pinyinMode = false;
            if (questions != null && qIndex < questions.Count)
            {
                mathMode = (questions[qIndex].Type == QType.MathCalc);
                pinyinMode = (questions[qIndex].Type == QType.ChinesePinyin);
            }
            if (mathMode)
            {
                answerBox.Left = cx - 160;
                answerBox.Top = questionLabel.Bottom + 55;
                submitBtn.Left = answerBox.Right + 24;
                submitBtn.Top = answerBox.Top;
                bottomOfInput = answerBox.Bottom;
            }
            else
            {
                int mTop = questionLabel.Bottom + (pinyinMode ? meaningLabel.Height + 24 : 28);
                for (int i = 0; i < 4; i++)
                {
                    optBtns[i].Left = cx - optBtns[i].Width / 2;
                    optBtns[i].Top = mTop + i * 62;
                }
                bottomOfInput = optBtns[3].Bottom;
            }
            statusLabel.Left = cx - statusLabel.Width / 2;
            statusLabel.Top = bottomOfInput + 40;
            parentBtn.Left = ClientSize.Width - parentBtn.Width - 24;
            parentBtn.Top = ClientSize.Height - parentBtn.Height - 24;
            settingsBtn.Left = 24;
            settingsBtn.Top = ClientSize.Height - settingsBtn.Height - 24;
            parentPanel.Left = cx - parentPanel.Width / 2;
            parentPanel.Top = y + 230;

            feedbackLabel.Left = cx - feedbackLabel.Width / 2;
            feedbackLabel.Top = statusLabel.Bottom + 18;
            continueBtn.Left = cx - continueBtn.Width / 2;
            continueBtn.Top = feedbackLabel.Bottom + 12;
            restLabel.Left = cx - restLabel.Width / 2;
            restLabel.Top = questionLabel.Bottom + 20;
            practiceBtn.Left = 24;
            practiceBtn.Top = ClientSize.Height - practiceBtn.Height - 62;
        }

        void StartBatch()
        {
            int[] grades = Store.GetGrades(user);
            int[] subjInts = Store.GetSubjects(user);
            Subject[] subs = new Subject[subjInts.Length];
            for (int i = 0; i < subjInts.Length; i++) subs[i] = (Subject)subjInts[i];
            int count = Store.GetQuestionCount(user);          // P0-7 题量可配（默认 10）
            requiredStreak = Store.GetRequiredStreak(user);     // 默认等于题量 = 全部答对
            baseQuestionCount = count;
            questions = QuestionBank.GetBatch(grades, subs, count);

            // P0-6 加练池：欠的题追加到本批尾部，必须全部答对才能解锁；跨天/重启保留
            PracticeState ps = Store.GetPracticeState(user);
            if (ps.ExtraPending > 0)
                questions.AddRange(QuestionBank.GetBatch(grades, subs, ps.ExtraPending));

            qIndex = 0;
            correctCount = 0;
            correctQuestions.Clear();
            ShowCurrentQuestion();
            UpdateStatus();
        }

        void ShowCurrentQuestion()
        {
            if (questions == null || questions.Count == 0) return;
            if (qIndex >= questions.Count) qIndex = questions.Count - 1;
            Question q = questions[qIndex];
            feedbackLabel.Visible = false;   // 进入下一题时清掉上一次的答错反馈
            questionLabel.Text = q.Prompt;
            if (q.Type == QType.MathCalc)
            {
                answerBox.Visible = true;
                submitBtn.Visible = true;
                meaningLabel.Visible = false;
                foreach (Button b in optBtns) b.Visible = false;
            }
            else
            {
                answerBox.Visible = false;
                submitBtn.Visible = false;
                meaningLabel.Visible = (q.Type == QType.ChinesePinyin);
                meaningLabel.Text = q.Explanation;
                for (int i = 0; i < 4; i++)
                {
                    optBtns[i].Visible = true;
                    optBtns[i].Text = q.Options[i];
                    optBtns[i].Tag = i;
                }
            }
            LayoutControls();
        }

        void UpdateStatus()
        {
            int cur = Math.Min(qIndex + 1, questions.Count);
            string subj = "练习";
            if (questions != null && qIndex < questions.Count)
            {
                Subject s = questions[qIndex].Subject;
                subj = (s == Subject.Chinese) ? "语文" : (s == Subject.Math) ? "数学" : "英语";
            }
            string extra = (questions.Count > baseQuestionCount)
                ? " + " + (questions.Count - baseQuestionCount) + " 道加练题" : "";
            string phase = (qIndex >= baseQuestionCount && questions.Count > baseQuestionCount) ? "（加练）" : "";
            statusLabel.Text = "共 " + baseQuestionCount + " 题" + extra + "（" + subj + "），需连续答对 " +
                requiredStreak + " 道" + phase + "（当前第 " + cur + " 题，已连对 " + correctCount + " 题）";
        }

        void SubmitAnswer()
        {
            if (wrongLeft > 0) return;
            if (questions[qIndex].Type != QType.MathCalc) return;
            int v;
            if (!int.TryParse(answerBox.Text.Trim(), out v))
            {
                statusLabel.Text = "请输入数字答案";
                answerBox.Focus();
                return;
            }
            if (v == questions[qIndex].Answer) OnCorrect();
            else OnWrong();
            answerBox.Text = "";
            answerBox.Focus();
        }

        void OnOptionClick(object sender, EventArgs e)
        {
            if (wrongLeft > 0) return;
            if (questions[qIndex].Type == QType.MathCalc) return;
            int idx = (int)((Button)sender).Tag;
            if (idx == questions[qIndex].CorrectIndex) OnCorrect();
            else OnWrong();
        }

        void OnCorrect()
        {
            Question answered = questions[qIndex];
            correctQuestions.Add(answered);
            if (qIndex >= baseQuestionCount && questions.Count > baseQuestionCount)
            {
                // 加练题答对：逐道扣除待加练数（跨天 / 重启保留）
                PracticeState ps = ExtraPractice.OnExtraCorrect(Store.GetPracticeState(user));
                Store.SetPracticeState(user, ps);
            }
            correctCount++;
            qIndex++;
            if (qIndex >= questions.Count) { FinishBatch(); return; }
            if (qIndex < baseQuestionCount && correctCount >= requiredStreak)
            {
                // 达到连续答对目标：跳过剩余普通题，进入加练题；没有加练题则直接解锁
                if (questions.Count > baseQuestionCount) qIndex = baseQuestionCount;
                else { FinishBatch(); return; }
            }
            ShowCurrentQuestion();
            UpdateStatus();
        }

        /// <summary>结算本批：星级奖励写入待结算秒数，由 App.Unlock 一次性取出（未启用时恒为 0）</summary>
        void FinishBatch()
        {
            UnlockReason = "答题达标（连续答对 " + correctCount + " 道）";
            int reward = 0;
            if (Store.IsStarRewardEnabled(user))
                reward = StarReward.RewardSeconds(correctQuestions, Store.GetStarRewards(user));
            Store.SetPendingReward(reward);
            if (UnlockRequested != null) UnlockRequested(this, EventArgs.Empty);
        }

        void OnWrong()
        {
            // 防作弊策略不变：答错整批清零、从第 1 题重新开始。
            // 新增：累计错误达阈值时增加加练题（跨天/重启保留），并展示正确答案与解析。
            Question wrong = questions[qIndex];
            PracticeState ps = Store.GetPracticeState(user);
            ps = ExtraPractice.OnWrong(ps, Store.GetExtraThreshold(user), Store.GetExtraPerThreshold(user));
            Store.SetPracticeState(user, ps);

            correctCount = 0;
            correctQuestions.Clear();
            StartBatch();                 // 按最新的加练池重建题目
            wrongLeft = Constants.WrongCooldownSec;
            lastCooldownTick = Environment.TickCount;
            answerBox.Enabled = false;
            submitBtn.Enabled = false;
            foreach (Button b in optBtns) b.Enabled = false;

            if (Store.IsFeedbackEnabled(user))
            {
                feedbackLabel.Text = AnswerFeedback.Build(wrong);
                feedbackLabel.Visible = true;
                continueBtn.Visible = true;
                continueBtn.Enabled = false;   // 冷却结束后才可点
                statusLabel.Text = "回答错误，全部清零重新开始，请 " + Constants.WrongCooldownSec + " 秒后点「继续答题」";
            }
            else
            {
                statusLabel.Text = "回答错误，全部清零重新开始，请 " + Constants.WrongCooldownSec + " 秒后再试";
            }
            LayoutControls();
        }

        void TryParentPin() { TryParentPin(false); }

        void TryParentPin(bool tempPass)
        {
            if (pinCooldown > 0) return;
            if (pinThreadRunning) return;
            string pin = pinBox.Text;
            pinThreadRunning = true;
            pinOkBtn.Enabled = false;
            // PBKDF2 验证约 0.2 秒，放到后台线程避免锁屏界面卡顿
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = false;
                try { ok = Store.VerifyPin(pin); }
                catch (Exception) { }
                try
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        pinThreadRunning = false;
                        if (ok)
                        {
                            if (tempPass)
                            {
                                int minutes = Store.GetTempPassMinutes(user);
                                UnlockReason = "家长临时放行 " + minutes + " 分钟";
                                AuditLog.Write("密码验证成功：锁屏家长入口临时放行 " + minutes + " 分钟");
                                Store.ClearRest();
                                if (App.Instance != null) App.Instance.TempPass(minutes);
                                return;
                            }
                            UnlockReason = "锁屏家长密码";
                            AuditLog.Write("密码验证成功：锁屏家长入口解锁");
                            if (UnlockRequested != null) UnlockRequested(this, EventArgs.Empty);
                            return;
                        }
                        pinFails++;
                        pinBox.Text = "";
                        pinBox.Focus();
                        if (pinFails >= Constants.PinMaxFails)
                        {
                            pinFails = 0;
                            pinCooldown = Constants.PinCooldownSec;
                            lastCooldownTick = Environment.TickCount;
                            pinBox.Enabled = false;
                            pinOkBtn.Enabled = false;
                            AuditLog.Write("密码连续失败：锁屏家长入口进入 " + Constants.PinCooldownSec + " 秒冷却");
                        }
                        else
                        {
                            pinOkBtn.Enabled = true;
                            pinLabel.Text = "PIN 错误（还可尝试 " + (Constants.PinMaxFails - pinFails) + " 次）";
                            AuditLog.Write("密码验证失败：锁屏家长入口（还可尝试 " + (Constants.PinMaxFails - pinFails) + " 次）");
                        }
                    });
                }
                catch (Exception) { pinThreadRunning = false; }
            });
        }

        void UiTick(object sender, EventArgs e)
        {
            // 保持全屏覆盖 + 置顶 + 焦点；但家长对话框（PIN/设置）打开期间暂停，
            // 否则每 500ms 的 Activate() 会把家长设置窗口重新压到锁屏底层
            if (!SuspendInteraction)
            {
                Rectangle vs = SystemInformation.VirtualScreen;
                if (Bounds != vs) { Location = vs.Location; Size = vs.Size; }
                if (!TopMost) TopMost = true;
                if (!ContainsFocus) Activate();
            }

            // 冷却按真实时间递减：uiTimer 每 500ms 触发一次，不能每个 tick 直接减秒
            if (wrongLeft > 0 || pinCooldown > 0)
            {
                int now = Environment.TickCount;
                if (now - lastCooldownTick >= 1000)
                {
                    lastCooldownTick = now;
                    if (wrongLeft > 0)
                    {
                        wrongLeft--;
                        if (wrongLeft == 0)
                        {
                            answerBox.Enabled = true;
                            submitBtn.Enabled = true;
                            foreach (Button b in optBtns) b.Enabled = true;
                            if (continueBtn.Visible) continueBtn.Enabled = true;
                            UpdateStatus();
                        }
                        else
                        {
                            statusLabel.Text = "回答错误，请 " + wrongLeft + " 秒后再试";
                        }
                    }
                    if (pinCooldown > 0)
                    {
                        pinCooldown--;
                        if (pinCooldown == 0)
                        {
                            pinBox.Enabled = true;
                            pinOkBtn.Enabled = true;
                            pinLabel.Text = "家长 PIN：";
                        }
                        else
                        {
                            pinLabel.Text = "PIN 错误次数过多，请 " + pinCooldown + " 秒后再试";
                        }
                    }
                }
            }

            // 护眼休息倒计时（UTC 截止，重启不失效）
            if (restLabel.Visible)
            {
                int left = RestCooldown.RemainingSeconds(Store.GetRestUntilUtc(), DateTime.UtcNow.Ticks);
                restLabel.Text = left > 0
                    ? "还需休息 " + (left / 60) + " 分 " + (left % 60) + " 秒"
                    : "休息结束，正在解锁…";
                restLabel.Left = ClientSize.Width / 2 - restLabel.Width / 2;
            }

            // 硬锁自动解锁：普通时段锁 → 进入允许时段；护眼休息 → 截止时间到；
            // 额度用尽 / 时间异常 → 不自动解锁，必须家长 PIN（避免靠等待绕过）。
            if (dueToWindow && !autoUnlocked && App.Instance != null)
            {
                bool canAuto = false;
                string reason = "进入可用时段自动解锁";
                if (!string.IsNullOrEmpty(hardReason))
                {
                    if (hardReason.IndexOf("休息") >= 0 &&
                        !RestCooldown.IsResting(Store.GetRestUntilUtc(), DateTime.UtcNow.Ticks))
                    {
                        canAuto = true;
                        reason = "护眼休息结束自动解锁";
                    }
                }
                else
                {
                    WindowDecision wd = WindowRules.Evaluate(Store.GetRules(user), DateTime.Now);
                    canAuto = wd.HasRules ? !wd.HardLock : App.Instance.IsWithinWindow();
                }
                if (canAuto)
                {
                    autoUnlocked = true;
                    UnlockReason = reason;
                    if (UnlockRequested != null) UnlockRequested(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TopMost = true;
            Activate();
            LayoutControls();   // 显示后（句柄/字体度量就绪）再重排一次，保证首题位置准确
            if (!dueToWindow) answerBox.Focus();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose && e.CloseReason != CloseReason.ApplicationExitCall &&
                e.CloseReason != CloseReason.WindowsShutDown)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (hook != null) { hook.Dispose(); hook = null; }
            if (uiTimer != null) { uiTimer.Stop(); uiTimer.Dispose(); uiTimer = null; }
        }

        // === 锁屏期间屏蔽系统命令 ===
        // 阻止通过系统命令关闭/最小化/弹出任务栏，锁屏期间无法用系统命令绕过或关闭。
        const int WM_SYSCOMMAND = 0x0112;
        const int SC_CLOSE = 0xF060;
        const int SC_MINIMIZE = 0xF020;
        const int SC_MAXIMIZE = 0xF030;
        const int SC_RESTORE = 0xF120;
        const int SC_TASKLIST = 0xF130;
        const int SC_KEYMENU = 0xF100;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND)
            {
                int w = (int)m.WParam;
                int cmd = w & 0xFFF0;
                if (cmd == SC_CLOSE || cmd == SC_MINIMIZE || cmd == SC_MAXIMIZE ||
                    cmd == SC_RESTORE || cmd == SC_TASKLIST || cmd == SC_KEYMENU)
                {
                    // 锁屏期间禁止通过系统命令关闭/最小化/弹出任务栏
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}
