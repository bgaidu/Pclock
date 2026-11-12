using System;
using System.Collections.Generic;
using System.Globalization;

namespace PCLock
{
    /// <summary>
    /// 轻量多语言层：以中文原文为 key，未收录的条目原样返回。
    /// 动态文案请用 Fmt("&lt;中文模板&gt;", args)（模板本身也是 key），避免把中文片段拼进英文句子。
    /// 窗口构造末尾调用 Program.Localize(this) 统一翻译控件文字，新增界面无需逐条赋值。
    /// 语言偏好存注册表，默认中文；切换后所有已打开窗口通过 LanguageChanged 即时刷新。
    /// </summary>
    public static class Strings
    {
        static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ===== 通用 =====
            { "电脑锁", "PCLock" },
            { "电脑锁 - 运行中", "PCLock - Running" },
            { "电脑锁 - 已暂停", "PCLock - Paused" },
            { "电脑锁 - 自由使用（不计时）", "PCLock - Free use (not counted)" },
            { "电脑锁 - 剩余 {0}", "PCLock - {0} left" },
            { "导出诊断包…", "Export diagnostics..." },
            { "已切换为中文，所有已打开的窗口立即生效。", "Switched to Chinese. All open windows updated." },
            { "已切换为英文，所有已打开的窗口立即生效。", "Switched to English. All open windows updated." },
            { "近 30 天合计：{0} 小时 {1} 分\n最弱题目（作答≥3 次）：{2}\n本地异常摘要：{3}",
              "Past 30 days: {0} h {1} min\nWeakest questions (>=3 attempts): {2}\nLocal error summary: {3}" },
            { "诊断包已导出：{0}", "Diagnostics saved: {0}" },
            { "诊断包导出失败：{0}", "Diagnostics export failed: {0}" },
            { "{0} 小时 {1} 分", "{0} h {1} min" },
            { "{0} 分 {1} 秒", "{0} min {1} s" },
            { "已练 {0} 题 · 答对 {1} 题 · 连对 {2} 题", "Practiced {0} · correct {1} · streak {2}" },
            { "密码错误（还可尝试 {0} 次）", "Wrong PIN ({0} attempts left)" },
            { "尝试次数过多，请 {0} 秒后再试", "Too many attempts. Retry in {0}s" },
            { "今日已用：{0} 分 {1} 秒", "Used today: {0} min {1} s" },
            { "「{0}」今日已用：{1} 分 {2} 秒", "{0} - used today: {1} min {2} s" },
            { "密码需为 4-12 位，可包含数字、字母和特殊符号，不能含空格。",
              "Passwords must be 4-12 characters (letters, digits, symbols) with no spaces." },
            { "将删除「{0}」的全部时长/时段/答题/统计配置（不影响其他用户），确定？",
              "Delete all time/window/quiz/stat settings for {0}? Other users are unaffected." },
            { "已删除 {0} 项配置。默认值将在下次启动/设置时生效。",
              "Removed {0} settings. Defaults apply at the next start or settings change." },
            { "已为「{0}」保存。下次该用户登录时生效。", "Saved for {0}. It applies the next time that user signs in." },
            { "已为「{0}」保存。下次该用户锁屏生效（当前锁屏不中断）。",
              "Saved for {0}. It applies at that user's next lock (no interruption now)." },
            { "已为「{0}」保存可用时段。", "Allowed hours saved for {0}." },
            { "已保存为「{0}」的高级规则。", "Advanced rules saved for {0}." },
            { "已导出到：{0}", "Exported to: {0}" },
            { "已写入 {0}，下次锁屏生效。", "Written to {0}; applies at the next lock." },
            { "题库质量检查", "Check bank quality" },
            { "批次质量：{0}", "Batch quality: {0}" },
            { "年级上限：{0}", "Grade cap: {0}" },
            { "内置语文词库：{0}", "Built-in Chinese bank: {0}" },
            { "电脑锁 v{0} - 家长设置", "PCLock v{0} - Parent settings" },
            { "电脑锁 v{0}", "PCLock v{0}" },
            { "电脑锁 v{0} ({1})", "PCLock v{0} ({1})" },
            { "电脑锁 - 首次使用，请设置密码", "PCLock - First run: set your passwords" },
            { "关于", "About" },
            { "确定", "OK" },
            { "确 定", "OK" },
            { "取消", "Cancel" },
            { "返回", "Back" },
            { "关闭", "Close" },
            { "保存全部", "Save all" },
            { "验证中…", "Verifying…" },
            { "暂无数据", "No data" },

