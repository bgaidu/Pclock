using System;
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
    public static class Store
    {
        static RegistryKey root;

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
                    SetInt("LockFlag", 0);
                    SetInt("RemainingSeconds", PCLockConstants.DefaultDurationMinutes * 60);
                    SetLong("LastSeenUtc", 0);
                    SetInt("FirstRunDone", 1);
                }
            }
            catch (Exception)
            {
                // 首次初始化失败不算致命：用默认值继续跑
                // 具体错误由 Store.VerifyPin/GetInt 兜底处理
            }
        }

        static string GetStr(string name, string def)
        {
            object v = root.GetValue(name);
            return v == null ? def : v.ToString();
        }

        static void SetStr(string name, string val)
        {
            root.SetValue(name, val);
        }

        static int GetInt(string name, int def)
        {
            object v = root.GetValue(name);
            return v is int ? (int)v : def;
        }

        static void SetInt(string name, int val)
        {
            root.SetValue(name, val, RegistryValueKind.DWord);
        }

        static long GetLong(string name, long def)
        {
            object v = root.GetValue(name);
            return v is long ? (long)v : def;
        }

        static void SetLong(string name, long val)
        {
            root.SetValue(name, val, RegistryValueKind.QWord);
        }

        public static int GetDurationMinutes()
        {
            int d = GetInt("DurationMinutes", PCLockConstants.DefaultDurationMinutes);
            int[] ok = PCLockConstants.AllowedDurations;
            for (int i = 0; i < ok.Length; i++)
                if (ok[i] == d) return d;
            return PCLockConstants.DefaultDurationMinutes;
        }

        public static void SetDurationMinutes(int m) { SetInt("DurationMinutes", m); }

        public static int GetLockFlag() { return GetInt("LockFlag", 0); }
        public static void SetLockFlag(int v) { SetInt("LockFlag", v); }

        public static int GetRemaining() { return GetInt("RemainingSeconds", -1); }
        public static void SetRemaining(int sec) { SetInt("RemainingSeconds", sec); }

        /// <summary>
        /// 上次写剩余时间时的 UTC 时间戳（.NET Ticks，0 = 无有效时间戳）。
        /// 用于硬断电后折算扣除离线时长，防止断电"冻结"倒计时。
        /// </summary>
        public static long GetLastSeenUtc() { return GetLong("LastSeenUtc", 0); }
        public static void SetLastSeenUtc(long ticks) { SetLong("LastSeenUtc", ticks); }

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
