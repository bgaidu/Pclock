using System;
using System.Security.Cryptography;
using System.Text;
using PCLock;
using TUnit;

/// <summary>
/// PIN 哈希纯函数测试（Store.NewHash / MatchesStoredHash / LegacyHashHex）。
/// 对应 README"2026-10-05 PIN 哈希抗离线枚举"条目声称的 16 项哈希测试。
/// 全部走 internal 纯函数（同一程序集可见），不涉及注册表。
/// </summary>
public class HashTests
{
    const string LegacySalt = "abcdef0123456789abcdef0123456789";

    static string BytesToHex(byte[] data)
    {
        StringBuilder sb = new StringBuilder(data.Length * 2);
        for (int i = 0; i < data.Length; i++) sb.Append(data[i].ToString("x2"));
        return sb.ToString();
    }

    static bool IsHex(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        return true;
    }

    // 1. 新格式往返：NewHash 生成的哈希必须能验证同一 PIN
    [Test]
    public void NewFormatRoundtrip()
    {
        string stored = Store.NewHash("1234");
        Assert.IsTrue(Store.MatchesStoredHash(stored, "1234", null), "新格式应验证成功");
    }

    // 2. 新格式往返（多个 PIN，含最长 20 位）：长度边界都不能破坏往返
    [Test]
    public void NewFormatRoundtripManyPins()
    {
        string[] pins = new string[] { "0000", "1234", "9999", "000000", "12345678901234567890" };
        foreach (string p in pins)
            Assert.IsTrue(Store.MatchesStoredHash(Store.NewHash(p), p, null), "PIN [" + p + "] 往返失败");
    }

    // 3. 新格式结构：pbkdf2$迭代$盐$哈希，盐 32 位十六进制（Guid N），哈希 64 位十六进制（32 字节）
    [Test]
    public void NewFormatStructure()
    {
        string[] parts = Store.NewHash("1234").Split('$');
        Assert.AreEqual(4, parts.Length, "应含 4 段");
        Assert.AreEqual("pbkdf2", parts[0], "前缀");
        Assert.AreEqual(Constants.PinHashIterations, int.Parse(parts[1]), "迭代次数应取自 Constants");
        Assert.AreEqual(32, parts[2].Length, "盐应为 32 位十六进制");
        Assert.IsTrue(IsHex(parts[2]), "盐应为十六进制");
        Assert.AreEqual(64, parts[3].Length, "哈希应为 64 位十六进制（32 字节）");
        Assert.IsTrue(IsHex(parts[3]), "哈希应为十六进制");
    }

    // 4. 盐随机：同一 PIN 两次哈希结果不同，但各自都能验证
    [Test]
    public void SaltUniquePerHash()
    {
        string h1 = Store.NewHash("1234");
        string h2 = Store.NewHash("1234");
        Assert.IsTrue(h1 != h2, "同一 PIN 两次哈希应不同（盐随机）");
        Assert.IsTrue(Store.MatchesStoredHash(h1, "1234", null), "h1 应验证成功");
        Assert.IsTrue(Store.MatchesStoredHash(h2, "1234", null), "h2 应验证成功");
    }