            // ===== 锁屏 =====
            { "时间已到，已锁定", "Time is up - locked" },
            { "当前不在使用时段", "Outside allowed hours" },
            { "时段开始后自动解锁，或由家长输入 PIN 解锁。", "Unlocks automatically when the window starts, or with the parent PIN." },
            { "家长入口", "Parent entry" },
            { "家长设置", "Parent settings" },
            { "练习模式", "Practice" },
            { "继续答题", "Continue" },
            // 硬锁原因是运行期从 App 传入的中文短语，也登记进词典才能即时翻译
            { "今日使用额度已用完", "Daily usage quota reached" },
            { "护眼休息时间", "Eye-rest break" },
            { "系统时间异常，保护性锁定", "System clock changed - protective lock" },
            { "当前规则：", "Current rules:" },
            { "允许使用时段：", "Allowed hours:" },
            { "看完正确答案与解析后，点「继续答题」做下一题", "Read the answer, then click Continue for the next question" },
            { "解 锁", "Unlock" },
            { "解锁", "Unlock" },
            { "回答错误", "Wrong answer" },
            { "答对了！", "Correct!" },
            { "答错了。", "Wrong." },
            { "当前不在允许使用时段", "Currently outside allowed hours" },
            { "请使用家长 PIN 解锁", "Use the parent PIN to unlock" },
            { "休息结束后自动解锁，或由家长输入 PIN / 临时放行。", "Unlocks when rest ends, or with the parent PIN / temporary pass." },
            { "请输入数字答案", "Enter a number" },
            { " + {0} 道加练题", " + {0} extra" },
            { "（加练）", " (extra)" },
            { "共 {0} 题{1}（{2}），需答对 {3} 道{4}（当前第 {5} 题，已得 {6} 星）",
              "{0} questions{1} ({2}); {3} correct required{4} (now #{5}, stars {6})" },
            { "回答错误，扣 1 星，请 {0} 秒后点「继续答题」",
              "Wrong answer. -1 star. Click Continue in {0}s" },
            { "回答错误，扣 1 星，请 {0} 秒后再试", "Wrong answer. -1 star. Retry in {0}s" },
            { "回答错误，扣 1 星", "Wrong answer. -1 star." },
            { "回答错误，请 {0} 秒后再试", "Wrong answer. Retry in {0}s" },
            { "PIN 错误（还可尝试 {0} 次）", "Wrong PIN ({0} attempts left)" },
            { "PIN 错误次数过多，请 {0} 秒后再试", "Too many wrong PINs. Retry in {0}s" },
            { "家长 PIN：", "Parent PIN: " },
            { "　距下次可用还有 {0}", " Next available in {0}" },
            { "正确答案：{0}", "Correct answer: {0}" },
            { "　（{0}）", " ({0})" },
            { "　解析：{0}", " Why: {0}" },
            { "功能说明：锁屏后需答对 {0} 道题（数学填答案，语文/英语四选一，答错扣 1 星并补一道新题），或由家长输入 PIN 解锁。",
              "How it works: answer {0} questions correctly (math typed, Chinese/English multiple choice; a mistake costs 1 star and adds a new question), or unlock with the parent PIN." },

            // ===== 托盘菜单 =====
            { "设置…（需家长PIN）", "Settings… (parent PIN)" },
            { "家长验证", "Parent verification" },
            { "立即锁定", "Lock now" },
            { "练习模式（不解锁）", "Practice (no unlock)" },
            { "使用记录（7 天）", "Usage (last 7 days)" },
            { "请输入家长 PIN：", "Enter parent PIN:" },
            { "当前仍在使用默认密码 1234，请立即修改家长 PIN 与卸载密码。",
              "The default password 1234 is still in use. Change the parent PIN and uninstall password now." },

