using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PCLock;
using TUnit;

/// <summary>第四批：运行期维护（日志保留期、统计键上限）与错题库混入的纯逻辑回归。</summary>
public class Batch4Tests
{
    // ===== 审计日志保留期 =====

    [Test]
    public void ExpiredLogsKeepCutoffDay()
    {
        DateTime today = new DateTime(2026, 11, 11);
        string[] files = new string[]
        {
            @"C:\logs\2026-08-01.log",   // 101 天前 → 删
            @"C:\logs\2026-08-13.log",   // 正好 90 天前 → 保留（cutoff 当天）
            @"C:\logs\2026-11-10.log",   // 昨天 → 保留
            @"C:\logs\笔记.txt",          // 非日志 → 忽略
            @"C:\logs\readme.log"         // 非日期命名 → 忽略
        };
        List<string> drop = Housekeeping.SelectExpiredLogs(files, today, 90);
        Assert.AreEqual(1, drop.Count);
        Assert.IsTrue(drop[0].EndsWith("2026-08-01.log"), "只删 90 天之前的那个");
    }

    [Test]
    public void ExpiredLogsDisabledWhenKeepDaysZero()
    {
        DateTime today = new DateTime(2026, 11, 11);
        string[] files = new string[] { @"C:\logs\2020-01-01.log" };
        Assert.AreEqual(0, Housekeeping.SelectExpiredLogs(files, today, 0).Count, "0=永久保留");
        Assert.AreEqual(0, Housekeeping.SelectExpiredLogs(null, today, 90).Count);
    }

    [Test]
    public void ParseLogDayHandlesNamesAndDates()
    {
        DateTime d;
        Assert.IsTrue(Housekeeping.TryParseLogDay("2026-11-11.log", out d));
        Assert.AreEqual(new DateTime(2026, 11, 11), d);
        Assert.IsTrue(Housekeeping.TryParseLogDay(@"D:\x\2026-01-02.LOG", out d));
        Assert.AreEqual(new DateTime(2026, 1, 2), d);
        Assert.IsFalse(Housekeeping.TryParseLogDay("2026-13-40.log", out d), "非法日期");
        Assert.IsFalse(Housekeeping.TryParseLogDay("20261111.log", out d), "格式不对");
        Assert.IsFalse(Housekeeping.TryParseLogDay("", out d));
    }

    [Test]
    public void ParseLogDayAcceptsTypePrefixedNames()
    {
        DateTime d;
        Assert.IsTrue(Housekeeping.TryParseLogDay("audit-2026-11-12.log", out d), "审计日志前缀");
        Assert.AreEqual(new DateTime(2026, 11, 12), d);
        Assert.IsTrue(Housekeeping.TryParseLogDay("error-2026-11-12.log", out d), "错误日志前缀");
        Assert.AreEqual(new DateTime(2026, 11, 12), d);
        Assert.IsTrue(Housekeeping.TryParseLogDay("watchdog-crash-2026-11-12.log", out d), "看门狗崩溃日志前缀");
        Assert.AreEqual(new DateTime(2026, 11, 12), d);
        Assert.IsFalse(Housekeeping.TryParseLogDay("readme.log", out d), "无日期仍忽略");
        Assert.IsFalse(Housekeeping.TryParseLogDay("audit-2026-11-12.txt", out d), "非 .log 仍忽略");
    }

    // ===== 统计键上限 =====

    [Test]
    public void SelectKeysToDropRespectsCap()
    {
        string[] keys = new string[] { "a", "b", "c", "d" };
        string[] vals = new string[] { "5|5|5", "1|1|0", "9|3|3", "2|2|1" };
        List<string> drop = Housekeeping.SelectKeysToDrop(keys, vals, 2);
        Assert.AreEqual(2, drop.Count, "4 → 2 应删 2 条");
        Assert.IsTrue(drop.Contains("b"), "作答次数最少（1 次）的最先删");
        Assert.IsTrue(drop.Contains("d"), "作答 2 次其次");
        Assert.IsFalse(drop.Contains("a"), "作答 5 次的保留");
    }

