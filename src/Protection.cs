using System;
using System.Runtime.InteropServices;
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
        const uint MASK_TERMINATE = 0x00000001;
        const uint MASK_WRITE_DAC = 0x00040000;
        const uint MASK_ALL = 0x001FFFFF;

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool SetKernelObjectSecurity(IntPtr Handle, int SecurityInformation, byte[] pSecurityDescriptor);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        /// <summary>给当前进程加上"拒绝结束进程"的 DACL</summary>
        public static void ProtectSelf()
        {
            try
            {
                byte[] everyone = new byte[] { 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0 }; // S-1-1-0 (Everyone)
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
                WriteAce(sd, 28, 1, denyMask, everyone);             // 拒绝
                WriteAce(sd, 28 + aceSize, 0, allowMask, everyone);  // 允许其余权限
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
                byte[] sd = new byte[20];
                sd[0] = 1;
                sd[2] = 0x04; sd[3] = 0x80;   // SE_DACL_PRESENT 但 DACL 指针为空 = 不限制
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