            // ===== 首次运行 =====
            { "首次使用必须设置两个密码，且不能沿用默认 1234。\n", "Two passwords are required on first run; the default 1234 is not allowed.\n" },
            { "首次使用必须设置密码后才能开始使用电脑锁。", "Set the passwords before PCLock can start." },
            { "不能继续使用默认密码 1234，请设置自己的密码。", "The default 1234 cannot be reused. Choose your own password." },
            { "家长 PIN", "Parent PIN" },
            { "卸载密码", "Uninstall password" },
            { "再输一遍", "Repeat" },
            { "保存并开始使用", "Save and start" },
            { "两次输入不一致，请重新确认。", "The two entries do not match. Please try again." },
            { "家长 PIN 与卸载密码建议设置为不同值。", "Use different values for the parent PIN and the uninstall password." },
            { "首次运行：家长 PIN 与卸载密码已由家长设置", "First run: parent PIN and uninstall password have been set" },

            // ===== 练习 / 使用记录 =====
            { "练习模式（不计入解锁，倒计时继续）", "Practice (does not unlock; countdown continues)" },
            { "练习模式：答对/答错都会显示正确答案，可随时换题或退出。", "Practice: the correct answer is always shown; switch questions or exit at any time." },
            { "换一题", "Next question" },
            { "退出练习", "Exit practice" },
            { "使用记录 - 最近 7 天", "Usage - last 7 days" },
            { "今日已用：", "Used today: " },
            { "近 30 天合计：", "Last 30 days total: " },
            { "最弱题目（作答≥3 次）：", "Weakest questions (3+ attempts): " },
            { "本地异常摘要：", "Local crash summary: " },
            { "无 err.txt", "no err.txt" },
            { "导出 CSV", "Export CSV" },
            { "已导出到：", "Exported to: " },
            { "导出失败：", "Export failed: " },

            // ===== 时段格子 =====
            { "时段格子编辑器", "Schedule grid editor" },
            { "点击格子切换：深色 = 管控生效时段；每行一天、每列一小时。",
              "Click a cell to toggle: dark = managed hours; one row per day, one column per hour." },
            { "使用此规则", "Use this rule" },
            { "仅时段内需要答题（时段外自由使用）", "Quiz only during the window (free outside)" },
            { "周一", "Mon" }, { "周二", "Tue" }, { "周三", "Wed" }, { "周四", "Thu" },
            { "周五", "Fri" }, { "周六", "Sat" }, { "周日", "Sun" },