    [Test]
    public void SelectKeysToDropPrefersDroppingHighAccuracy()
    {
        // 作答次数相同：正确率高的先删（孩子已掌握，留着价值最低）
        string[] keys = new string[] { "good", "bad" };
        string[] vals = new string[] { "5|5|5", "5|5|1" };
        List<string> drop = Housekeeping.SelectKeysToDrop(keys, vals, 1);
        Assert.AreEqual(1, drop.Count);
        Assert.AreEqual("good", drop[0]);
    }

    [Test]
    public void SelectKeysToDropNoopWhenUnderCap()
    {
        string[] keys = new string[] { "a", "b" };
        string[] vals = new string[] { "1|1|1", "1|1|0" };
        Assert.AreEqual(0, Housekeeping.SelectKeysToDrop(keys, vals, 5).Count);
        Assert.AreEqual(2, Housekeeping.SelectKeysToDrop(keys, vals, 0).Count, "cap=0 时全部可删");
        Assert.AreEqual(0, Housekeeping.SelectKeysToDrop(null, null, 5).Count);
    }

    [Test]
    public void ShouldPruneOnlyAfterBuffer()
    {
        Assert.IsFalse(Housekeeping.ShouldPruneStat(600, 600), "刚好等于上限不清");
        Assert.IsFalse(Housekeeping.ShouldPruneStat(640, 600), "缓冲区内不清（避免频繁枚举注册表）");
        Assert.IsTrue(Housekeeping.ShouldPruneStat(651, 600));
    }

    // ===== 错题库按用户隔离 / 混入去重 =====

    [Test]
    public void ReviewFileIsPerUserAndPathSafe()
    {
        string a = ReviewBank.FileNameFor("Alice");
        string b = ReviewBank.FileNameFor("Bob");
        Assert.IsFalse(a == b, "不同用户必须不同文件（原先共用 review.json）");
        Assert.AreEqual("review-Alice.json", a);
        Assert.IsFalse(ReviewBank.FileNameFor("a/b:c*?").IndexOfAny(new char[] { '/', ':', '*' }) >= 0,
            "非法路径字符应被替换");
        Assert.AreEqual("review-default.json", ReviewBank.FileNameFor(""));
    }

    static ReviewItem Item(string key, long due, string prompt)
    {
        ReviewItem it = new ReviewItem();
        it.Key = key;
        it.DueUtcTicks = due;
        Question q = new Question();
        q.Prompt = prompt;
        q.Type = QType.MathCalc;
        q.Answer = 1;
        it.Q = q;
        return it;
    }

    [Test]
    public void MixSkipsNotDueAndAlreadyPresent()
    {
        long now = 1000;
        List<ReviewItem> items = new List<ReviewItem>();
        items.Add(Item("k1", 500, "到期题1"));      // 到期 → 可选
        items.Add(Item("k2", 5000, "未到期"));      // 未到期 → 跳过
        items.Add(Item("k3", 500, "本批已有"));      // 已在本批 → 跳过
        items.Add(Item("k4", 500, "到期题2"));      // 到期 → 可选
        List<Question> pick = ReviewBank.SelectDueToMix(items, new string[] { "k3" }, now, 10);
        Assert.AreEqual(2, pick.Count);
        Assert.AreEqual("到期题1", pick[0].Prompt);
        Assert.AreEqual("到期题2", pick[1].Prompt);
    }

    [Test]
    public void MixStopsAtMaxCount()
    {
        long now = 1000;
        List<ReviewItem> items = new List<ReviewItem>();
        for (int i = 0; i < 10; i++) items.Add(Item("k" + i, 500, "题" + i));
        Assert.AreEqual(3, ReviewBank.SelectDueToMix(items, new string[0], now, 3).Count);
    }