    // 5. 错误 PIN 拒绝：错值、短一位、长一位、尾随空格
    [Test]
    public void WrongPinRejected()
    {
        string stored = Store.NewHash("1234");
        Assert.IsFalse(Store.MatchesStoredHash(stored, "1111", null), "错误 PIN 应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash(stored, "123", null), "短一位应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash(stored, "12345", null), "长一位应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash(stored, "1234 ", null), "尾随空格应拒绝");
    }

    // 6. 空 PIN 拒绝
    [Test]
    public void EmptyPinRejected()
    {
        string stored = Store.NewHash("1234");
        Assert.IsFalse(Store.MatchesStoredHash(stored, "", null), "空 PIN 应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash(stored, null, null), "null PIN 应拒绝");
    }

    // 7. 空 stored 拒绝
    [Test]
    public void EmptyStoredRejected()
    {
        Assert.IsFalse(Store.MatchesStoredHash("", "1234", null), "空 stored 应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash(null, "1234", null), "null stored 应拒绝");
    }

    // 8. 畸形字符串拒绝：段数不足、段数过多、完全不像哈希
    [Test]
    public void MalformedPartsRejected()
    {
        string[] p = Store.NewHash("1234").Split('$');
        Assert.IsFalse(Store.MatchesStoredHash("pbkdf2$" + p[1] + "$" + p[2], "1234", null), "缺哈希段应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash("pbkdf2$" + p[1] + "$" + p[2] + "$" + p[3] + "$x", "1234", null), "多一段应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash("garbage", "1234", null), "非 pbkdf2 且无旧盐应拒绝");
    }

    // 9. 畸形迭代次数拒绝：非数字、为空
    [Test]
    public void MalformedIterationsRejected()
    {
        string[] p = Store.NewHash("1234").Split('$');
        Assert.IsFalse(Store.MatchesStoredHash("pbkdf2$abc$" + p[2] + "$" + p[3], "1234", null), "迭代次数非数字应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash("pbkdf2$$" + p[2] + "$" + p[3], "1234", null), "迭代次数为空应拒绝");
    }

    // 10. 迭代次数越界拒绝：0、负数、超上限（防篡改成超大值拖死验证）
    [Test]
    public void IterationsOutOfRangeRejected()
    {
        string[] p = Store.NewHash("1234").Split('$');
        Assert.IsFalse(Store.MatchesStoredHash("pbkdf2$0$" + p[2] + "$" + p[3], "1234", null), "0 次迭代应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash("pbkdf2$-1$" + p[2] + "$" + p[3], "1234", null), "负迭代应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash("pbkdf2$10000001$" + p[2] + "$" + p[3], "1234", null), "超上限迭代应拒绝");
    }

    // 11. 空盐 / 空哈希段拒绝
    [Test]
    public void EmptySaltOrHashRejected()
    {
        string[] p = Store.NewHash("1234").Split('$');
        Assert.IsFalse(Store.MatchesStoredHash("pbkdf2$" + p[1] + "$$" + p[3], "1234", null), "空盐应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash("pbkdf2$" + p[1] + "$" + p[2] + "$", "1234", null), "空哈希应拒绝");
    }

    // 12. 旧格式兼容：与独立 SHA256(salt|pin) 实现比对一致，且能验证成功
    //     （这是旧格式兼容性的锚点：Store 与独立实现必须字节级一致）
    [Test]
    public void LegacyFormatMatchesIndependentSha256()
    {
        string pin = "1234";
        string expect;
        using (SHA256 sha = SHA256.Create())
        {
            expect = BytesToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(LegacySalt + "|" + pin)));
        }
        Assert.AreEqual(expect, Store.LegacyHashHex(pin, LegacySalt), "旧格式应与独立 SHA256 实现一致");
        Assert.IsTrue(Store.MatchesStoredHash(Store.LegacyHashHex(pin, LegacySalt), pin, LegacySalt),
            "旧格式哈希应验证成功");
    }

    // 13. 旧格式错误 PIN / 缺盐拒绝
    [Test]
    public void LegacyWrongPinOrMissingSaltRejected()
    {
        string legacy = Store.LegacyHashHex("1234", LegacySalt);
        Assert.IsFalse(Store.MatchesStoredHash(legacy, "1111", LegacySalt), "旧格式错误 PIN 应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash(legacy, "1234", ""), "旧格式缺盐应拒绝");
        Assert.IsFalse(Store.MatchesStoredHash(legacy, "1234", null), "旧格式 null 盐应拒绝");
    }

    // 14. 新格式路径不参考 legacySalt 参数（传垃圾值也不影响验证）
    [Test]
    public void NewFormatIgnoresLegacySaltParam()
    {
        string stored = Store.NewHash("1234");
        Assert.IsTrue(Store.MatchesStoredHash(stored, "1234", "whatever"), "新格式验证不应受 legacySalt 影响");
    }

    // 15. 篡改哈希末位必须拒绝（确认是全量比对，不是前缀/长度比对）
    [Test]
    public void TamperedHashRejected()
    {
        string stored = Store.NewHash("1234");
        char[] c = stored.ToCharArray();
        c[c.Length - 1] = (c[c.Length - 1] == '0') ? '1' : '0';
        Assert.IsFalse(Store.MatchesStoredHash(new string(c), "1234", null), "篡改哈希末位应拒绝");
    }

    // 16. 成本健全性：25000 次迭代单次验证应有可感知耗时（下界断言，
    //     防止有人把迭代次数改成 1 使 4 位 PIN 可被瞬间离线枚举；不设上界，慢机器不误报）
    [Test]
    public void Pbkdf2CostSanity()
    {
        string stored = Store.NewHash("1234");
        DateTime t0 = DateTime.UtcNow;
        bool ok = Store.MatchesStoredHash(stored, "1234", null);
        double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
        Assert.IsTrue(ok, "验证应成功");
        Assert.GreaterOrEqual(ms, 5.0, "单次验证耗时应 >= 5ms（迭代次数可能被削弱）");
        Console.WriteLine("    [info] PBKDF2(" + Constants.PinHashIterations + ") 单次验证耗时 "
            + ms.ToString("F1") + " ms");
    }
}