            // ===== 家长设置（主窗口）=====
            { " - 家长设置", " - Parent settings" },
            { "为以下用户设置：", "Settings apply to: " },
            { "配置对象（Windows 用户）", "Target (Windows user)" },
            { "每次可用时长（按用户）", "Session length (per user)" },
            { "每日可用时段（按用户）", "Allowed hours (per user)" },
            { "本次会话（今日已用 / 暂停 / 加时）", "This session (used today / pause / extra time)" },
            { "锁屏答题（年级 / 科目，可多选）", "Quiz (grade / subject, multi-select)" },
            { "保存时长", "Save length" },
            { "保存时段", "Save hours" },
            { "保存设置", "Save settings" },
            { "本次 +15 分钟", "Add 15 minutes" },
            { "暂停计时", "Pause timer" },
            { "继续计时", "Resume timer" },
            { "修改 PIN", "Change PIN" },
            { "修改密码", "Change passwords" },
            { "修改家长密码（4-12 位字母/数字/符号，锁屏家长入口用）", "Change parent PIN (4-12 chars; used at the lock screen)" },
            { "修改卸载密码（4-12 位字母/数字/符号，卸载时必须输入）", "Change uninstall password (4-12 chars; required to uninstall)" },
            { "删除当前用户配置", "Delete this user's settings" },
            { "删除用户配置：", "Delete settings for: " },
            { "卸载验证", "Uninstall check" },
            { "卸载并删除程序", "Uninstall and remove files" },
            { "高级规则", "Advanced rules" },
            { "使用记录", "Usage" },
            { "语言", "Language" },
            { "中文", "Chinese" },
            { "年级：", "Grade: " },
            { "科目：", "Subject: " },
            { "全天", "All day" },
            { "数学", "Math" },
            { "语文", "Chinese" },
            { "英语", "English" },
            { "退出程序", "Exit" },
            { "今日已用：计算中…", "Used today: calculating…" },
            { "格式：08:00-12:00;14:00-21:00（分号分隔多段，留空=全天可用；支持跨午夜如 22:00-06:00）",
              "Format: 08:00-12:00;14:00-21:00 (segments split by ';', empty = all day; midnight crossing like 22:00-06:00 is supported)" },
            { "已暂停计时：孩子仍可使用电脑，但剩余时间不再减少。", "Timer paused: the computer stays usable and remaining time no longer decreases." },
            { "已继续计时。", "Timer resumed." },
            { "已为本次会话增加 15 分钟使用时间。", "Added 15 minutes to this session." },
            { "已保存。本次剩余时间已重置。", "Saved. Remaining time for this session has been reset." },
            { "请先启动电脑锁主程序后再操作。", "Start PCLock first, then try again." },
            { "请先启动电脑锁主程序后再锁定。", "Start PCLock first, then lock." },
            { "请先启动电脑锁主程序后再卸载。", "Start PCLock first, then uninstall." },
            { "主程序未运行，无需退出。", "PCLock is not running; nothing to exit." },
            { "请至少选择一个年级。", "Select at least one grade." },
            { "请至少选择一个科目。", "Select at least one subject." },
            { "两次输入的 PIN 不一致。", "The two PIN entries do not match." },
            { "两次输入的密码不一致。", "The two password entries do not match." },
            { "家长 PIN 已修改。", "Parent PIN changed." },
            { "卸载密码已修改。", "Uninstall password changed." },
            { "确定退出电脑锁？退出后本次不再计时，下次开机自动重新开始。",
              "Exit PCLock? The timer stops for this session and restarts on the next boot." },
            { "确定卸载并删除电脑锁的全部文件和设置吗？", "Uninstall PCLock and delete all files and settings?" },
            { "请输入卸载密码：", "Enter uninstall password: " },
            { "新 PIN：", "New PIN: " },
            { "新密码：", "New password: " },
            { "再输一遍：", "Repeat: " },

