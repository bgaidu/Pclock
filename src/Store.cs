using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using PCLockConstants = PCLock.Constants;  // 避免与 System.Security.Cryptography.Constants 冲突

namespace PCLock
{
    /// <summary>
    /// 设置持久化（HKLM\SOFTWARE\PCLock，要求管理员权限）。
    /// LockFlag 持久化是实现"重启仍锁"的关键。
    /// </summary>
    public static partial class Store
    {
        static RegistryKey root;

        // 日志下沉：Store 处于核心层，不依赖入口类 Program（测试构建 tests\run.bat 不编译
        // Program.cs，直接调用 Program.Log 会因测试程序集里没有 Log 方法而编译失败）。
        // 由 Program.Main 启动时注入真实日志；未注入时静默（单元测试场景）。
        static Action<string> logSink;

        /// <summary>注入日志输出（Program.Main 启动时调用）；传 null 恢复静默</summary>
        public static void SetLogSink(Action<string> sink) { logSink = sink; }

        static void Log(string msg)
        {
            Action<string> s = logSink;
            if (s == null) return;
            try { s(msg); } catch { }
        }

        public static void Init()
        {
            if (root != null) return;

            // 必须使用 HKLM（需要管理员权限），失败直接抛异常
            // 程序清单已声明 requireAdministrator，正常情况下必定成功
            try
            {
                root = Registry.LocalMachine.CreateSubKey(PCLockConstants.RegPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "无法创建/打开注册表键 HKLM\\" + PCLockConstants.RegPath + "。请以管理员身份运行。", ex);
            }

            try
            {
                if (GetInt("FirstRunDone", 0) == 0)
                {
                    SetStr("PinHash", NewHash(PCLockConstants.DefaultPin));               // 家长PIN默认 1234
                    SetStr("UnPinHash", NewHash(PCLockConstants.DefaultUninstallPin));    // 卸载密码默认 1234
                    SetInt("DurationMinutes", PCLockConstants.DefaultDurationMinutes);
                    SetDurationMinutes(PCLockConstants.DefaultDurationMinutes); // 为当前用户写入默认时长
                    SetInt("Grade", PCLockConstants.DefaultGrade);
                    SetInt("Subject", PCLockConstants.DefaultSubject);
                    SetInt("LockFlag", 0);
                    SetInt("RemainingSeconds", PCLockConstants.DefaultDurationMinutes * 60);
                    SetLong("LastSeenUtc", 0);
                    SetInt("FirstRunDone", 1);
                    SetInt("PinSetupDone", 0);   // 新安装：首次运行强制家长设置两个密码（P0-1）
                    SetInt("AntiTamper", 0);     // 可选加固默认关闭（P1-12）
                }
            }
            catch (Exception ex)
            {
                // 首次初始化失败不算致命：用默认值继续跑
                // 具体错误由 Store.VerifyPin/GetInt 兜底处理
                Log("Store.Init 首次默认值写入失败: " + ex.Message);
            }
        }

        // 注册表读写统一异常保护：运行中键被删除/权限变化/句柄失效时不再向上抛，
        // 读失败返回默认值、写失败记日志，避免异常沿 OnTick 打到 UI 线程导致锁屏界面假死。
        static string GetStr(string name, string def)
        {
            try
            {
                if (root == null) return def;
                object v = root.GetValue(name);
                return v == null ? def : v.ToString();
            }
            catch (Exception ex)
            {
                Log("Store.GetStr(" + name + ") 失败: " + ex.Message);
                return def;
            }
        }

        static void SetStr(string name, string val)
        {
            try
            {
                if (root == null)
                {
                    Log("Store.SetStr(" + name + ") 失败: root 未初始化");
                    return;
                }
                root.SetValue(name, val);
            }
            catch (Exception ex)
            {
                Log("Store.SetStr(" + name + ") 失败: " + ex.Message);
            }
        }

        static int GetInt(string name, int def)
        {
            try
            {
                if (root == null) return def;
                object v = root.GetValue(name);
                return v is int ? (int)v : def;
            }
            catch (Exception ex)
            {
                Log("Store.GetInt(" + name + ") 失败: " + ex.Message);
                return def;
            }
        }

