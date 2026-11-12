using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>
    /// P0/P1 新增规则的统一设置窗口。所有项默认关闭 / 保持旧行为，按 Windows 用户隔离。
    /// 从家长设置菜单进入（需先通过家长 PIN）。
    ///
    /// 布局约定：全部用 TableLayoutPanel 逐行堆叠（PageGrid），不再手写坐标。
    /// 手写坐标的两个老问题：① 复选框 AutoSize 后比固定 x 的按钮更宽，两者直接叠在一起；
    /// ② 标签文字（长句/英文翻译）变长后压住右侧输入框。按行堆叠后同类控件永不相交。
    /// </summary>
    public class AdvancedSettingsForm : Form
    {
        ComboBox cbUser;
        TextBox txtDailyLimit, txtUnlockLimit, txtRules, txtRest, txtTempPass;
        TextBox txtQCount, txtStreak, txtExtraThreshold, txtExtraPer;
        TextBox[] txtStars = new TextBox[5];
        CheckBox chkAntiTamper, chkFeedback, chkStar, chkPractice, chkStarWallet;
        TextBox txtStarSecPerStar, txtStarDailyMin;
        CheckBox chkMathChoice, chkChineseWord, chkTrueFalse, chkChildGrade, chkReview;
        ComboBox cbDifficulty, cbTerm, cbLang;
        TextBox txtBank, txtHold, txtEncourage, txtHolidays, txtLogKeep;
        Label hint;

        public AdvancedSettingsForm()
        {
            Text = "高级规则 - 家长设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(600, 610);
            this.Icon = Program.LoadAppIcon();

            // ---- 根布局：配置对象 / 选项卡（占满）/ 状态提示 / 按钮 ----
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12, 10, 12, 10);
            root.ColumnCount = 2;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));   // 两行提示的高度
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            Label ul = PageGrid.MakeLabel("配置对象：");
            ul.Margin = new Padding(0, 4, 10, 8);
            root.Controls.Add(ul, 0, 0);

            cbUser = new ComboBox();
            cbUser.Size = new Size(200, 24);
            cbUser.DropDownStyle = ComboBoxStyle.DropDownList;
            cbUser.Anchor = AnchorStyles.Left;
            cbUser.Margin = new Padding(0, 0, 0, 8);
            cbUser.Items.AddRange(Store.ListUsers());
            if (cbUser.Items.Count > 0) cbUser.SelectedIndex = 0;
            cbUser.SelectedIndexChanged += delegate { LoadValues(cbUser.Text); };
            root.Controls.Add(cbUser, 1, 0);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Margin = new Padding(0, 0, 0, 6);

            TabPage p1 = new TabPage("时间与额度");
            BuildTimePage(p1);
            tabs.TabPages.Add(p1);

            TabPage p2 = new TabPage("答题与奖励");
            BuildQuizPage(p2);
            tabs.TabPages.Add(p2);

            TabPage p3 = new TabPage("题库与记录");
            BuildBankPage(p3);
            tabs.TabPages.Add(p3);

            TabPage p4 = new TabPage("学习与体验");
            BuildLearnPage(p4);
            tabs.TabPages.Add(p4);

            root.Controls.Add(tabs, 0, 1);
            root.SetColumnSpan(tabs, 2);

            hint = new Label();
            hint.Text = "";
            hint.ForeColor = Color.DimGray;
            hint.AutoSize = false;
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Margin = new Padding(0, 0, 0, 4);
            root.Controls.Add(hint, 0, 2);
            root.SetColumnSpan(hint, 2);

            // 右对齐按钮条：不参与文字排版，改字号/换语言都不会压住提示行
            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.FlowDirection = FlowDirection.RightToLeft;
            bar.WrapContents = false;
            bar.AutoSize = true;
            bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bar.Anchor = AnchorStyles.Right;
            bar.Margin = new Padding(0);
            root.Controls.Add(bar, 0, 3);
            root.SetColumnSpan(bar, 2);

            Button save = new Button();
            save.Text = "保存全部";
            save.Size = new Size(100, 32);
            save.Margin = new Padding(8, 0, 0, 0);
            save.Click += delegate { SaveAll(); };

            Button close = new Button();
            close.Text = "关闭";
            close.Size = new Size(100, 32);
            close.Margin = new Padding(0);
            close.Click += delegate { Close(); };

            // RightToLeft 流式布局：先加的更靠右，因此「关闭」最后落位在右下角（与旧版一致）
            bar.Controls.Add(close);
            bar.Controls.Add(save);

            LoadValues(cbUser.Text);
            Program.Localize(this);
            Program.FlattenButtons(this);
        }

        // ==================== 各选项卡 ====================

        void BuildTimePage(TabPage p)
        {
            PageGrid g = new PageGrid(p);

            txtDailyLimit = B(70);
            g.Field("每日总时长上限（分钟，0=关闭）", txtDailyLimit);
            txtUnlockLimit = B(70);
            g.Field("每日解锁次数上限（0=关闭）", txtUnlockLimit);

            g.Gap(4);
            g.Text("多规则时段（留空 = 沿用旧「每日可用时段」，旧安装行为不变）", false);
            g.Text("格式：1-5|17:00-21:00@quiz;6,7|08:00-22:00\n" +
                "星期 1=周一..7=周日；@quiz = 仅时段内需要答题（时段外自由、不扣时长）；\n" +
                "不带 @quiz = 仅时段内可用（时段外硬锁）；支持跨午夜 22:00-06:00。", true);

            txtRules = new TextBox();
            txtRules.Multiline = true;
            txtRules.ScrollBars = ScrollBars.Vertical;
            g.Full(txtRules, 92);

            Button grid = new Button();
            grid.Text = "打开时段格子编辑器";
            grid.Size = new Size(180, 30);
            grid.Click += delegate
            {
                using (ScheduleGridForm f = new ScheduleGridForm())
                    if (f.ShowDialog(this) == DialogResult.OK) txtRules.Text = f.RuleText;
            };
            g.Full(grid, 0);

            g.Gap(4);
            txtRest = B(70);
            g.Field("护眼强制休息（分钟，0=关闭；时长用尽后休息，休息完仍需答题）", txtRest);
            txtTempPass = B(70);
            g.Field("家长临时放行（分钟）", txtTempPass);

            chkAntiTamper = new CheckBox();
            chkAntiTamper.Text = "检测系统时间大幅回拨并保护性锁定（可选加固，默认关）";
            chkAntiTamper.AutoSize = true;
            g.Full(chkAntiTamper, 0);
        }

        void BuildQuizPage(TabPage p)
        {
            PageGrid g = new PageGrid(p);

            txtQCount = B(70);
            g.Field("每次锁屏题量（1-30，默认 10）", txtQCount);
            txtStreak = B(70);
            g.Field("需答对题数（默认=题量，即全部答对；答错扣 1 星，最低 0 星）", txtStreak);

            chkFeedback = new CheckBox();
            chkFeedback.Text = "答错显示正确答案与解析（不改变扣星与补题规则）";
            chkFeedback.AutoSize = true;
            g.Full(chkFeedback, 0);

            g.Gap(4);
            g.Text("累计答错 N 题，增加 M 道加练题（0=关闭；跨天/重启保留）", false);
            Label ln = PageGrid.MakeLabel("N");
            ln.Margin = new Padding(4, 0, 4, 0);
            Label lm = PageGrid.MakeLabel("M");
            lm.Margin = new Padding(18, 0, 4, 0);
            txtExtraThreshold = B(56);
            txtExtraPer = B(56);
            g.Row(ln, txtExtraThreshold, lm, txtExtraPer);

            chkStar = new CheckBox();
            chkStar.Text = "启用星级时长奖励（按题目年级/难度，1-5 星）";
            chkStar.AutoSize = true;
            g.Full(chkStar, 0);

            g.Text("1-5 星每题奖励分钟数：", false);
            Control[] cells = new Control[10];
            for (int i = 0; i < 5; i++)
            {
                Label sl = PageGrid.MakeLabel((i + 1) + "★");
                sl.Margin = new Padding(i == 0 ? 4 : 18, 2, 4, 2);
                txtStars[i] = B(48);
                cells[i * 2] = sl;
                cells[i * 2 + 1] = txtStars[i];
            }
            g.Row(cells);

            chkStarWallet = new CheckBox();
            chkStarWallet.Text = "孩子可自助兑换星星（开启后解锁不再自动折算）";
            chkStarWallet.AutoSize = true;
            g.Full(chkStarWallet, 0);

            txtStarSecPerStar = B(60);
            g.Field("每星兑换秒数（默认 60，即 1 星 = 1 分钟）：", txtStarSecPerStar);
            txtStarDailyMin = B(60);
            g.Field("每日兑换上限（分钟，0=不限）：", txtStarDailyMin);

            chkPractice = new CheckBox();
            chkPractice.Text = "启用练习模式（锁屏/托盘可进入；不解锁、不暂停倒计时）";
            chkPractice.AutoSize = true;
            g.Full(chkPractice, 0);
        }

        void BuildBankPage(TabPage p)
        {
            PageGrid g = new PageGrid(p);

            g.Text("题库以程序目录下 chinese.txt / english.txt 为准（UTF-8，| 分隔）。", false);
            g.Text("JSON 用于批量备份/交换；导入会覆盖对应 txt，下次锁屏生效。", true);

            g.Gap(6);
            Button bExport = new Button();
            bExport.Text = "导出内置题库 JSON";
            bExport.Size = new Size(180, 34);
            bExport.Click += delegate { ExportBankJson(); };

            Button bZh = new Button();
            bZh.Text = "导入语文 JSON";
            bZh.Size = new Size(150, 34);
            bZh.Click += delegate { ImportBankJson(true); };

            Button bEn = new Button();
            bEn.Text = "导入英语 JSON";
            bEn.Size = new Size(150, 34);
            bEn.Click += delegate { ImportBankJson(false); };
            g.Row(bExport, bZh, bEn);

            Button bCheck = new Button();
            bCheck.Text = "题库质量检查";
            bCheck.Size = new Size(180, 34);
            bCheck.Click += delegate { CheckBank(); };

            Button bUsage = new Button();
            bUsage.Text = "查看 7 天使用记录";
            bUsage.Size = new Size(180, 34);
            bUsage.Click += delegate
            {
                using (UsageForm f = new UsageForm()) f.ShowDialog(this);
            };
            g.Row(bCheck, bUsage);

            Button bExportCfg = new Button();
            bExportCfg.Text = "导出配置（换机）";
            bExportCfg.Size = new Size(160, 32);
            bExportCfg.Click += delegate { ExportConfigToFile(); };

            Button bImportCfg = new Button();
            bImportCfg.Text = "导入配置";
            bImportCfg.Size = new Size(160, 32);
            bImportCfg.Click += delegate { ImportConfigFromFile(); };
            g.Row(bExportCfg, bImportCfg);

            g.Gap(6);
            g.Text("质量门禁：题量、选项 2-4 个且互不重复、正确答案下标、\n" +
                "无占位符、题干去重、年级不超纲；导入前还会校验年级范围与重复词条。", true);
        }

        void BuildLearnPage(TabPage p)
        {
            PageGrid g = new PageGrid(p);

            Label ld = PageGrid.MakeLabel("难度倾向（80/20 加权）");
            ld.Margin = new Padding(4, 4, 6, 4);
            cbDifficulty = new ComboBox();
            cbDifficulty.Size = new Size(120, 24);
            cbDifficulty.DropDownStyle = ComboBoxStyle.DropDownList;
            cbDifficulty.Items.AddRange(new object[] { "不限", "偏易", "中等", "偏难" });
            Label lt = PageGrid.MakeLabel("学期");
            lt.Margin = new Padding(22, 4, 6, 4);
            cbTerm = new ComboBox();
            cbTerm.Size = new Size(100, 24);
            cbTerm.DropDownStyle = ComboBoxStyle.DropDownList;
            cbTerm.Items.AddRange(new object[] { "全学年", "上册", "下册" });
            g.Row(ld, cbDifficulty, lt, cbTerm);

            txtBank = B(150);
            g.Field("题库名过滤（空=全部）", txtBank);

            chkMathChoice = new CheckBox();
            chkMathChoice.Text = "数学四选一";
            chkMathChoice.AutoSize = true;
            chkChineseWord = new CheckBox();
            chkChineseWord.Text = "看拼音选词语";
            chkChineseWord.AutoSize = true;
            chkTrueFalse = new CheckBox();
            chkTrueFalse.Text = "判断题";
            chkTrueFalse.AutoSize = true;
            chkChineseWord.Margin = new Padding(24, 3, 3, 3);
            chkTrueFalse.Margin = new Padding(24, 3, 3, 3);
            g.Row(chkMathChoice, chkChineseWord, chkTrueFalse);

            chkReview = new CheckBox();
            chkReview.Text = "间隔重复复习（错题到期优先，本地 review.json）";
            chkReview.AutoSize = true;
            g.Full(chkReview, 0);
            chkChildGrade = new CheckBox();
            chkChildGrade.Text = "允许孩子自选年级（仅影响下一批题）";
            chkChildGrade.AutoSize = true;
            g.Full(chkChildGrade, 0);

            g.Gap(2);
            txtHold = B(60);
            g.Field("解析停留秒数（0=不强制）", txtHold);
            txtEncourage = B(250);
            g.Field("鼓励语", txtEncourage);

            Label ll = PageGrid.MakeLabel("界面语言（新窗口生效）");
            ll.Margin = new Padding(4, 4, 6, 4);
            cbLang = new ComboBox();
            cbLang.Size = new Size(120, 24);
            cbLang.DropDownStyle = ComboBoxStyle.DropDownList;
            cbLang.Items.AddRange(new object[] { "中文", "English" });
            g.Row(ll, cbLang);

            g.Gap(2);
            g.Text("节假日日期（yyyy-MM-dd，逗号分隔，按周日规则）", false);
            txtHolidays = B(520);
            g.Full(txtHolidays, 0);

            g.Text("学期 / 题库名来自外部词库可选列；内置题库无学期标签时按全学年处理。", true);

            txtLogKeep = B(60);
            g.Field("日志保留天数（0=永久，全局）", txtLogKeep);
            g.Text("默认 90 天：启动时清理更早的 log\\*.log，删除失败静默。", true);

            // 让家长看得见「现在能填哪些题库名」：banks\*.txt 的文件名即题库名
            string[] banks = BankSet.CollectBankNames(AppDomain.CurrentDomain.BaseDirectory);
            string bankNote;
            if (banks.Length == 0)
            {
                bankNote = "多题库：把 chinese.txt / english.txt 拆成 banks\\<题库名>.txt，文件名即题库名。";
            }
            else if (banks.Length <= 4)
            {
                bankNote = "可用题库：" + string.Join("、", banks);
            }
            else
            {
                // 题库很多时只列前 4 个，避免长句把窗口撑宽
                string[] head = new string[4];
                Array.Copy(banks, head, 4);
                bankNote = "可用题库（共 " + banks.Length + " 个）：" +
                    string.Join("、", head) + " 等";
            }
            g.Text(bankNote, true);
        }

        // ==================== 页内布局工具 ====================

        /// <summary>
        /// 一页内容按「行」逐步堆叠：行高由内容决定，控件不会互相覆盖。
        /// 三列 = 行标签 / 输入控件 / 余量，跨列控件占满整行。
        /// 页面超出可视区时由 TabPage 自动出滚动条。
        /// </summary>
        sealed class PageGrid
        {
            readonly TableLayoutPanel panel;
            readonly List<Label> wraps = new List<Label>();
            readonly List<Label> rowLabels = new List<Label>();
            readonly List<Control> rowFields = new List<Control>();
            int row;

            public PageGrid(TabPage page)
            {
                panel = new TableLayoutPanel();
                panel.AutoSize = true;
                panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                panel.Dock = DockStyle.Top;          // 宽度跟随页面，高度随行数增长
                panel.Padding = new Padding(6, 8, 6, 8);
                panel.ColumnCount = 3;
                panel.RowCount = 0;
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // 行标签
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // 输入控件
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // 余量
                page.AutoScroll = true;              // 小屏 / 大字号下可滚动，不裁剪
                page.Controls.Add(panel);
                // 换行宽度必须跟随实际宽度：首次布局前 panel.Width 还是 0，
                // 这里统一在布局事件里刷新，避免长说明被压成 200px 的窄柱
                panel.Layout += delegate { SyncWrap(); };
            }

            void SyncWrap()
            {
                int inner = panel.ClientSize.Width - 12;   // 去掉左右 padding
                int w = Math.Max(200, inner - 8);
                for (int i = 0; i < wraps.Count; i++)
                {
                    if (wraps[i].MaximumSize.Width != w)
                        wraps[i].MaximumSize = new Size(w, 0);
                }
                // 行标签：英文/更长的译文下折行，而不是把输入框挤出页面右侧
                for (int i = 0; i < rowLabels.Count; i++)
                {
                    int cap = Math.Max(140, inner - rowFields[i].Width - 26);
                    if (rowLabels[i].MaximumSize.Width != cap)
                        rowLabels[i].MaximumSize = new Size(cap, 0);
                }
            }

            public static Label MakeLabel(string text)
            {
                Label l = new Label();
                l.Text = text;
                l.AutoSize = true;
                l.Anchor = AnchorStyles.Left;
                l.Margin = new Padding(4, 6, 10, 4);
                return l;
            }

            int NextRow(SizeType type, int height)
            {
                int r = row;
                row++;
                panel.RowCount = row;
                panel.RowStyles.Add(new RowStyle(type, height));
                return r;
            }

            /// <summary>标签 + 控件同行：控件紧贴标签文字右缘，标签变长只会折行，不会压住控件</summary>
            public void Field(string text, Control field)
            {
                int r = NextRow(SizeType.AutoSize, 0);
                Label l = MakeLabel(text);
                l.MaximumSize = new Size(Math.Max(140, panel.Width - field.Width - 38), 0);
                rowLabels.Add(l);
                rowFields.Add(field);
                panel.Controls.Add(l, 0, r);
                field.Anchor = AnchorStyles.Left;
                field.Margin = new Padding(0, 4, 4, 4);
                panel.Controls.Add(field, 1, r);
            }

            /// <summary>整行文字（跨列）：dim=true 用灰色说明色，自动换行</summary>
            public void Text(string text, bool dim)
            {
                int r = NextRow(SizeType.AutoSize, 0);
                Label l = MakeLabel(text);
                if (dim) l.ForeColor = Color.DimGray;
                l.MaximumSize = new Size(Math.Max(200, panel.Width - 20), 0);
                l.Margin = new Padding(4, dim ? 2 : 8, 4, 2);
                wraps.Add(l);
                panel.Controls.Add(l, 0, r);
                panel.SetColumnSpan(l, 3);
            }

            /// <summary>整行控件（跨列）。height &gt; 0 时固定行高并铺满宽度（多行输入框）</summary>
            public void Full(Control c, int height)
            {
                int r = NextRow(height > 0 ? SizeType.Absolute : SizeType.AutoSize, height);
                c.Margin = new Padding(4, 4, 4, 4);
                if (height > 0) c.Dock = DockStyle.Fill;
                else c.Anchor = AnchorStyles.Left;
                panel.Controls.Add(c, 0, r);
                panel.SetColumnSpan(c, 3);
            }

            /// <summary>整行横向排列的一组控件（按钮组 / 复选框组）</summary>
            public void Row(params Control[] items)
            {
                int r = NextRow(SizeType.AutoSize, 0);
                FlowLayoutPanel f = new FlowLayoutPanel();
                f.AutoSize = true;
                f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                f.WrapContents = false;
                f.Anchor = AnchorStyles.Left;
                f.Margin = new Padding(2, 4, 2, 4);
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] is Button) items[i].Margin = new Padding(i == 0 ? 0 : 10, 0, 0, 0);
                    f.Controls.Add(items[i]);
                }
                panel.Controls.Add(f, 0, r);
                panel.SetColumnSpan(f, 3);
            }

            /// <summary>纵向留白</summary>
            public void Gap(int h) { NextRow(SizeType.Absolute, h); }
        }

        // ==================== 逻辑（未改动） ====================

        void ExportConfigToFile()
        {
            string json = ConfigIo.ExportConfig();
            if (string.IsNullOrEmpty(json)) { MessageBox.Show(this, Strings.Get("导出失败（需管理员权限）。"), Strings.Get("电脑锁")); return; }
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "JSON 文件|*.json";
                d.FileName = "pclock-config-" + DateTime.Now.ToString("yyyyMMdd") + ".json";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(d.FileName, json, new UTF8Encoding(false));
                    AuditLog.Write("配置导出：" + d.FileName);
                    MessageBox.Show(this, Strings.Get("已导出。文件内含家长 PIN / 卸载密码哈希，请妥善保管。"), Strings.Get("电脑锁"));
                }
                catch (Exception ex) { MessageBox.Show(this, Strings.Get("导出失败：") + ex.Message, Strings.Get("电脑锁")); }
            }
        }

        void ImportConfigFromFile()
        {
            if (MessageBox.Show(this, Strings.Get("导入会覆盖同名配置项（含密码哈希），确定继续？"), Strings.Get("电脑锁"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "JSON 文件|*.json";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string err = ConfigIo.ImportConfig(File.ReadAllText(d.FileName, Encoding.UTF8));
                    if (err != null) { MessageBox.Show(this, Strings.Get("导入失败：") + err, Strings.Get("电脑锁")); return; }
                    AuditLog.Write("配置导入：" + d.FileName);
                    LoadValues(cbUser.Text);
                    MessageBox.Show(this, Strings.Get("已导入配置。运行态未导入，下次锁屏生效。"), Strings.Get("电脑锁"));
                }
                catch (Exception ex) { MessageBox.Show(this, Strings.Get("导入失败：") + ex.Message, Strings.Get("电脑锁")); }
            }
        }

        void LoadValues(string user)
        {
            // 构造早期 SelectedIndex=0 会先触发一次事件，此时各页控件尚未创建
            if (txtDailyLimit == null) return;
            txtDailyLimit.Text = Store.GetDailyLimitMinutes(user).ToString();
            txtUnlockLimit.Text = Store.GetUnlockLimit(user).ToString();
            txtRules.Text = Store.GetRules(user);
            txtRest.Text = Store.GetRestMinutes(user).ToString();
            txtTempPass.Text = Store.GetTempPassMinutes(user).ToString();
            chkAntiTamper.Checked = Store.IsAntiTamperEnabled();

            txtQCount.Text = Store.GetQuestionCount(user).ToString();
            txtStreak.Text = Store.GetRequiredStreak(user).ToString();
            chkFeedback.Checked = Store.IsFeedbackEnabled(user);
            txtExtraThreshold.Text = Store.GetExtraThreshold(user).ToString();
            txtExtraPer.Text = Store.GetExtraPerThreshold(user).ToString();
            chkStar.Checked = Store.IsStarRewardEnabled(user);
            int[] rewards = Store.GetStarRewards(user);
            for (int i = 0; i < 5; i++) txtStars[i].Text = rewards[i].ToString();
            chkStarWallet.Checked = Store.IsStarWalletEnabled(user);
            txtStarSecPerStar.Text = Store.GetStarSecondsPerStar(user).ToString();
            txtStarDailyMin.Text = Store.GetStarDailyLimitMinutes(user).ToString();
            chkPractice.Checked = Store.IsPracticeEnabled(user);
            cbDifficulty.SelectedIndex = Store.GetPreferDifficulty(user);
            cbTerm.SelectedIndex = Store.GetTerm(user);
            cbLang.SelectedIndex = Store.GetLanguage() == "en" ? 1 : 0;
            txtBank.Text = Store.GetBankFilter(user);
            txtHold.Text = Store.GetFeedbackHoldSec(user).ToString();
            txtEncourage.Text = Store.GetEncourageText(user);
            txtHolidays.Text = Store.GetHolidayDates();
            txtLogKeep.Text = Store.GetLogKeepDays().ToString();
            chkMathChoice.Checked = Store.IsMathChoiceOn(user);
            chkChineseWord.Checked = Store.IsChineseWordOn(user);
            chkTrueFalse.Checked = Store.IsTrueFalseOn(user);
            chkChildGrade.Checked = Store.IsChildGradeOn(user);
            chkReview.Checked = Store.IsReviewOn(user);
            hint.Text = "所有新增规则默认关闭；修改后按用户生效。";
        }

        void SaveAll()
        {
            string u = cbUser.Text;
            try
            {
                Store.SetDailyLimitMinutes(u, Int(txtDailyLimit.Text, 0));
                Store.SetUnlockLimit(u, Int(txtUnlockLimit.Text, 0));
                Store.SetRules(u, txtRules.Text.Trim());
                Store.SetRestMinutes(u, Int(txtRest.Text, 0));
                Store.SetTempPassMinutes(u, Int(txtTempPass.Text, Constants.DefaultTempPassMinutes));
                Store.SetAntiTamperEnabled(chkAntiTamper.Checked);

                // 先写题量，再写需答对数（后者会按题量钳制）
                Store.SetQuestionCount(u, Int(txtQCount.Text, QuizConfig.DefaultQuestionCount));
                Store.SetRequiredStreak(u, Int(txtStreak.Text, Store.GetQuestionCount(u)));
                Store.SetFeedbackEnabled(u, chkFeedback.Checked);
                Store.SetExtraThreshold(u, Int(txtExtraThreshold.Text, 0));
                Store.SetExtraPerThreshold(u, Int(txtExtraPer.Text, 0));
                Store.SetStarRewardEnabled(u, chkStar.Checked);
                int[] rewards = new int[5];
                for (int i = 0; i < 5; i++) rewards[i] = Int(txtStars[i].Text, i + 1);
                Store.SetStarRewards(u, rewards);
                Store.SetStarWalletEnabled(u, chkStarWallet.Checked);
                Store.SetStarSecondsPerStar(u, Int(txtStarSecPerStar.Text, StarWallet.DefaultSecondsPerStar));
                Store.SetStarDailyLimitMinutes(u, Int(txtStarDailyMin.Text, StarWallet.DefaultDailyLimitMinutes));
                Store.SetPracticeEnabled(u, chkPractice.Checked);
                Store.SetPreferDifficulty(u, cbDifficulty.SelectedIndex);
                Store.SetTerm(u, cbTerm.SelectedIndex);
                Store.SetLanguage(cbLang.SelectedIndex == 1 ? "en" : "zh");
                Strings.Lang = Store.GetLanguage();
                Store.SetBankFilter(u, txtBank.Text);
                Store.SetFeedbackHoldSec(u, Int(txtHold.Text, 0));
                Store.SetEncourageText(u, txtEncourage.Text);
                Store.SetHolidayDates(txtHolidays.Text);
                Store.SetLogKeepDays(Int(txtLogKeep.Text, Constants.DefaultLogKeepDays));
                Store.SetMathChoiceOn(u, chkMathChoice.Checked);
                Store.SetChineseWordOn(u, chkChineseWord.Checked);
                Store.SetTrueFalseOn(u, chkTrueFalse.Checked);
                Store.SetChildGradeOn(u, chkChildGrade.Checked);
                Store.SetReviewOn(u, chkReview.Checked);

                AuditLog.Write("高级规则设置修改：用户 " + u);
                hint.Text = "已保存。部分设置（题量/规则/题库）在下次锁屏或下次登录生效。";
                MessageBox.Show(this, Strings.Fmt("已保存为「{0}」的高级规则。", u), Strings.Get("电脑锁"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Strings.Get("保存失败：") + ex.Message, Strings.Get("电脑锁"));
            }
        }

        static int Int(string s, int def)
        {
            int v;
            return int.TryParse(s == null ? "" : s.Trim(), out v) ? v : def;
        }

        void ExportBankJson()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "JSON 文件|*.json";
                d.FileName = "pclock-bank-" + DateTime.Now.ToString("yyyyMMdd") + ".json";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    // 合并导出：两个分库各带 items 数组，ImportChinese / ImportEnglish 均可直接读回
                    string json = "{\"chinese\":" + BankIo.ExportChinese(ChineseData.All) +
                        ",\"english\":" + BankIo.ExportEnglish(EnglishData.All) + "}";
                    File.WriteAllText(d.FileName, json, new UTF8Encoding(false));
                    AuditLog.Write("题库导出 JSON：" + d.FileName);
                    MessageBox.Show(this, Strings.Fmt("已导出到：{0}", d.FileName), Strings.Get("电脑锁"));
                }
                catch (Exception ex) { MessageBox.Show(this, Strings.Get("导出失败：") + ex.Message, Strings.Get("电脑锁")); }
            }
        }

        void ImportBankJson(bool chinese)
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "JSON 文件|*.json";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string text = File.ReadAllText(d.FileName, Encoding.UTF8);
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        chinese ? "chinese.txt" : "english.txt");
                    StringBuilder sb = new StringBuilder();
                    sb.Append("# 由 JSON 导入生成（UTF-8）").Append(Environment.NewLine);
                    if (chinese)
                    {
                        List<WordEntry> items = BankIo.ImportChinese(text);
                        string err = BankIo.ValidateChinese(items);
                        if (err != null) throw new FormatException(err);
                        foreach (WordEntry e in items)
                            sb.Append(e.Grade).Append('|').Append(e.Word).Append('|')
                              .Append(e.Pinyin).Append('|').Append(e.Meaning).Append(Environment.NewLine);
                    }
                    else
                    {
                        List<EnglishEntry> items = BankIo.ImportEnglish(text);
                        if (items.Count == 0) throw new FormatException("英语题库为空或格式不符");
                        foreach (EnglishEntry e in items)
                            sb.Append(e.Grade).Append('|').Append(e.En).Append('|')
                              .Append(e.Zh).Append(Environment.NewLine);
                    }
                    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                    AuditLog.Write("题库导入 JSON：" + path);
                    MessageBox.Show(this, Strings.Fmt("已写入 {0}，下次锁屏生效。", path), Strings.Get("电脑锁"));
                }
                catch (Exception ex) { MessageBox.Show(this, Strings.Get("导入失败：") + ex.Message, Strings.Get("电脑锁")); }
            }
        }

        void CheckBank()
        {
            string u = cbUser.Text;
            int[] grades = Store.GetGrades(u);
            int[] si = Store.GetSubjects(u);
            Subject[] subs = new Subject[si.Length];
            for (int i = 0; i < si.Length; i++) subs[i] = (Subject)si[i];
            List<Question> qs = QuestionBank.GetBatch(grades, subs, 10);
            string err = QuestionPolicy.ValidateBatch(qs, 10);
            string cap = QuestionPolicy.IsGradeCapped(qs, grades) ? "通过" : "失败（年级超纲）";
            string bankErr = BankIo.ValidateChinese(ChineseData.All);
            MessageBox.Show(this,
                Strings.Fmt("批次质量：{0}", err == null ? Strings.Get("通过") : err) + Environment.NewLine +
                Strings.Fmt("年级上限：{0}", Strings.Get(cap)) + Environment.NewLine +
                Strings.Fmt("内置语文词库：{0}", bankErr == null ? Strings.Get("通过") : bankErr),
                Strings.Get("题库质量检查"), MessageBoxButtons.OK,
                err == null && cap == "通过" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        static TextBox B(int w) { return B(w, 24); }
        static TextBox B(int w, int h)
        {
            TextBox t = new TextBox();
            t.Size = new Size(w, h);
            return t;
        }
    }
}