            // ===== 高级规则 =====
            { "高级规则 - 家长设置", "Advanced rules - Parent settings" },
            { "配置对象：", "Target: " },
            { "时间与额度", "Time and quota" },
            { "答题与奖励", "Quiz and rewards" },
            { "学习与体验", "Learning and UX" },
            { "题库与记录", "Banks and logs" },
            { "每日总时长上限（分钟，0=关闭）", "Daily total limit (minutes, 0 = off)" },
            { "每日解锁次数上限（0=关闭）", "Daily unlock limit (0 = off)" },
            { "护眼强制休息（分钟，0=关闭；时长用尽后进入）", "Eye-rest break (minutes, 0 = off; starts when time runs out)" },
            { "家长临时放行（分钟）", "Parent temporary pass (minutes)" },
            { "多规则时段（留空 = 沿用旧「每日可用时段」，旧安装行为不变）",
              "Multi-rule hours (empty = keep the legacy daily window; old installs unchanged)" },
            { "格式：1-5|17:00-21:00@quiz;6,7|08:00-22:00\n", "Format: 1-5|17:00-21:00@quiz;6,7|08:00-22:00\n" },
            { "星期 1=周一..7=周日；@quiz = 仅时段内需要答题（时段外自由、不扣时长）；\n",
              "Weekday 1=Mon..7=Sun; @quiz = quiz only inside the window (free and not counted outside);\n" },
            { "不带 @quiz = 仅时段内可用（时段外硬锁）；支持跨午夜 22:00-06:00。",
              "Without @quiz = usable only inside the window (hard lock outside); midnight crossing like 22:00-06:00 is supported." },
            { "打开时段格子编辑器", "Open schedule grid editor" },
            { "检测系统时间大幅回拨并保护性锁定（可选加固，默认关）", "Detect large clock rollbacks and lock protectively (optional, off by default)" },
            { "每次锁屏题量（1-30，默认 10）", "Questions per lock (1-30, default 10)" },
            { "需答对题数（默认=题量，即全部答对；答错扣 1 星，最低 0 星）",
              "Correct answers required (default = question count; a wrong answer costs 1 star, never below 0)" },
            { "答错显示正确答案与解析（不改变扣星与补题规则）", "Show the correct answer after a mistake (the star rule is unchanged)" },
            { "累计答错 N 题，增加 M 道加练题（0=关闭；跨天/重启保留）",
              "Every N mistakes adds M practice questions (0 = off; survives days and restarts)" },
            { "启用星级时长奖励（按题目年级/难度，1-5 星）", "Enable star time rewards (1-5 stars by grade/difficulty)" },
            { "1-5 星每题奖励分钟数：", "Reward minutes per star (1-5): " },
            { "启用练习模式（锁屏/托盘可进入；不解锁、不暂停倒计时）",
              "Enable practice mode (from lock screen or tray; does not unlock or pause)" },
            { "难度倾向（80/20 加权）", "Difficulty bias (80/20 weighting)" },
            { "不限", "Any" }, { "偏易", "Easier" }, { "中等", "Medium" }, { "偏难", "Harder" },
            { "学期", "Term" }, { "全学年", "Full year" }, { "上册", "First semester" }, { "下册", "Second semester" },
            { "题库名过滤（空=全部）", "Bank name filter (empty = all)" },
            { "数学四选一", "Math multiple choice" },
            { "看拼音选词语", "Pick the word for the pinyin" },
            { "判断题", "True / false" },
            { "间隔重复复习（错题到期优先，本地 review.json）", "Spaced repetition (due mistakes first; local review.json)" },
            { "允许孩子自选年级（仅影响下一批题）", "Let the child pick the grade (next batch only)" },
            { "解析停留秒数（0=不强制）", "Answer explanation hold (seconds, 0 = off)" },
            { "鼓励语", "Encouragement text" },
            { "界面语言（新窗口生效）", "UI language (new windows)" },
            { "节假日日期（yyyy-MM-dd，逗号分隔，按周日规则）", "Holidays (yyyy-MM-dd, comma separated, treated as Sunday)" },
            { "日志保留天数（0=永久，全局）", "Log retention days (0 = forever, global)" },
            { "默认 90 天：启动时清理更早的 logs\\*.log，删除失败静默。", "Default 90 days: older logs\\*.log are removed at startup; failures are silent." },
            { "学期 / 题库名来自外部词库可选列；内置题库无学期标签时按全学年处理。",
              "Term / bank come from optional columns in external banks; built-in banks without a term count as full-year." },
            { "题库以程序目录下 chinese.txt / english.txt 为准（UTF-8，| 分隔）。",
              "Banks are read from chinese.txt / english.txt next to the program (UTF-8, '|' separated)." },
            { "多题库：把 chinese.txt / english.txt 拆成 banks\\<题库名>.txt，文件名即题库名。",
              "Multiple banks: split chinese.txt / english.txt into banks\\<name>.txt; the file name is the bank name." },
            { "可用题库：", "Available banks: " },
            { "通过", "Pass" }, { "失败（年级超纲）", "Fail (grade out of scope)" },
            { "批次质量：", "Batch quality: " },
            { "内置语文词库：", "Built-in Chinese bank: " },
            { "英语题库为空或格式不符", "English bank is empty or malformed" },
            { "质量门禁：题量、选项 2-4 个且互不重复、正确答案下标、\n", "Quality gate: question count, 2-4 unique options, correct index,\n" },
            { "无占位符、题干去重、年级不超纲；导入前还会校验年级范围与重复词条。",
              "no placeholders, no duplicate prompts, grade in scope; imports also check grade range and duplicates." },
            { "导入英语 JSON", "Import English JSON" },
            { "导入语文 JSON", "Import Chinese JSON" },
            { "导出内置题库 JSON", "Export built-in banks (JSON)" },
            { "JSON 用于批量备份/交换；导入会覆盖对应 txt，下次锁屏生效。",
              "JSON is for bulk backup/exchange; importing overwrites the matching txt and applies at the next lock." },
            { "导入配置", "Import settings" },
            { "导出配置（换机）", "Export settings (new machine)" },
            { "查看 7 天使用记录", "View 7-day usage" },
            { "所有新增规则默认关闭；修改后按用户生效。", "New rules are off by default and apply per user." },
            { "已保存。部分设置（题量/规则/题库）在下次锁屏或下次登录生效。",
              "Saved. Some settings (counts/rules/banks) apply at the next lock or logon." },
            { "导入会覆盖同名配置项（含密码哈希），确定继续？", "Importing overwrites matching settings (including password hashes). Continue?" },
            { "已导入配置。运行态未导入，下次锁屏生效。", "Settings imported. Runtime state is not imported; it applies at the next lock." },
            { "已导出。文件内含家长 PIN / 卸载密码哈希，请妥善保管。",
              "Exported. The file contains password hashes - keep it safe." },
            { "导出失败（需管理员权限）。", "Export failed (administrator required)." },
            { "保存失败：", "Save failed: " },
            { "导入失败：", "Import failed: " }
        };

