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

        int correctCount;   // 当前星数（已答对题数）：答错扣 1 星，最低 0
        int qIndex;         // 当前第几题
        List<Question> questions;
        List<Question> correctQuestions = new List<Question>(); // 本轮答对题（星级奖励结算用）
        int baseQuestionCount;  // 初始普通题数量
        int normalCount;        // 当前普通题总数（含答错补题）；其后的索引是加练题
        int requiredStreak;     // 需答对多少道（默认 = 题量，即全部答对）
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
        bool restActive;    // 护眼休息中（用字段而非 restLabel.Visible：构造期 getter 不可靠）
        Action langRefresh; // 语言切换时重算动态状态行（Program 只持弱引用）
        bool feedbackPending;       // 答错后正在展示【本题】的正确答案/解析，等孩子点「继续答题」
        Question feedbackQuestion;  // 反馈对应的那道题（切语言时重算解析）
        int pinFailLeft;            // >0 表示家长 PIN 面板正显示「还可尝试 N 次」，切语言时重算
        bool autoUnlocked;  // 自动解锁只触发一次

        Label feedbackLabel;    // 答错后的正确答案 / 解析
        Button continueBtn;    // 孩子确认反馈后继续
        Button practiceBtn;    // 练习模式（不解锁、不消耗额度）
        Button tempPassBtn;     // 家长临时放行 N 分钟
        Label restLabel;        // 护眼休息倒计时
        int selectedGrade;      // 孩子自选年级（0 = 家长允许的全部年级）
        ComboBox gradeBox;

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
            // 小屏 / 高 DPI：题干字号按虚拟屏幕高度缩放，避免长题干溢出
            float scale = Math.Max(0.65f, Math.Min(1.15f, vs.Height / 1080f));
            questionLabel.Font = new Font("Microsoft YaHei", 44f * scale, FontStyle.Bold);
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

            // 静态文案统一保留中文原文，由 Program.Localize 翻译并缓存，
            // 这样切换语言时才能按缓存原文即时重刷（构造期不能预先 Get 成英文）。
            hook = new LowLevelHook();

            // 多语言：统一在控件创建完成后套用，未收录的字符串原样显示中文
            Program.Localize(this);
            Program.FlattenButtons(this);

            // 静态文案由 Program.RefreshLanguage 重刷；这里只重算带数字/规则的状态行
            langRefresh = delegate
            {
                if (dueToWindow) ApplyDueToWindowUI();
                else if (feedbackPending)
                {
                    // 反馈停留期：重算解析与状态行，不能用 UpdateStatus 覆盖「请点继续答题」
                    if (feedbackQuestion != null)
                        feedbackLabel.Text = AnswerFeedback.Build(feedbackQuestion);
                    statusLabel.Text = AnswerFeedback.StatusText(true, wrongLeft);
                }
                else if (questions != null && questions.Count > 0) UpdateStatus();

                // 家长 PIN 面板的运行期提示原先要等下一次操作才更新语言，这里即时重算
                pinLabel.Text = UiText.PinLabel(pinCooldown, pinFailLeft);
            };
            Program.RegisterDynamicRefresh(langRefresh);
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
            parentBtn.Click += delegate
            {
                parentPanel.Visible = true;
                parentPanel.BringToFront();   // 每次打开都确认在最上层，防被题干/反馈层盖住
                // 重开面板时清掉上次的错误提示；冷却中则继续显示冷却文案
                if (pinCooldown <= 0) pinFailLeft = 0;
                pinLabel.Text = UiText.PinLabel(pinCooldown, pinFailLeft);
                pinBox.Focus();
            };

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
            pinOkBtn.Text = "解 锁";
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
                optBtns[i].Name = "QuestionOption";   // 题目选项不是固定文案，Localize 必须跳过
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
                if (!feedbackPending) return;
                wrongLeft = 0;
                // 看清解析后进入下一题：不再重建批次，已得的星保留（答错只扣 1 星）
                AdvanceAfterWrong();
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

            // 孩子自选年级（家长可关闭）：只影响下一批题，当前一轮不重开
            gradeBox = new ComboBox();
            gradeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            gradeBox.Font = new Font("Microsoft YaHei", 9);
            gradeBox.Size = new Size(120, 24);
            gradeBox.Visible = Store.IsChildGradeOn(user);
            gradeBox.Items.Add("全部年级");
            foreach (int g in Store.GetGrades(user)) gradeBox.Items.Add(g + "年级");
            gradeBox.SelectedIndex = 0;
            gradeBox.SelectedIndexChanged += delegate
            {
                string txt = gradeBox.SelectedItem == null ? "" : gradeBox.SelectedItem.ToString();
                int g;
                selectedGrade = (txt.Length > 2 && int.TryParse(txt.Substring(0, txt.Length - 2), out g)) ? g : 0;
            };
            Controls.Add(gradeBox);
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

            // WinForms 中先 Add 的控件在 z-order 上层、后 Add 的在底层。
            // parentPanel 最后加入，会被题干 questionLabel 盖住 —— 点「家长入口」后
            // PIN 输入框看不见也点不到。交互层统一提到最前。
            parentPanel.BringToFront();
            parentBtn.BringToFront();
            settingsBtn.BringToFront();
            practiceBtn.BringToFront();
            gradeBox.BringToFront();
        }

        void ApplyDueToWindowUI()
        {
            if (!string.IsNullOrEmpty(hardReason))
            {
                // hardReason 是 App 运行期传入的中文短语（额度/休息/时间异常）。
                // 必须过词典，否则英文模式下标题漏译；字段本身保留中文，IndexOf("休息") 判断不受影响。
                titleLabel.Text = UiText.HardLockTitle(hardReason);
                questionLabel.Text = UiText.HardLockQuestion(Store.GetRules(user));
                statusLabel.Text = Strings.Get("休息结束后需答题解锁，或由家长输入 PIN / 临时放行。");
                restActive = RestCooldown.IsRestReason(hardReason);
                restLabel.Visible = restActive;
            }
            else
            {
                titleLabel.Text = "当前不在使用时段";
                string w = Store.GetWindow(user);
                questionLabel.Text = UiText.WindowQuestion(w);
                statusLabel.Text = WindowStatusText();
            }
            answerBox.Visible = false;
            submitBtn.Visible = false;
            meaningLabel.Visible = false;
            foreach (Button b in optBtns) b.Visible = false;
        }

        /// <summary>硬锁文案 + 距下次可用还有多久（三家项目共有的家长/孩子提示能力）</summary>
        string WindowStatusText()
        {
            string spec = Store.GetRules(user);
            if (string.IsNullOrWhiteSpace(spec)) spec = Store.GetWindow(user);
            int mins = NextWindow.MinutesUntil(WindowRules.Parse(spec), DateTime.Now);
            return UiText.WindowStatus(mins > 0 ? NextWindow.Format(mins) : "");
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
                // 按当前题实际选项数计算底部（不能用 Visible：窗体未 Show 时恒为 false）
                int optCount = (questions != null && qIndex < questions.Count &&
                    questions[qIndex].Options != null) ? questions[qIndex].Options.Length : 4;
                if (optCount < 2) optCount = 2;
                if (optCount > 4) optCount = 4;
                bottomOfInput = optBtns[optCount - 1].Bottom;
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
            gradeBox.Left = 24;
            gradeBox.Top = ClientSize.Height - practiceBtn.Height - 96;
        }

        /// <summary>按当前设置取题：初始批次与答错补题共用同一套参数，避免两条路径漂移</summary>
        List<Question> NewQuestions(int count, bool useSettings)
        {
            int[] grades = selectedGrade > 0 ? new int[] { selectedGrade } : Store.GetGrades(user);
            int[] subjInts = Store.GetSubjects(user);
            Subject[] subs = new Subject[subjInts.Length];
            for (int i = 0; i < subjInts.Length; i++) subs[i] = (Subject)subjInts[i];
            if (!useSettings)
                return QuestionBank.GetBatchEx(grades, subs, count, 0, 0, "", false, false, false);
            return QuestionBank.GetBatchEx(grades, subs, count,
                Store.GetPreferDifficulty(user), Store.GetTerm(user), Store.GetBankFilter(user),
                Store.IsMathChoiceOn(user), Store.IsChineseWordOn(user), Store.IsTrueFalseOn(user));
        }

        void StartBatch()
        {
            int count = Store.GetQuestionCount(user);          // P0-7 题量可配（默认 10）
            requiredStreak = Store.GetRequiredStreak(user);
            baseQuestionCount = count;
            questions = NewQuestions(count, true);
            // 题库收窄（学期/题库名过滤）后可能不足题量：按实际题数显示，并把「需答对」钳到实际题数，
            // 否则状态栏会写着「共 10 题」而孩子答到第 4 题就无题可答（配置失效且数字对不上）
            if (baseQuestionCount > questions.Count) baseQuestionCount = questions.Count;
            requiredStreak = QuizConfig.EffectiveStreak(requiredStreak, baseQuestionCount);
            // 到期错题优先复习（本地 review-<user>.json，默认关闭）
            if (Store.IsReviewOn(user)) ReviewBank.MixDue(questions, user, count / 3);

            // P0-6 加练池：欠的题追加到本批尾部，必须全部答对才能解锁；跨天/重启保留
            PracticeState ps = Store.GetPracticeState(user);
            if (ps.ExtraPending > 0)
                questions.AddRange(NewQuestions(ps.ExtraPending, false));

            qIndex = 0;
            correctCount = 0;
            normalCount = baseQuestionCount;
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
                // 新题型（判断题等）可能只有 2 个选项，不能越界访问第 3/4 个。
                // 【严禁】用 optBtns[i].Visible 判断：首题在构造函数里渲染，此时窗体尚未 Show，
                // WinForms 中父窗体不可见时子控件的 Visible getter 恒返回 false，会跳过
                // Text/Tag 赋值 —— 按钮显示后文字为空、Tag 为 null，点击 (int)null 即
                // NullReferenceException（v1.6.4 线上事故根因）。
                // 渲染结果由纯函数算出（构造期窗体未 Show 也正确）；UI 层只做无脑赋值
                OptionSlot[] slots = QuestionView.Build(q, optBtns.Length);
                for (int i = 0; i < optBtns.Length; i++)
                {
                    optBtns[i].Visible = slots[i].Visible;
                    optBtns[i].Tag = QuestionView.TagOf(slots[i]);
                    optBtns[i].Text = slots[i].Text;
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
                subj = Strings.Get((s == Subject.Chinese) ? "语文" : (s == Subject.Math) ? "数学" : "英语");
            }
            int practiceStart = normalCount;
            int extraCount = questions.Count - practiceStart;
            if (extraCount < 0) extraCount = 0;
            string extra = extraCount > 0 ? Strings.Fmt(" + {0} 道加练题", extraCount) : "";
            string phase = (qIndex >= practiceStart && extraCount > 0) ? Strings.Get("（加练）") : "";
            string cheer = correctCount > 0 ? Store.GetEncourageText(user) + "　" : "";
            statusLabel.Text = cheer + Strings.Fmt("共 {0} 题{1}（{2}），需答对 {3} 道{4}（当前第 {5} 题，已得 {6} 星）",
                questions.Count, extra, subj, requiredStreak, phase, cur, correctCount);
        }

        void SubmitAnswer()
        {
            // 冷却结束后、点「继续答题」之前也必须拦截：否则孩子能对着屏幕上的
            // 正确答案反复点旧题刷判定（扣星与补题的语义会被绕过）
            if (wrongLeft > 0 || AnswerFeedback.BlocksAnswerInput(feedbackPending)) return;
            if (questions[qIndex].Type != QType.MathCalc) return;
            int v;
            if (!int.TryParse(answerBox.Text.Trim(), out v))
            {
                statusLabel.Text = Strings.Get("请输入数字答案");
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
            // 冷却结束后、点「继续答题」之前也必须拦截：否则孩子能对着屏幕上的
            // 正确答案反复点旧题刷判定（扣星与补题的语义会被绕过）
            if (wrongLeft > 0 || AnswerFeedback.BlocksAnswerInput(feedbackPending)) return;
            if (questions == null || qIndex < 0 || qIndex >= questions.Count) return;
            Question q = questions[qIndex];
            if (q == null || q.Type == QType.MathCalc || q.Options == null) return;
            Button b = sender as Button;
            int idx = UiSafe.OptionIndex(b == null ? null : b.Tag, q.Options.Length);
            if (idx == UiSafe.InvalidIndex) return;   // Tag 未初始化/非索引：直接忽略，绝不拆箱
            if (idx == q.CorrectIndex) OnCorrect();
            else OnWrong();
        }

        void OnCorrect()
        {
            Question answered = questions[qIndex];
            correctQuestions.Add(answered);
            Store.RecordAnswer(user, answered, true);       // 按题统计 + SM-2 复习进度
            if (Store.IsReviewOn(user)) ReviewBank.Remove(user, QuestionStats.KeyOf(answered));
            int practiceStart = normalCount;
            if (qIndex >= practiceStart && questions.Count > practiceStart)
            {
                // 加练题答对：逐道扣除待加练数（跨天 / 重启保留）
                PracticeState ps = ExtraPractice.OnExtraCorrect(Store.GetPracticeState(user));
                Store.SetPracticeState(user, ps);
            }
            correctCount = AnswerFlow.StarsAfterCorrect(correctCount);
            qIndex++;
            if (AnswerFlow.Reached(correctCount, requiredStreak))
            {
                // 达到目标星数：有加练题则跳过剩余普通题继续做完，否则直接解锁
                if (questions.Count > practiceStart && qIndex < questions.Count)
                {
                    if (qIndex < practiceStart) qIndex = practiceStart;
                    ShowCurrentQuestion();
                    UpdateStatus();
                    return;
                }
                FinishBatch();
                return;
            }
            if (qIndex >= questions.Count) { FinishBatch(); return; }
            ShowCurrentQuestion();
            UpdateStatus();
        }

        /// <summary>结算本批：星级奖励写入待结算秒数，由 App.Unlock 一次性取出（未启用时恒为 0）</summary>
        void FinishBatch()
        {
            UnlockReason = "答题达标（答对 " + correctCount + " 道）";
            int reward = 0;
            if (Store.IsStarRewardEnabled(user))
            {
                if (Store.IsStarWalletEnabled(user))
                {
                    // 钱包模式：星星存进余额，由孩子在托盘自助兑换（不再解锁自动折算）
                    Store.AddStarBalance(user, StarReward.TotalStars(correctQuestions));
                }
                else
                {
                    reward = StarReward.RewardSeconds(correctQuestions, Store.GetStarRewards(user));
                }
            }
            Store.SetPendingReward(reward);
            if (UnlockRequested != null) UnlockRequested(this, EventArgs.Empty);
        }

        void OnWrong()
        {
            // 答错：扣 1 星（最低 0，扣完为止），进度不回退到第 1 题；
            // 同时补一道新题，保证还有题可答；达到阈值仍会额外增加加练题。
            Question wrong = questions[qIndex];
            PracticeState ps = Store.GetPracticeState(user);
            ps = ExtraPractice.OnWrong(ps, Store.GetExtraThreshold(user), Store.GetExtraPerThreshold(user));
            Store.SetPracticeState(user, ps);
            Store.RecordAnswer(user, wrong, false);
            if (Store.IsReviewOn(user))
                ReviewBank.RecordWrong(wrong, user, Store.GetReviewState(user, QuestionStats.KeyOf(wrong)));

            if (correctCount > 0)
            {
                correctCount = AnswerFlow.StarsAfterWrong(correctCount);
                // 星数回退时同步撤掉一道已答对题，星级奖励按最终星数结算
                if (correctQuestions.Count > 0) correctQuestions.RemoveAt(correctQuestions.Count - 1);
            }
            AddQuestionAfterWrong();

            // 【不要在这里 StartBatch】。旧实现答错瞬间就重建题目，屏幕换成新第 1 题，
            // 而下方反馈显示的却是刚答错那题的正确答案 —— 题与答案对不上。
            // 现在保留当前题，让孩子看完解析后点「继续答题」进入下一题。
            bool feedbackOn = Store.IsFeedbackEnabled(user);
            feedbackPending = feedbackOn;
            feedbackQuestion = feedbackOn ? wrong : null;
            int hold = feedbackOn ? Store.GetFeedbackHoldSec(user) : 0;
            wrongLeft = Math.Max(Constants.WrongCooldownSec, hold);
            lastCooldownTick = Environment.TickCount;
            answerBox.Enabled = false;
            submitBtn.Enabled = false;
            foreach (Button b in optBtns) b.Enabled = false;

            if (feedbackOn)
            {
                feedbackLabel.Text = AnswerFeedback.Build(wrong);
                feedbackLabel.Visible = true;
                continueBtn.Visible = true;
                continueBtn.Enabled = false;   // 停留/冷却结束后才可点
            }
            statusLabel.Text = AnswerFeedback.StatusText(feedbackOn, wrongLeft);
            LayoutControls();
        }

        /// <summary>
        /// 答错后补一道新题，插在当前题后面（点「继续答题」后正好答到它），
        /// 跳过与现有题干重复的题；答错普通题补普通题，答错加练题补加练题。
        /// </summary>
        void AddQuestionAfterWrong()
        {
            Question added = null;
            for (int attempt = 0; attempt < 5 && added == null; attempt++)
            {
                List<Question> one = NewQuestions(1, true);
                if (one == null || one.Count == 0) break;
                bool dup = false;
                foreach (Question q in questions)
                    if (q != null && q.Prompt == one[0].Prompt) { dup = true; break; }
                if (!dup) added = one[0];
            }
            if (added == null) return;
            bool inPractice = qIndex >= normalCount;
            int at = qIndex + 1;
            if (at > questions.Count) at = questions.Count;
            questions.Insert(at, added);
            if (!inPractice) normalCount++;
        }

        /// <summary>答错反馈结束后进入下一题：不重建批次，已得的星保留（最多扣 1 星）。</summary>
        void AdvanceAfterWrong()
        {
            feedbackPending = false;
            feedbackQuestion = null;
            feedbackLabel.Visible = false;
            feedbackLabel.Text = "";
            continueBtn.Visible = false;
            answerBox.Enabled = true;
            submitBtn.Enabled = true;
            foreach (Button b in optBtns) b.Enabled = true;
            qIndex++;
            if (qIndex >= questions.Count && !AnswerFlow.Reached(correctCount, requiredStreak))
                AddQuestionAfterWrong();
            if (qIndex >= questions.Count) { FinishBatch(); return; }
            ShowCurrentQuestion();
            UpdateStatus();
            answerBox.Focus();
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
                            pinFailLeft = 0;
                            pinCooldown = Constants.PinCooldownSec;
                            lastCooldownTick = Environment.TickCount;
                            pinBox.Enabled = false;
                            pinOkBtn.Enabled = false;
                            AuditLog.Write("密码连续失败：锁屏家长入口进入 " + Constants.PinCooldownSec + " 秒冷却");
                        }
                        else
                        {
                            pinOkBtn.Enabled = true;
                            pinFailLeft = Constants.PinMaxFails - pinFails;
                            pinLabel.Text = UiText.PinLabel(0, pinFailLeft);
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
                // 无符号差值：Environment.TickCount 约 49.7 天回绕时也不会出现负数跳变
                uint now = (uint)Environment.TickCount;
                uint last = (uint)lastCooldownTick;
                if (now - last >= 1000)
                {
                    lastCooldownTick = (int)now;
                    if (wrongLeft > 0)
                    {
                        wrongLeft--;
                        if (wrongLeft == 0 && feedbackPending)
                        {
                            // 反馈开启：只放行「继续答题」，答题控件保持禁用，题目保持原题
                            continueBtn.Enabled = true;   // 不读 Visible：构造期 getter 不可靠
                            statusLabel.Text = AnswerFeedback.StatusText(true, 0);
                        }
                        else if (wrongLeft == 0)
                        {
                            // 反馈关闭：冷却结束后进入下一题（不重建批次，已得的星保留）
                            AdvanceAfterWrong();
                        }
                        else
                        {
                            statusLabel.Text = AnswerFeedback.StatusText(feedbackPending, wrongLeft);
                        }
                    }
                    if (pinCooldown > 0)
                    {
                        pinCooldown--;
                        if (pinCooldown == 0)
                        {
                            pinBox.Enabled = true;
                            pinOkBtn.Enabled = true;
                            pinFailLeft = 0;
                        }
                        pinLabel.Text = UiText.PinLabel(pinCooldown, pinFailLeft);
                    }
                }
            }

            // 普通时段硬锁：实时刷新「距下次可用还有多久」
            if (dueToWindow && string.IsNullOrEmpty(hardReason))
                statusLabel.Text = WindowStatusText();

            // 护眼休息倒计时（UTC 截止，重启不失效）
            if (restActive)
            {
                int left = RestCooldown.RemainingSeconds(Store.GetRestUntilUtc(), DateTime.UtcNow.Ticks);
                restLabel.Text = left > 0
                    ? Strings.Fmt("还需休息 {0} 分 {1} 秒", left / 60, left % 60)
                    : Strings.Get("休息结束，正在进入答题…");
                restLabel.Left = ClientSize.Width / 2 - restLabel.Width / 2;
            }

            // 硬锁自动解锁：普通时段锁 → 进入允许时段；护眼休息 → 转答题锁屏（不放行）；
            // 额度用尽 / 时间异常 → 不自动解锁，必须家长 PIN（避免靠等待绕过）。
            if (dueToWindow && !autoUnlocked && App.Instance != null)
            {
                bool canAuto = false;
                string reason = "进入可用时段自动解锁";
                bool restOver = false;
                if (!string.IsNullOrEmpty(hardReason))
                {
                    if (RestCooldown.IsRestReason(hardReason) &&
                        !RestCooldown.IsResting(Store.GetRestUntilUtc(), DateTime.UtcNow.Ticks))
                    {
                        canAuto = true;
                        restOver = true;
                        reason = "护眼休息结束";
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
                    // 护眼休息只解决「该休息」，不解决「该答题」：休息结束一律转到答题锁屏，
                    // 答对够星数才进入新一轮。否则等满休息时间就能免答题拿到整轮时长。
                    if (restOver)
                    {
                        App.Instance.SwitchToQuizLock();
                        return;
                    }
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