    [Test]
    public void MixReplacesWithoutDuplicatingBatch()
    {
        // 台账里两条一样的 key：只能混入一次，且不能覆盖出重复题
        long now = 1000;
        List<ReviewItem> items = new List<ReviewItem>();
        items.Add(Item("dup", 500, "同一题"));
        items.Add(Item("dup", 500, "同一题"));
        List<Question> qs = new List<Question>();
        string[] existing = new string[3];
        for (int i = 0; i < 3; i++)
        {
            Question q = new Question();
            q.Prompt = "原题" + i; q.Type = QType.MathCalc; q.Answer = i;
            qs.Add(q);
            existing[i] = QuestionStats.KeyOf(q);
        }
        List<Question> pick = ReviewBank.SelectDueToMix(items, existing, now, 2);
        Assert.AreEqual(1, pick.Count, "重复 key 只应选入一次");
        int used = ReviewBank.ApplyMix(qs, pick);
        Assert.AreEqual(1, used);
        Assert.AreEqual("同一题", qs[0].Prompt);
        Assert.AreEqual("原题1", qs[1].Prompt);
    }

    // ===== 题量钳制 / 判断题题干 =====

    [Test]
    public void EffectiveStreakClampsToAvailable()
    {
        Assert.AreEqual(10, QuizConfig.EffectiveStreak(10, 10));
        Assert.AreEqual(3, QuizConfig.EffectiveStreak(10, 3), "题库只有 3 题时不能要求连对 10 题");
        Assert.AreEqual(5, QuizConfig.EffectiveStreak(5, 10));
        Assert.AreEqual(10, QuizConfig.EffectiveStreak(0, 10), "未配置=全部答对");
        Assert.AreEqual(1, QuizConfig.EffectiveStreak(10, 0));
    }

    [Test]
    public void TrueFalsePromptHasSingleEquals()
    {
        for (int i = 0; i < 60; i++)
        {
            List<Question> batch = QuestionBank.GetBatchEx(new int[] { 3 }, new Subject[] { Subject.Math },
                5, 0, 0, "", false, false, true);
            foreach (Question q in batch)
            {
                if (q.Type != QType.TrueFalse) continue;
                Assert.IsFalse(q.Prompt.Contains("= ? ="), "判断题题干不应出现两个等号：" + q.Prompt);
                Assert.IsTrue(q.Prompt.EndsWith("，对吗？"), "判断题题干格式：" + q.Prompt);
                Assert.AreEqual(2, q.Options.Length);
                Assert.IsTrue(q.CorrectIndex == 0 || q.CorrectIndex == 1);
            }
        }
    }

    // ===== 多语言（Fmt 模板 + 词典） =====

    [Test]
    public void StringsFmtTranslatesTemplateAndArgs()
    {
        string old = Strings.Lang;
        try
        {
            Strings.Lang = "en";
            Assert.AreEqual("PCLock - Running", Strings.Get("电脑锁 - 运行中"));
            Assert.AreEqual("Practiced 3 · correct 2 · streak 1",
                Strings.Fmt("已练 {0} 题 · 答对 {1} 题 · 连对 {2} 题", 3, 2, 1));
            Assert.AreEqual("PCLock - 1 h 2 min left",
                Strings.Fmt("电脑锁 - 剩余 {0}", Strings.Fmt("{0} 小时 {1} 分", 1, 2)));
            Assert.AreEqual("Used today: 1 min 2 s", Strings.Fmt("今日已用：{0} 分 {1} 秒", 1, 2));
        }
        finally { Strings.Lang = old; }
    }

    [Test]
    public void StringsFmtKeepsUnknownTemplatesAndBadArgs()
    {
        string old = Strings.Lang;
        try
        {
            Strings.Lang = "en";
            Assert.AreEqual("未收录 {0}", Strings.Get("未收录 {0}"), "未收录的模板原样返回中文");
            Assert.AreEqual("未收录 7", Strings.Fmt("未收录 {0}", 7), "未收录的模板仍按参数格式化");
            Assert.AreEqual("{1}", Strings.Fmt("{1}", "only-one-arg"), "占位符越界时回退模板而不抛异常");
        }
        finally { Strings.Lang = old; }
    }