        // 语言偏好是全局状态，但本类属于纯逻辑层（tests\run.bat 编译），
        // 不能引用任何 WinForms 类型。界面刷新通过 LanguageChanged 事件解耦，
        // 由 Program 订阅并统一重刷已打开窗口的文案。
        static string lang = "zh";

        public static event Action LanguageChanged;

        public static string Lang
        {
            get { return lang; }
            set { SetLanguage(value); }   // 兼容旧代码直接赋值
        }

        public static bool IsEnglish { get { return lang == "en"; } }

        /// <summary>
        /// 切换语言；值未变化时不触发事件（幂等）。
        /// 返回 true 表示确实发生了变化、已通知所有订阅者。
        /// </summary>
        public static bool SetLanguage(string value)
        {
            string v = (value == "en") ? "en" : "zh";
            if (v == lang) return false;
            lang = v;
            Action h = LanguageChanged;
            if (h != null) h();
            return true;
        }

        /// <summary>该中文文案是否收录在词典中；仅收录项才会被 Localize 缓存原文。</summary>
        public static bool Contains(string zh)
        {
            return !string.IsNullOrEmpty(zh) && En.ContainsKey(zh);
        }

        /// <summary>翻译一段固定文案；未收录时原样返回中文</summary>
        public static string Get(string zh)
        {
            if (Lang == "en" && !string.IsNullOrEmpty(zh))
            {
                string v;
                if (En.TryGetValue(zh, out v)) return v;
            }
            return zh;
        }

        /// <summary>
        /// 带占位符的文案：模板本身也是词典 key，英文模式下模板与参数一起格式化，
        /// 避免出现「Used today: 12 分」这类中英混排。
        /// </summary>
        public static string Fmt(string zhTemplate, params object[] args)
        {
            string tpl = Get(zhTemplate);
            if (args == null || args.Length == 0) return tpl;
            try { return string.Format(CultureInfo.InvariantCulture, tpl, args); }
            catch (FormatException) { return tpl; }
        }

        /// <summary>词条数量（供单测/自检，避免词典被误删空）</summary>
        public static int Count { get { return En.Count; } }

        /// <summary>全部中文 key（供单测核对覆盖率，避免新增界面漏登记）</summary>
        public static string[] AllKeys
        {
            get
            {
                string[] r = new string[En.Count];
                En.Keys.CopyTo(r, 0);
                return r;
            }
        }
    }
}
