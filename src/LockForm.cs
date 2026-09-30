using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
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

        const int TotalQuestions = 10;   // 一次锁屏共 10 题，全部答对才能解锁

        Label titleLabel;
        Label questionLabel;
        Label statusLabel;
        TextBox answerBox;
        Button submitBtn;
        Button parentBtn;
        Panel parentPanel;
        Label pinLabel;
        TextBox pinBox;
        Button pinOkBtn;
        Button pinBackBtn;

        int correctCount;   // 本轮已答对题数
        int qIndex;         // 当前第几题
        List<MathQuestion> questions;
        int wrongLeft;      // 答错后的冷却秒数
        int pinFails;
        int pinCooldown;
        Random rnd = new Random();
        LowLevelHook hook;
        Timer uiTimer;

        public LockForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Rectangle vs = SystemInformation.VirtualScreen;
            Location = vs.Location;
            Size = vs.Size;
            BackColor = Color.FromArgb(12, 18, 32);
            TopMost = true;
            ShowInTaskbar = false;

            titleLabel = MakeLabel("时间已到，已锁定", 30, FontStyle.Bold, Color.White);
            questionLabel = MakeLabel("", 44, FontStyle.Bold, Color.FromArgb(120, 200, 255));
            statusLabel = MakeLabel("", 14, FontStyle.Regular, Color.Silver);

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

            parentBtn = new Button();
            parentBtn.Text = "家长入口";
            parentBtn.Font = new Font("Microsoft YaHei", 9);
            parentBtn.AutoSize = true;
            parentBtn.ForeColor = Color.DimGray;
            parentBtn.Click += delegate { parentPanel.Visible = true; pinBox.Focus(); };

            parentPanel = new Panel();
            parentPanel.BorderStyle = BorderStyle.FixedSingle;
            parentPanel.Size = new Size(380, 150);
            parentPanel.BackColor = Color.FromArgb(30, 38, 60);

            pinLabel = MakeLabel("家长 PIN：", 12, FontStyle.Regular, Color.White);
            pinLabel.Location = new Point(20, 18);

            pinBox = new TextBox();
            pinBox.Font = new Font("Microsoft YaHei", 14);
            pinBox.Location = new Point(20, 52);
            pinBox.Width = 330;
            pinBox.PasswordChar = '*';
            pinBox.MaxLength = 20;
            pinBox.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                // PIN 只能是数字：答案框也只接受数字，允许字母会导致 PIN 永远无法在锁屏界面输入
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
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

            parentPanel.Controls.Add(pinLabel);
            parentPanel.Controls.Add(pinBox);
            parentPanel.Controls.Add(pinOkBtn);
            parentPanel.Controls.Add(pinBackBtn);
            parentPanel.Visible = false;

            Controls.Add(titleLabel);
            Controls.Add(questionLabel);
            Controls.Add(statusLabel);
            Controls.Add(answerBox);
            Controls.Add(submitBtn);
            Controls.Add(parentBtn);
            Controls.Add(parentPanel);

            Resize += delegate { LayoutControls(); };

            uiTimer = new Timer();
            uiTimer.Interval = 500;
            uiTimer.Tick += UiTick;
            uiTimer.Start();

            StartBatch();
            LayoutControls();

            hook = new LowLevelHook();
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
            int y = ClientSize.Height * 32 / 100;
            titleLabel.Left = cx - titleLabel.Width / 2;
            titleLabel.Top = y;
            questionLabel.Left = cx - questionLabel.Width / 2;
            questionLabel.Top = y + 85;
            answerBox.Left = cx - 160;
            answerBox.Top = questionLabel.Bottom + 55;
            submitBtn.Left = answerBox.Right + 24;
            submitBtn.Top = answerBox.Top;
            statusLabel.Left = cx - statusLabel.Width / 2;
            statusLabel.Top = answerBox.Bottom + 45;
            parentBtn.Left = ClientSize.Width - parentBtn.Width - 24;
            parentBtn.Top = ClientSize.Height - parentBtn.Height - 24;
            parentPanel.Left = cx - parentPanel.Width / 2;
            parentPanel.Top = y + 230;
        }

        void StartBatch()
        {
            questions = MathUtil.NextBatch(TotalQuestions);
            qIndex = 0;
            ShowCurrentQuestion();
            UpdateStatus();
        }

        void ShowCurrentQuestion()
        {
            if (questions == null || questions.Count == 0) return;
            if (qIndex >= questions.Count) qIndex = questions.Count - 1;
            questionLabel.Text = questions[qIndex].Text;
            LayoutControls();
        }

        void UpdateStatus()
        {
            int cur = Math.Min(qIndex + 1, questions.Count);
            statusLabel.Text = "共 " + TotalQuestions + " 题，全部答对即可解锁（当前第 " + cur +
                " 题，已答对 " + correctCount + " 题）";
        }

        void SubmitAnswer()
        {
            if (wrongLeft > 0) return;
            int v;
            if (!int.TryParse(answerBox.Text.Trim(), out v))
            {
                statusLabel.Text = "请输入数字答案";
                answerBox.Focus();
                return;
            }
            if (v == questions[qIndex].Answer)
            {
                correctCount++;
                qIndex++;
                if (qIndex >= questions.Count)
                {
                    if (UnlockRequested != null) UnlockRequested(this, EventArgs.Empty);
                    return;
                }
                ShowCurrentQuestion();
                UpdateStatus();
            }
            else
            {
                // 答错一题：整批清零，从第 1 题重新开始
                correctCount = 0;
                qIndex = 0;
                ShowCurrentQuestion();
                wrongLeft = 5;
                answerBox.Enabled = false;
                submitBtn.Enabled = false;
                statusLabel.Text = "回答错误，全部清零重新开始，请 5 秒后再试";
            }
            answerBox.Text = "";
            answerBox.Focus();
        }

        void TryParentPin()
        {
            if (pinCooldown > 0) return;
            if (Store.VerifyPin(pinBox.Text))
            {
                if (UnlockRequested != null) UnlockRequested(this, EventArgs.Empty);
            }
            else
            {
                pinFails++;
                pinBox.Text = "";
                pinBox.Focus();
                if (pinFails >= 5)
                {
                    pinFails = 0;
                    pinCooldown = 30;
                    pinBox.Enabled = false;
                    pinOkBtn.Enabled = false;
                }
                else
                {
                    pinLabel.Text = "PIN 错误（还可尝试 " + (5 - pinFails) + " 次）";
                }
            }
        }

        void UiTick(object sender, EventArgs e)
        {
            // 保持全屏覆盖 + 置顶 + 焦点
            Rectangle vs = SystemInformation.VirtualScreen;
            if (Bounds != vs) { Location = vs.Location; Size = vs.Size; }
            if (!TopMost) TopMost = true;
            if (!ContainsFocus) Activate();

            if (wrongLeft > 0)
            {
                wrongLeft--;
                if (wrongLeft == 0)
                {
                    answerBox.Enabled = true;
                    submitBtn.Enabled = true;
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

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TopMost = true;
            Activate();
            answerBox.Focus();
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
    }

    /// <summary>低级键盘钩子：锁屏期间屏蔽 Win 键、Alt+Tab、Alt+Esc、Ctrl+Esc、Alt+方向键</summary>
    public class LowLevelHook : IDisposable
    {
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;
        const int WM_SYSKEYDOWN = 0x0104;

        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        IntPtr hHook;
        HookProc proc;

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int nVirtKey);

        public LowLevelHook()
        {
            proc = HookCallback;
            try { hHook = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0); }
            catch (Exception) { hHook = IntPtr.Zero; }
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vk = Marshal.ReadInt32(lParam);
                bool isSysKey = (wParam == (IntPtr)WM_SYSKEYDOWN);
                bool isKeyDown = (wParam == (IntPtr)WM_KEYDOWN);
                bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;
                bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
                bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;

                if (vk == 0x5B || vk == 0x5C) return (IntPtr)1;            // 左/右 Win 键
                // Alt+Tab：WM_SYSKEYDOWN 时 Tab 必然带 Alt，直接拦截
                if (isSysKey && vk == 0x09) return (IntPtr)1;
                // Shift+Tab：WM_KEYDOWN + Shift 按下
                if (isKeyDown && vk == 0x09 && shift) return (IntPtr)1;
                if (vk == 0x1B && (alt || ctrl)) return (IntPtr)1;         // Alt+Esc / Ctrl+Esc
                if (alt && (vk >= 0x25 && vk <= 0x28)) return (IntPtr)1;   // Alt+方向键
            }
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (hHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hHook);
                hHook = IntPtr.Zero;
            }
        }
    }
}
