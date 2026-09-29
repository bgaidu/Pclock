using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace PCLock
{
    /// <summary>
    /// 设置持久化（HKLM\SOFTWARE\PCLock，无权限时退回 HKCU）。
    /// LockFlag 持久化是实现"重启仍锁"的关键。
    /// </summary>
    public static class Store
    {
        static RegistryKey root;

        public static void Init()
        {
            if (root != null) return;

            // 尝试 HKLM（管理员），失败退回 HKCU
            if (root == null)
            {
                try { root = Registry.LocalMachine.CreateSubKey(App.RegPath); }
                catch { root = null; }
            }
            if (root == null)
            {
                try { root = Registry.CurrentUser.CreateSubKey(App.RegPath); }
                catch { root = null; }
            }
            if (root == null)
                throw new InvalidOperationException(
                    "Cannot open registry key under " + App.RegPath);

            try
            {
                if (GetInt("FirstRunDone", 0) == 0)
                {
                    SetStr("PinHash", Hash("1234", "Salt"));          // 家长PIN默认 1234
                    SetStr("UnPinHash", Hash("1234", "USalt"));       // 卸载密码默认 1234
                    SetInt("DurationMinutes", 60);
                    SetInt("LockFlag", 0);
                    SetInt("RemainingSeconds", 60 * 60);
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
            int d = GetInt("DurationMinutes", 60);
            int[] ok = new int[] { 15, 30, 45, 60, 90, 120 };
            for (int i = 0; i < ok.Length; i++)
                if (ok[i] == d) return d;
            return 60;
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

        static string Hash(string pin, string saltName)
        {
            string salt = GetStr(saltName, null);
            if (salt == null)
            {
                salt = Guid.NewGuid().ToString("N");
                SetStr(saltName, salt);
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] data = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + "|" + pin));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < data.Length; i++) sb.Append(data[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool VerifyPin(string pin)
        {
            return pin != null && pin.Length > 0 && GetStr("PinHash", "") == Hash(pin, "Salt");
        }

        public static bool VerifyUninstallPin(string pin)
        {
            return pin != null && pin.Length > 0 && GetStr("UnPinHash", "") == Hash(pin, "USalt");
        }

        public static void SetPin(string p) { SetStr("PinHash", Hash(p, "Salt")); }
        public static void SetUninstallPin(string p) { SetStr("UnPinHash", Hash(p, "USalt")); }

        /// <summary>卸载时清除全部痕迹</summary>
        public static void RemoveAll()
        {
            try { Registry.LocalMachine.DeleteSubKeyTree(App.RegPath); } catch (Exception) { }
            try { Registry.CurrentUser.DeleteSubKeyTree(App.RegPath); } catch (Exception) { }
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (k != null) k.DeleteValue("PCLock", false);
                }
            }
            catch (Exception) { }
        }
    }
}
