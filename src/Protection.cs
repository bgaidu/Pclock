using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace PCLock
{
    /// <summary>
    /// 进程自保护 + 锁屏期间的系统策略。
    /// 1) DACL 拒绝所有人 TERMINATE/WRITE_DAC：任务管理器/taskkill 结束进程时提示"拒绝访问"；
    ///    退出/卸载时恢复为空 DACL 以便正常退出。
    /// 2) 锁屏期间写 DisableTaskMgr 策略，锁屏界面下无法打开任务管理器。
    /// </summary>
    public static class Protection
    {
        const int DACL_SECURITY_INFORMATION = 0x00000004;
        // Win7 进程访问权限
        const uint MASK_TERMINATE = 0x00000001;
        const uint MASK_WRITE_DAC = 0x00040000;
        // Win10/11 新增权限位（在 Win7 上设置无效但不会报错）
        const uint PROCESS_SET_LIMITED_INFORMATION = 0x00002000;
        const uint PROCESS_SET_QUOTA = 0x00000100;
        // 完整的进程访问权限掩码（覆盖 Win7/10/11）
        const uint MASK_ALL = 0x001FFFFF | PROCESS_SET_LIMITED_INFORMATION | PROCESS_SET_QUOTA;

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool SetKernelObjectSecurity(IntPtr Handle, int SecurityInformation, byte[] pSecurityDescriptor);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        /// <summary>给当前进程加上"拒绝结束进程"的 DACL</summary>
        public static void ProtectSelf()
        {
            try
            {
                // 使用 WellKnownSidType.WorldSid (S-1-1-0 Everyone) 动态获取 SID，避免硬编码
                var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                byte[] everyone = new byte[everyoneSid.BinaryLength];
                everyoneSid.GetBinaryForm(everyone, 0);

                uint denyMask = MASK_TERMINATE | MASK_WRITE_DAC;
                uint allowMask = MASK_ALL & ~denyMask;
                int aceSize = 8 + everyone.Length;
                int aclSize = 8 + aceSize * 2;
                byte[] sd = new byte[20 + aclSize];
                sd[0] = 1;                                   // SECURITY_DESCRIPTOR_REVISION
                sd[2] = 0x04; sd[3] = 0x80;                  // SE_DACL_PRESENT | SE_SELF_RELATIVE
                sd[16] = (byte)20;                           // Dacl 偏移 = 20
                sd[20] = 2;                                  // ACL_REVISION
                sd[22] = (byte)(aclSize & 0xFF);
                sd[23] = (byte)((aclSize >> 8) & 0xFF);
                sd[24] = 2;                                  // 2 个 ACE
                WriteAce(sd, 28, 1, denyMask, everyone);             // 拒绝 ACE (type=1 ACCESS_DENIED_ACE_TYPE)
                WriteAce(sd, 28 + aceSize, 0, allowMask, everyone);  // 允许 ACE (type=0 ACCESS_ALLOWED_ACE_TYPE)
                SetKernelObjectSecurity(GetCurrentProcess(), DACL_SECURITY_INFORMATION, sd);
            }
            catch (Exception) { }
        }

        static void WriteAce(byte[] buf, int off, byte type, uint mask, byte[] sid)
        {
            int size = 8 + sid.Length;
            buf[off] = type;
            buf[off + 1] = 0;
            buf[off + 2] = (byte)(size & 0xFF);
            buf[off + 3] = (byte)((size >> 8) & 0xFF);
            buf[off + 4] = (byte)(mask & 0xFF);
            buf[off + 5] = (byte)((mask >> 8) & 0xFF);
            buf[off + 6] = (byte)((mask >> 16) & 0xFF);
            buf[off + 7] = (byte)((mask >> 24) & 0xFF);
            Array.Copy(sid, 0, buf, off + 8, sid.Length);
        }

        /// <summary>恢复正常 DACL（允许所有人），用于正常退出/卸载</summary>
        public static void UnprotectSelf()
        {
            try
            {
                // 创建含空 ACL 的 SD：20 字节 header + 8 字节空 ACL（无 ACE）
                // Dacl 偏移 = 20，ACL_REVISION = 2，ACE 数 = 0
                byte[] sd = new byte[28];
                sd[0] = 1;                                   // SECURITY_DESCRIPTOR_REVISION
                sd[2] = 0x04; sd[3] = 0x80;                  // SE_DACL_PRESENT | SE_SELF_RELATIVE
                sd[16] = 20;                                 // Dacl 偏移 = 20
                sd[20] = 2;                                  // ACL_REVISION
                // aclSize = 8 (无 ACE)，低字节 sd[22]=8, 高字节 sd[23]=0
                sd[22] = 8;
                sd[23] = 0;
                sd[24] = 0;                                  // 0 个 ACE
                sd[25] = 0;
                SetKernelObjectSecurity(GetCurrentProcess(), DACL_SECURITY_INFORMATION, sd);
            }
            catch (Exception) { }
        }

        /// <summary>禁用/恢复任务管理器（写 DisableTaskMgr 策略，HKCU + HKLM 双保险）</summary>
        public static void SetTaskMgrDisabled(bool on)
        {
            ApplyPolicy(Registry.CurrentUser, on);
            ApplyPolicy(Registry.LocalMachine, on);
        }

        static void ApplyPolicy(RegistryKey hive, bool on)
        {
            try
            {
                using (RegistryKey k = hive.CreateSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System"))
                {
                    if (on)
                    {
                        k.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                    }
                    else
                    {
                        try { k.DeleteValue("DisableTaskMgr", false); }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
