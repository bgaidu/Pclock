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
    /// </summary>
    public class AdvancedSettingsForm : Form
    {
        ComboBox cbUser;
        TextBox txtDailyLimit, txtUnlockLimit, txtRules, txtRest, txtTempPass;
        TextBox txtQCount, txtStreak, txtExtraThreshold, txtExtraPer;
        TextBox[] txtStars = new TextBox[5];
        CheckBox chkAntiTamper, chkFeedback, chkStar, chkPractice;
        CheckBox chkMathChoice, chkChineseWord, chkTrueFalse, chkChildGrade, chkReview;
        ComboBox cbDifficulty, cbTerm, cbLang;
        TextBox txtBank, txtHold, txtEncourage, txtHolidays;
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
            ClientSize = new Size(560, 500);
            this.Icon = Program.LoadAppIcon();

            Label ul = L("配置对象：", 15, 16);
            Controls.Add(ul);
            cbUser = new ComboBox();
            cbUser.Location = new Point(90, 12);
            cbUser.Size = new Size(180, 24);
            cbUser.DropDownStyle = ComboBoxStyle.DropDownList;
            cbUser.Items.AddRange(Store.ListUsers());
            cbUser.SelectedIndex = 0;
            cbUser.SelectedIndexChanged += delegate { LoadValues(cbUser.Text); };
            Controls.Add(cbUser);

            TabControl tabs = new TabControl();
            tabs.Location = new Point(12, 46);
            tabs.Size = new Size(536, 400);

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
            Controls.Add(tabs);

            hint = L("", 15, 452);
            hint.ForeColor = Color.DimGray;
            Controls.Add(hint);

            Button save = new Button();
            save.Text = "保存全部";
            save.Location = new Point(330, 458);
            save.Size = new Size(100, 32);
            save.Click += delegate { SaveAll(); };
            Controls.Add(save);

            Button close = new Button();
            close.Text = "关闭";
            close.Location = new Point(440, 458);
            close.Size = new Size(100, 32);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            LoadValues(cbUser.Text);
            Program.FlattenButtons(this);
        }

        void BuildTimePage(TabPage p)
        {
            p.Controls.Add(L("每日总时长上限（分钟，0=关闭）", 15, 20));
            txtDailyLimit = B(250, 18, 70); p.Controls.Add(txtDailyLimit);
            p.Controls.Add(L("每日解锁次数上限（0=关闭）", 15, 55));
            txtUnlockLimit = B(250, 53, 70); p.Controls.Add(txtUnlockLimit);

            p.Controls.Add(L("多规则时段（留空 = 沿用旧「每日可用时段」，旧安装行为不变）", 15, 92));
            Label fmt = L("格式：1-5|17:00-21:00@quiz;6,7|08:00-22:00\n" +
                "星期 1=周一..7=周日；@quiz = 仅时段内需要答题（时段外自由、不扣时长）；\n" +
                "不带 @quiz = 仅时段内可用（时段外硬锁）；支持跨午夜 22:00-06:00。", 15, 115);
            fmt.ForeColor = Color.DimGray;
            p.Controls.Add(fmt);
            txtRules = B(15, 165, 500, 90);
            txtRules.Multiline = true;
            txtRules.ScrollBars = ScrollBars.Vertical;
            p.Controls.Add(txtRules);

            p.Controls.Add(L("护眼强制休息（分钟，0=关闭；时长用尽后进入）", 15, 266));
            txtRest = B(360, 264, 70); p.Controls.Add(txtRest);
            p.Controls.Add(L("家长临时放行（分钟）", 15, 300));
            txtTempPass = B(250, 298, 70); p.Controls.Add(txtTempPass);

            chkAntiTamper = new CheckBox();
            chkAntiTamper.Text = "检测系统时间大幅回拨并保护性锁定（可选加固，默认关）";
            chkAntiTamper.AutoSize = true;
            chkAntiTamper.Location = new Point(15, 330);
            p.Controls.Add(chkAntiTamper);

            Button grid = new Button();
            grid.Text = "打开时段格子编辑器";
            grid.Location = new Point(250, 326);
            grid.Size = new Size(180, 28);
            grid.Click += delegate
            {
                using (ScheduleGridForm g = new ScheduleGridForm())
                    if (g.ShowDialog(this) == DialogResult.OK) txtRules.Text = g.RuleText;
            };
            p.Controls.Add(grid);
        }

        void BuildQuizPage(TabPage p)
        {
            p.Controls.Add(L("每次锁屏题量（1-30，默认 10）", 15, 20));
            txtQCount = B(250, 18, 70); p.Controls.Add(txtQCount);
            p.Controls.Add(L("需连续答对题数（默认=题量，即全部答对；答错仍清零）", 15, 55));
            txtStreak = B(380, 53, 70); p.Controls.Add(txtStreak);

            chkFeedback = new CheckBox();
            chkFeedback.Text = "答错显示正确答案与解析（不改变「答错清零」）";
            chkFeedback.AutoSize = true;
            chkFeedback.Location = new Point(15, 90);
            p.Controls.Add(chkFeedback);

            p.Controls.Add(L("累计答错 N 题，增加 M 道加练题（0=关闭；跨天/重启保留）", 15, 128));
            txtExtraThreshold = B(250, 158, 60); p.Controls.Add(txtExtraThreshold);
            p.Controls.Add(L("N", 220, 160));
            txtExtraPer = B(380, 158, 60); p.Controls.Add(txtExtraPer);
            p.Controls.Add(L("M", 355, 160));

            chkStar = new CheckBox();
            chkStar.Text = "启用星级时长奖励（按题目年级/难度，1-5 星）";
            chkStar.AutoSize = true;
            chkStar.Location = new Point(15, 200);
            p.Controls.Add(chkStar);
            p.Controls.Add(L("1-5 星每题奖励分钟数：", 15, 234));
            for (int i = 0; i < 5; i++)
            {
                p.Controls.Add(L((i + 1) + "★", 165 + i * 72, 234));
                txtStars[i] = B(165 + i * 72, 258, 50);
                p.Controls.Add(txtStars[i]);
            }

            chkPractice = new CheckBox();
            chkPractice.Text = "启用练习模式（锁屏/托盘可进入；不解锁、不暂停倒计时）";
            chkPractice.AutoSize = true;
            chkPractice.Location = new Point(15, 305);
            p.Controls.Add(chkPractice);
        }

        void BuildBankPage(TabPage p)
        {
            p.Controls.Add(L("题库以程序目录下 chinese.txt / english.txt 为准（UTF-8，| 分隔）。", 15, 20));
            p.Controls.Add(L("JSON 用于批量备份/交换；导入会覆盖对应 txt，下次锁屏生效。", 15, 45));

            Button bExport = new Button();
            bExport.Text = "导出内置题库 JSON";
            bExport.Location = new Point(15, 85);
            bExport.Size = new Size(180, 34);
            bExport.Click += delegate { ExportBankJson(); };
            p.Controls.Add(bExport);

            Button bZh = new Button();
            bZh.Text = "导入语文 JSON";
            bZh.Location = new Point(210, 85);
            bZh.Size = new Size(150, 34);
            bZh.Click += delegate { ImportBankJson(true); };
            p.Controls.Add(bZh);

            Button bEn = new Button();
            bEn.Text = "导入英语 JSON";
            bEn.Location = new Point(370, 85);
            bEn.Size = new Size(150, 34);
            bEn.Click += delegate { ImportBankJson(false); };
            p.Controls.Add(bEn);

            Button bCheck = new Button();
            bCheck.Text = "题库质量检查";
            bCheck.Location = new Point(15, 140);
            bCheck.Size = new Size(180, 34);
            bCheck.Click += delegate { CheckBank(); };
            p.Controls.Add(bCheck);

            Button bUsage = new Button();
            bUsage.Text = "查看 7 天使用记录";
            bUsage.Location = new Point(210, 140);
            bUsage.Size = new Size(180, 34);
            bUsage.Click += delegate
            {
                using (UsageForm f = new UsageForm()) f.ShowDialog(this);
            };
            p.Controls.Add(bUsage);

            Button bExportCfg = new Button();
            bExportCfg.Text = "导出配置（换机）";
            bExportCfg.Location = new Point(15, 195);
            bExportCfg.Size = new Size(160, 32);
            bExportCfg.Click += delegate { ExportConfigToFile(); };
            p.Controls.Add(bExportCfg);

            Button bImportCfg = new Button();
            bImportCfg.Text = "导入配置";
            bImportCfg.Location = new Point(190, 195);
            bImportCfg.Size = new Size(160, 32);
            bImportCfg.Click += delegate { ImportConfigFromFile(); };
            p.Controls.Add(bImportCfg);

            Label note = L("质量门禁：题量、选项 2-4 个且互不重复、正确答案下标、\n" +
                "无占位符、题干去重、年级不超纲；导入前还会校验年级范围与重复词条。", 15, 240);
            note.ForeColor = Color.DimGray;
            p.Controls.Add(note);
        }

        void BuildLearnPage(TabPage p)
        {
            p.Controls.Add(L("难度倾向（80/20 加权）", 15, 20));
            cbDifficulty = new ComboBox();
            cbDifficulty.Location = new Point(150, 16);
            cbDifficulty.Size = new Size(120, 24);
            cbDifficulty.DropDownStyle = ComboBoxStyle.DropDownList;
            cbDifficulty.Items.AddRange(new object[] { "不限", "偏易", "中等", "偏难" });
            p.Controls.Add(cbDifficulty);

            p.Controls.Add(L("学期", 290, 20));
            cbTerm = new ComboBox();
            cbTerm.Location = new Point(340, 16);
            cbTerm.Size = new Size(100, 24);
            cbTerm.DropDownStyle = ComboBoxStyle.DropDownList;
            cbTerm.Items.AddRange(new object[] { "全学年", "上册", "下册" });
            p.Controls.Add(cbTerm);

            p.Controls.Add(L("题库名过滤（空=全部）", 15, 58));
            txtBank = B(160, 55, 150); p.Controls.Add(txtBank);

            chkMathChoice = new CheckBox();
            chkMathChoice.Text = "数学四选一";
            chkMathChoice.AutoSize = true; chkMathChoice.Location = new Point(15, 90);
            p.Controls.Add(chkMathChoice);
            chkChineseWord = new CheckBox();
            chkChineseWord.Text = "看拼音选词语";
            chkChineseWord.AutoSize = true; chkChineseWord.Location = new Point(160, 90);
            p.Controls.Add(chkChineseWord);
            chkTrueFalse = new CheckBox();
            chkTrueFalse.Text = "判断题";
            chkTrueFalse.AutoSize = true; chkTrueFalse.Location = new Point(310, 90);
            p.Controls.Add(chkTrueFalse);

            chkReview = new CheckBox();
            chkReview.Text = "间隔重复复习（错题到期优先，本地 review.json）";
            chkReview.AutoSize = true; chkReview.Location = new Point(15, 125);
            p.Controls.Add(chkReview);
            chkChildGrade = new CheckBox();
            chkChildGrade.Text = "允许孩子自选年级（仅影响下一批题）";
            chkChildGrade.AutoSize = true; chkChildGrade.Location = new Point(15, 155);
            p.Controls.Add(chkChildGrade);

            p.Controls.Add(L("解析停留秒数（0=不强制）", 15, 190));
            txtHold = B(170, 187, 60); p.Controls.Add(txtHold);
            p.Controls.Add(L("鼓励语", 250, 190));
            txtEncourage = B(310, 187, 180); p.Controls.Add(txtEncourage);

            p.Controls.Add(L("界面语言（新窗口生效）", 15, 225));
            cbLang = new ComboBox();
            cbLang.Location = new Point(170, 222);
            cbLang.Size = new Size(120, 24);
            cbLang.DropDownStyle = ComboBoxStyle.DropDownList;
            cbLang.Items.AddRange(new object[] { "中文", "English" });
            p.Controls.Add(cbLang);

            p.Controls.Add(L("节假日日期（yyyy-MM-dd，逗号分隔，按周日规则）", 15, 260));
            txtHolidays = B(15, 282, 490); p.Controls.Add(txtHolidays);

            Label note = L("学期 / 题库名来自外部词库可选列；内置题库无学期标签时按全学年处理。", 15, 315);
            note.ForeColor = Color.DimGray;
            p.Controls.Add(note);
        }

        void ExportConfigToFile()
        {
            string json = ConfigIo.ExportConfig();
            if (string.IsNullOrEmpty(json)) { MessageBox.Show(this, "导出失败（需管理员权限）。", "电脑锁"); return; }
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "JSON 文件|*.json";
                d.FileName = "pclock-config-" + DateTime.Now.ToString("yyyyMMdd") + ".json";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(d.FileName, json, new UTF8Encoding(false));
                    AuditLog.Write("配置导出：" + d.FileName);
                    MessageBox.Show(this, "已导出。文件内含家长 PIN / 卸载密码哈希，请妥善保管。", "电脑锁");
                }
                catch (Exception ex) { MessageBox.Show(this, "导出失败：" + ex.Message, "电脑锁"); }
            }
        }

        void ImportConfigFromFile()
        {
            if (MessageBox.Show(this, "导入会覆盖同名配置项（含密码哈希），确定继续？", "电脑锁",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "JSON 文件|*.json";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string err = ConfigIo.ImportConfig(File.ReadAllText(d.FileName, Encoding.UTF8));
                    if (err != null) { MessageBox.Show(this, "导入失败：" + err, "电脑锁"); return; }
                    AuditLog.Write("配置导入：" + d.FileName);
                    LoadValues(cbUser.Text);
                    MessageBox.Show(this, "已导入配置。运行态未导入，下次锁屏生效。", "电脑锁");
                }
                catch (Exception ex) { MessageBox.Show(this, "导入失败：" + ex.Message, "电脑锁"); }
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
            chkPractice.Checked = Store.IsPracticeEnabled(user);
            cbDifficulty.SelectedIndex = Store.GetPreferDifficulty(user);
            cbTerm.SelectedIndex = Store.GetTerm(user);
            cbLang.SelectedIndex = Store.GetLanguage() == "en" ? 1 : 0;
            txtBank.Text = Store.GetBankFilter(user);
            txtHold.Text = Store.GetFeedbackHoldSec(user).ToString();
            txtEncourage.Text = Store.GetEncourageText(user);
            txtHolidays.Text = Store.GetHolidayDates();
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

                // 先写题量，再写连续答对数（后者会按题量钳制）
                Store.SetQuestionCount(u, Int(txtQCount.Text, QuizConfig.DefaultQuestionCount));
                Store.SetRequiredStreak(u, Int(txtStreak.Text, Store.GetQuestionCount(u)));
                Store.SetFeedbackEnabled(u, chkFeedback.Checked);
                Store.SetExtraThreshold(u, Int(txtExtraThreshold.Text, 0));
                Store.SetExtraPerThreshold(u, Int(txtExtraPer.Text, 0));
                Store.SetStarRewardEnabled(u, chkStar.Checked);
                int[] rewards = new int[5];
                for (int i = 0; i < 5; i++) rewards[i] = Int(txtStars[i].Text, i + 1);
                Store.SetStarRewards(u, rewards);
                Store.SetPracticeEnabled(u, chkPractice.Checked);
                Store.SetPreferDifficulty(u, cbDifficulty.SelectedIndex);
                Store.SetTerm(u, cbTerm.SelectedIndex);
                Store.SetLanguage(cbLang.SelectedIndex == 1 ? "en" : "zh");
                Strings.Lang = Store.GetLanguage();
                Store.SetBankFilter(u, txtBank.Text);
                Store.SetFeedbackHoldSec(u, Int(txtHold.Text, 0));
                Store.SetEncourageText(u, txtEncourage.Text);
                Store.SetHolidayDates(txtHolidays.Text);
                Store.SetMathChoiceOn(u, chkMathChoice.Checked);
                Store.SetChineseWordOn(u, chkChineseWord.Checked);
                Store.SetTrueFalseOn(u, chkTrueFalse.Checked);
                Store.SetChildGradeOn(u, chkChildGrade.Checked);
                Store.SetReviewOn(u, chkReview.Checked);

                AuditLog.Write("高级规则设置修改：用户 " + u);
                hint.Text = "已保存。部分设置（题量/规则/题库）在下次锁屏或下次登录生效。";
                MessageBox.Show(this, "已保存为「" + u + "」的高级规则。", "电脑锁",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：" + ex.Message, "电脑锁");
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
                    MessageBox.Show(this, "已导出到：" + d.FileName, "电脑锁");
                }
                catch (Exception ex) { MessageBox.Show(this, "导出失败：" + ex.Message, "电脑锁"); }
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
                    MessageBox.Show(this, "已写入 " + path + "，下次锁屏生效。", "电脑锁");
                }
                catch (Exception ex) { MessageBox.Show(this, "导入失败：" + ex.Message, "电脑锁"); }
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
                "批次质量：" + (err == null ? "通过" : err) + Environment.NewLine +
                "年级上限：" + cap + Environment.NewLine +
                "内置语文词库：" + (bankErr == null ? "通过" : bankErr),
                "题库质量检查", MessageBoxButtons.OK,
                err == null && cap == "通过" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        static Label L(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            return l;
        }

        static TextBox B(int x, int y, int w) { return B(x, y, w, 24); }
        static TextBox B(int x, int y, int w, int h)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(w, h);
            return t;
        }
    }
}