        static void SetInt(string name, int val)
        {
            try
            {
                if (root == null)
                {
                    Log("Store.SetInt(" + name + ") 失败: root 未初始化");
                    return;
                }
                root.SetValue(name, val, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Log("Store.SetInt(" + name + ") 失败: " + ex.Message);
            }
        }

        static long GetLong(string name, long def)
        {
            try
            {
                if (root == null) return def;
                object v = root.GetValue(name);
                return v is long ? (long)v : def;
            }
            catch (Exception ex)
            {
                Log("Store.GetLong(" + name + ") 失败: " + ex.Message);
                return def;
            }
        }

        static void SetLong(string name, long val)
        {
            try
            {
                if (root == null)
                {
                    Log("Store.SetLong(" + name + ") 失败: root 未初始化");
                    return;
                }
                root.SetValue(name, val, RegistryValueKind.QWord);
            }
            catch (Exception ex)
            {
                Log("Store.SetLong(" + name + ") 失败: " + ex.Message);
            }
        }

        public static int GetDurationMinutes()
        {
            return GetDurationMinutes(GetCurrentUser());
        }

        public static void SetDurationMinutes(int m) { SetDurationMinutes(GetCurrentUser(), m); }

        public static int GetGrade() { return GetGrade(GetCurrentUser()); }
        public static void SetGrade(int g) { SetGrade(GetCurrentUser(), g); }

        // 年级按用户：优先 U:<user>:Grade，回退全局 Grade，再回退默认
        public static int GetGrade(string user)
        {
            int g = GetInt(UKey(user, "Grade"), -1);
            if (g < 0) g = GetInt("Grade", PCLockConstants.DefaultGrade);
            foreach (int x in PCLockConstants.AllowedGrades)
                if (x == g) return g;
            return PCLockConstants.DefaultGrade;
        }
        public static void SetGrade(string user, int g) { SetInt(UKey(user, "Grade"), g); }

        public static int GetSubject() { return GetSubject(GetCurrentUser()); }
        public static void SetSubject(int s) { SetSubject(GetCurrentUser(), s); }

        // 学科按用户：优先 U:<user>:Subject，回退全局 Subject，再回退默认
        public static int GetSubject(string user)
        {
            int s = GetInt(UKey(user, "Subject"), -1);
            if (s < 0) s = GetInt("Subject", PCLockConstants.DefaultSubject);
            if (s < 0 || s > 6) return PCLockConstants.DefaultSubject;
            return s;
        }
        public static void SetSubject(string user, int s) { SetInt(UKey(user, "Subject"), s); }

        public static int GetLockFlag() { return GetInt("LockFlag", 0); }
        public static void SetLockFlag(int v) { SetInt("LockFlag", v); }

        public static int GetRemaining() { return GetInt("RemainingSeconds", -1); }
        public static void SetRemaining(int sec) { SetInt("RemainingSeconds", sec); }

        /// <summary>今日已用秒数（按本地日期区分；跨天自动视为 0）</summary>
        public static int GetUsedToday()
        {
            return GetUsedToday(GetCurrentUser());
        }

        /// <summary>累加今日已用秒数；跨天自动重置再累加</summary>
        public static void AddUsedSeconds(int sec)
        {
            AddUsedSeconds(GetCurrentUser(), sec);
        }

        /// <summary>
        /// 上次写剩余时间时的 UTC 时间戳（.NET Ticks，0 = 无有效时间戳）。
        /// 用于硬断电后折算扣除离线时长，防止断电"冻结"倒计时。
        /// </summary>
        public static long GetLastSeenUtc() { return GetLong("LastSeenUtc", 0); }
        public static void SetLastSeenUtc(long ticks) { SetLong("LastSeenUtc", ticks); }

        // === 按用户分别限制 / 统计 ===
        /// <summary>当前登录的 Windows 用户名（作为按用户隔离的维度）</summary>
        public static string GetCurrentUser()
        {
            try { return Environment.UserName ?? "default"; }
            catch { return "default"; }
        }

        /// <summary>按用户键名前缀：U:&lt;user&gt;:&lt;field&gt;</summary>
        static string UKey(string user, string field) { return "U:" + (user ?? "default") + ":" + field; }

        /// <summary>列出所有已配置（或当前）的 Windows 用户，供设置界面切换</summary>
        public static string[] ListUsers()
        {
            List<string> list = new List<string>();
            try
            {
                foreach (string name in root.GetValueNames())
                {
                    if (name.StartsWith("U:"))
                    {
                        int i1 = name.IndexOf(':');
                        int i2 = name.IndexOf(':', i1 + 1);
                        if (i2 > i1)
                        {
                            string u = name.Substring(i1 + 1, i2 - i1 - 1);
                            if (u.Length > 0 && !list.Contains(u)) list.Add(u);
                        }
                    }
                }
            }
            catch (Exception) { }
            list.Sort();
            string cur = GetCurrentUser();
            if (!list.Contains(cur)) list.Add(cur);
            return list.ToArray();
        }

        public static int GetDurationMinutes(string user)
        {
            int d = GetInt(UKey(user, "Dur"), -1);
            if (d < 0) d = GetInt("DurationMinutes", PCLockConstants.DefaultDurationMinutes);
            int[] ok = PCLockConstants.AllowedDurations;
            for (int i = 0; i < ok.Length; i++)
                if (ok[i] == d) return d;
            return PCLockConstants.DefaultDurationMinutes;
        }
        public static void SetDurationMinutes(string user, int m) { SetInt(UKey(user, "Dur"), m); }

        // === 年级多选 / 科目多选（按用户）===
        // 存为逗号分隔字符串：Grades="1,3,5"；Subjects="1,2"（1=语文 2=数学 3=英语）；空=默认
        public static int[] GetGrades() { return GetGrades(GetCurrentUser()); }
        public static int[] GetGrades(string user)
        {
            List<int> r = new List<int>();
            string s = GetStr(UKey(user, "Grades"), "");
            if (!string.IsNullOrEmpty(s))
                foreach (string t in s.Split(','))
                {
                    int g;
                    if (int.TryParse(t.Trim(), out g))
                        foreach (int x in PCLockConstants.AllowedGrades)
                            if (x == g && !r.Contains(g)) r.Add(g);
                }
            if (r.Count == 0) foreach (int x in PCLockConstants.AllowedGrades) r.Add(x); // 默认全选
            r.Sort();
            return r.ToArray();
        }
        public static void SetGrades(int[] gs) { SetGrades(GetCurrentUser(), gs); }
        public static void SetGrades(string user, int[] gs)
        {
            List<int> r = new List<int>();
            if (gs != null) foreach (int g in gs)
                foreach (int x in PCLockConstants.AllowedGrades)
                    if (x == g && !r.Contains(g)) r.Add(x);
            if (r.Count == 0) return;
            r.Sort();
            StringBuilder sb = new StringBuilder();
            foreach (int g in r) { if (sb.Length > 0) sb.Append(','); sb.Append(g); }
            SetStr(UKey(user, "Grades"), sb.ToString());
        }

        public static int[] GetSubjects() { return GetSubjects(GetCurrentUser()); }
        public static int[] GetSubjects(string user)
        {
            List<int> r = new List<int>();
            string s = GetStr(UKey(user, "Subjects"), "");
            if (!string.IsNullOrEmpty(s))
                foreach (string t in s.Split(','))
                {
                    int v;
                    if (int.TryParse(t.Trim(), out v) && v >= 1 && v <= 3 && !r.Contains(v)) r.Add(v);
                }
            if (r.Count == 0) { r.Add(1); r.Add(2); r.Add(3); } // 默认三科
            r.Sort();
            return r.ToArray();
        }
        public static void SetSubjects(int[] ss) { SetSubjects(GetCurrentUser(), ss); }
        public static void SetSubjects(string user, int[] ss)
        {
            List<int> r = new List<int>();
            if (ss != null) foreach (int v in ss) if (v >= 1 && v <= 3 && !r.Contains(v)) r.Add(v);
            if (r.Count == 0) return;
            r.Sort();
            StringBuilder sb = new StringBuilder();
            foreach (int v in r) { if (sb.Length > 0) sb.Append(','); sb.Append(v); }
            SetStr(UKey(user, "Subjects"), sb.ToString());
        }

        /// <summary>每日可用时段字符串，如 "08:00-12:00;14:00-21:00"；空=全天可用</summary>
        public static string GetWindow(string user) { return GetStr(UKey(user, "Win"), ""); }
        public static void SetWindow(string user, string w) { SetStr(UKey(user, "Win"), w == null ? "" : w); }

        /// <summary>当前是否处于该用户的允许使用时段内（未配置=全天可用）</summary>
        public static bool IsWithinWindow() { return IsWithinWindow(GetCurrentUser()); }
        public static bool IsWithinWindow(string user)
        {
            return IsWithinWindow(GetWindow(user), DateTime.Now.TimeOfDay);
        }

        /// <summary>纯函数版（不读注册表，便于单元测试）：判断 now 是否落在 windowSpec 任意段内</summary>
        public static bool IsWithinWindow(string windowSpec, TimeSpan now)
        {
            if (string.IsNullOrWhiteSpace(windowSpec)) return true;
            foreach (string raw in windowSpec.Split(new char[] { ';', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string seg = raw.Trim();
                if (seg.Length == 0) continue;
                int dash = seg.IndexOf('-');
                if (dash < 0) continue;
                TimeSpan? a = ParseHm(seg.Substring(0, dash));
                TimeSpan? b = ParseHm(seg.Substring(dash + 1));
                if (a.HasValue && b.HasValue)
                {
                    if (a.Value <= b.Value)
                    {
                        // 普通时段（08:00-12:00）：闭区间
                        if (a.Value <= now && now <= b.Value) return true;
                    }
                    else
                    {
                        // 跨午夜时段（如 22:00-06:00）：起始大于结束时按环绕判断，
                        // 覆盖 22:00-24:00 与 00:00-06:00 两段。原实现 a<=now<=b 恒为 false，
                        // 家长填写跨午夜时段会导致全天硬锁、孩子完全无法使用电脑。
                        if (now >= a.Value || now <= b.Value) return true;
                    }
                }
            }
            return false;
        }
        static TimeSpan? ParseHm(string s)
        {
            s = s.Trim();
            int colon = s.IndexOf(':');
            if (colon < 0) return null;
            int h, m;
            if (!int.TryParse(s.Substring(0, colon), out h)) return null;
            if (!int.TryParse(s.Substring(colon + 1), out m)) return null;
            if (h < 0 || h > 23 || m < 0 || m > 59) return null;
            return new TimeSpan(h, m, 0);
        }

        // === 硬断电离线时长折算（纯函数，不读注册表，便于单元测试）===

        /// <summary>离线折算结果：调用方据此更新状态与写日志</summary>
        public struct OfflineDeductResult
        {
            /// <summary>折算后的剩余秒数（未折算时为入参原值）</summary>
            public int Remaining;
            /// <summary>本次是否发生了折算（false = 正常退出 / 时钟回拨 / 离线不足阈值）</summary>
            public bool Deducted;
            /// <summary>实际扣除秒数（向上取整；未折算为 0）</summary>
            public int DeductedSeconds;
            /// <summary>距上次时间戳的墙钟秒数（时间戳无效或时钟回拨时为 0）</summary>
            public double ElapsedSeconds;
        }

        /// <summary>
        /// 纯函数：硬断电离线时长折算。规则（与 App.DeductOffline 原逻辑一致）：
        /// - lastSeenUtcTicks &lt;= 0：上次正常退出（Shutdown 会清时间戳），不折算；
        /// - 时钟回拨（nowUtcTicks &lt; lastSeenUtcTicks）：不折算，防止改系统时间加时；
        /// - 离线不足 Constants.OfflineDeductThresholdSec：视为正常关机延迟，不折算；
        /// - 否则按离线秒数向上取整扣减，剩余不足扣到 0。
        ///   向上取整是故意的：零点几秒也不给"零成本重启"的可乘之机。
        /// </summary>
        public static OfflineDeductResult DeductOffline(int currentRemaining, long lastSeenUtcTicks, long nowUtcTicks)
        {
            OfflineDeductResult r = new OfflineDeductResult();
            r.Remaining = currentRemaining;
            if (lastSeenUtcTicks <= 0) return r;   // 正常退出
            double elapsedSec = (nowUtcTicks - lastSeenUtcTicks) / (double)TimeSpan.TicksPerSecond;
            if (elapsedSec <= 0) return r;         // 时钟回拨（ElapsedSeconds 保持 0）
            if (elapsedSec < PCLockConstants.OfflineDeductThresholdSec)
            {
                r.ElapsedSeconds = elapsedSec;     // 记录真实离线时长，供调用方写日志
                return r;                          // 视为正常关机，不扣
            }
            long elapsedCeil = (long)Math.Ceiling(elapsedSec);
            long deduct = elapsedCeil > int.MaxValue ? int.MaxValue : elapsedCeil;
            long left = (long)currentRemaining - deduct;
            r.Remaining = left < 0 ? 0 : (int)left;
            r.Deducted = true;
            r.DeductedSeconds = (int)deduct;
            r.ElapsedSeconds = elapsedSec;
            return r;
        }

        public static int GetUsedToday(string user)
        {
            if (GetStr(UKey(user, "UsedDate"), "") != DateTime.Now.ToString("yyyy-MM-dd")) return 0;
            return GetInt(UKey(user, "UsedSec"), 0);
        }
        public static void AddUsedSeconds(string user, int sec)
        {
            if (sec <= 0) return;
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string dateKey = UKey(user, "UsedDate");
            if (GetStr(dateKey, "") != today)
            {
                SetStr(dateKey, today);
                SetInt(UKey(user, "UsedSec"), 0);
            }
            SetInt(UKey(user, "UsedSec"), GetInt(UKey(user, "UsedSec"), 0) + sec);
        }

        // === PIN 哈希 ===
        // HKLM\SOFTWARE\PCLock 普通用户可读，4 位纯数字 PIN 若用快速哈希可被离线枚举，
        // 因此新格式用 PBKDF2（迭代次数见 Constants.PinHashIterations）：
        //   新格式："pbkdf2$<迭代次数>$<salt>$<hash>"
        //   旧格式：64 位十六进制 = SHA256(salt|pin)，验证成功后自动升级为新格式
        const string HashPrefix = "pbkdf2";
        const int HashMaxIterations = 10000000; // 解析已存哈希时的迭代次数上限（防篡改成超大值拖死验证）

        static string BytesToHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++) sb.Append(data[i].ToString("x2"));
            return sb.ToString();
        }

        internal static string LegacyHashHex(string pin, string salt)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BytesToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(salt + "|" + pin)));
            }
        }

        static string Pbkdf2HashHex(string pin, string salt, int iterations)
        {
            // .NET 3.5 的 DeriveBytes 未实现 IDisposable，不能用 using（v3.5 csc 报 CS1674）；
            // 无非托管资源，交给 GC 回收即可，v3.5/v4 编译器均兼容
            Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(pin, Encoding.UTF8.GetBytes(salt), iterations);
            return BytesToHex(kdf.GetBytes(32));
        }

        internal static string NewHash(string pin)
        {
            string salt = Guid.NewGuid().ToString("N");
            return HashPrefix + "$" + PCLockConstants.PinHashIterations + "$" + salt + "$" +
                Pbkdf2HashHex(pin, salt, PCLockConstants.PinHashIterations);
        }

        /// <summary>纯函数：pin 是否匹配已存的哈希（兼容新旧两种格式）</summary>
        internal static bool MatchesStoredHash(string stored, string pin, string legacySalt)
        {
            if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(pin)) return false;
            if (stored.StartsWith(HashPrefix + "$", StringComparison.Ordinal))
            {
                string[] parts = stored.Split('$');
                if (parts.Length != 4) return false;
                int iters;
                if (!int.TryParse(parts[1], out iters) || iters < 1 || iters > HashMaxIterations) return false;
                if (parts[2].Length == 0 || parts[3].Length == 0) return false;
                return parts[3] == Pbkdf2HashHex(pin, parts[2], iters);
            }
            // 旧格式
            if (string.IsNullOrEmpty(legacySalt)) return false;
            return LegacyHashHex(pin, legacySalt) == stored;
        }

        static bool VerifyHash(string hashName, string legacySaltName, string pin)
        {
            if (string.IsNullOrEmpty(pin)) return false;
            string stored = GetStr(hashName, "");
            if (!MatchesStoredHash(stored, pin, GetStr(legacySaltName, null))) return false;
            if (!stored.StartsWith(HashPrefix + "$", StringComparison.Ordinal))
            {
                try { SetStr(hashName, NewHash(pin)); } catch (Exception) { } // 旧格式自动升级
            }
            return true;
        }

        public static bool VerifyPin(string pin)
        {
            return VerifyHash("PinHash", "Salt", pin);
        }

        public static bool VerifyUninstallPin(string pin)
        {
            return VerifyHash("UnPinHash", "USalt", pin);
        }

        public static void SetPin(string p) { SetStr("PinHash", NewHash(p)); }
        public static void SetUninstallPin(string p) { SetStr("UnPinHash", NewHash(p)); }

        /// <summary>卸载时清除全部痕迹</summary>
        public static void RemoveAll()
        {
            try { Registry.LocalMachine.DeleteSubKeyTree(PCLockConstants.RegPath); } catch (Exception) { }
            try { Registry.CurrentUser.DeleteSubKeyTree(PCLockConstants.RegPath); } catch (Exception) { }
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (k != null) k.DeleteValue(PCLockConstants.TaskName, false);
                }
            }
            catch (Exception) { }
        }
    }
}