    [Test]
    public void StringsDictionaryIsNotEmptyAndChineseIsIdentity()
    {
        string old = Strings.Lang;
        try
        {
            Strings.Lang = "zh";
            Assert.AreEqual("电脑锁 - 运行中", Strings.Get("电脑锁 - 运行中"), "中文模式原样返回");
            Strings.Lang = "en";
            Assert.GreaterOrEqual(Strings.Count, 150, "词典条目数（覆盖锁屏/托盘/设置/高级规则）");
        }
        finally { Strings.Lang = old; }
    }

    [Test]
    public void StringsCoversEveryWindow()
    {
        string old = Strings.Lang;
        try
        {
            Strings.Lang = "en";
            string[] must = new string[]
            {
                "电脑锁 - 运行中", "设置…（需家长PIN）", "立即锁定", "使用记录（7 天）",
                "高级规则 - 家长设置", "时间与额度", "答题与奖励", "题库与记录",
                "每日可用时段（按用户）", "保存全部", "时段格子编辑器", "使用记录 - 最近 7 天",
                "保存并开始使用", "退出练习", "练习模式（不计入解锁，倒计时继续）"
            };
            foreach (string key in must)
                Assert.IsFalse(Strings.Get(key) == key, "缺少英文词条：" + key);
        }
        finally { Strings.Lang = old; }
    }

    [Test]
    public void StringsAllKeysAreUnique()
    {
        string[] keys = Strings.AllKeys;
        Assert.GreaterOrEqual(keys.Length, 150);
        // Dictionary 本身不允许重复 key（重复会在静态构造时抛异常），这里再确认没有空 key
        foreach (string k in keys) Assert.IsTrue(k != null && k.Length > 0, "词典不应有空 key");
    }

    [Test]
    public void BankNameComesFromFileName()
    {
        Assert.AreEqual("PEP三上", BankSet.BankNameFromPath(@"D:\app\banks\PEP三上.txt"));
        Assert.AreEqual("", BankSet.BankNameFromPath(""));
    }

    [Test]
    public void ChineseHeaderSuppliesDefaults()
    {
        string[] lines = new string[]
        {
            "# 人教三上单元词表",
            "#grade:3",
            "#term:上",
            "#bank:PEP三上",
            "|春天|chūn tiān|季节名",           // 无年级列 → 用块头默认值
            "4|瀑布|pù bù|水名|下|自定义"       // 行内显式列优先于块头
        };
        List<WordEntry> list = BankSet.ParseChinese(lines, "文件名题库");
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(3, list[0].Grade);
        Assert.AreEqual("春天", list[0].Word);
        Assert.AreEqual(1, list[0].Term);
        Assert.AreEqual("PEP三上", list[0].Bank);
        Assert.AreEqual(4, list[1].Grade);
        Assert.AreEqual(2, list[1].Term);
        Assert.AreEqual("自定义", list[1].Bank);
    }

    [Test]
    public void EnglishDefaultsToFileNameAsBank()
    {
        string[] lines = new string[]
        {
            "#term:下",
            "3|apple|苹果",
            "4|library|图书馆|上"
        };
        List<EnglishEntry> list = BankSet.ParseEnglish(lines, "PEP英语");
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(2, list[0].Term);
        Assert.AreEqual("PEP英语", list[0].Bank, "未写题库列时用文件名");
        Assert.AreEqual(1, list[1].Term);
    }

    [Test]
    public void LoadChineseMergesLegacyFileAndBanksDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pclock-bank-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, BankSet.DirName));
            File.WriteAllText(Path.Combine(dir, "chinese.txt"), "3|旧库|jiù kù|老格式\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, BankSet.DirName, "PEP三上.txt"),
                "#grade:3\n#bank:PEP三上\n|春天|chūn tiān|季节名\n", new UTF8Encoding(false));
            List<WordEntry> all = BankSet.LoadChinese(dir);
            Assert.AreEqual(2, all.Count, "旧单文件 + banks 目录应合并");
            Assert.AreEqual("旧库", all[0].Word);
            Assert.AreEqual("PEP三上", all[1].Bank);
            string[] names = BankSet.CollectBankNames(dir);
            Assert.AreEqual(1, names.Length);
            Assert.AreEqual("PEP三上", names[0]);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }
    }
}
