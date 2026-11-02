using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>App 的拆分文件：退出与卸载（同 partial class，行为不变）</summary>
    public partial class App : ApplicationContext
    {
        /// <summary>退出 / 卸载</summary>
        public void Shutdown(bool uninstall)
        {
            stopping = true;
            if (timer != null) timer.Stop();
            AuditLog.Write((uninstall ? "程序卸载" : "程序退出") + "：用户=" + currentUser +
                (lockForm != null ? "（退出时处于锁屏）" : ""));
            try
            {
                FlushUsed();                           // 退出前结掉统计（卸载时数据随之删除）
                if (lockForm != null)
                {
                    lockForm.AllowClose = true;
                    lockForm.Dispose();
                    lockForm = null;
                }
                Store.SetLockFlag(0);
                Protection.SetTaskMgrDisabled(false);
                if (!uninstall)
                {
                    // 与退出确认框语义一致："下次开机自动重新开始"——
                    // 重置为完整时长，并清掉断电折算时间戳，下次开机不扣离线时长
                    remaining = Store.GetDurationMinutes(currentUser) * 60;
                    Store.SetRemaining(remaining);
                    Store.SetLastSeenUtc(0);
                }
                // 通知看门狗自行退出（使用构造时创建/打开的事件对象）
                if (stopEvent != null)
                {
                    try { stopEvent.Set(); }
                    catch { }
                }
            }
            catch (Exception) { }

            if (uninstall)
            {
                try { Store.RemoveAll(); } catch (Exception ex) { Program.Log("卸载: 清除注册表失败: " + ex.Message); }
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("schtasks",
                        "/Delete /F /TN \"" + Constants.TaskName + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    using (Process p = Process.Start(psi)) { p.WaitForExit(5000); }
                }
                catch (Exception ex) { Program.Log("卸载: 删除计划任务失败: " + ex.Message); }
                // 先等看门狗退出再删目录，避免其仍持有目录内文件句柄导致 rd 失败、卸载残留
                WaitWatchdogExit();
                try
                {
                    // 进程退出后再由系统删除全部文件
                    string dir = Application.StartupPath;
                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                        "/c ping 127.0.0.1 -n 6 > nul & rd /s /q \"" + dir + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                }
                catch (Exception ex) { Program.Log("卸载: 删除程序目录失败: " + ex.Message); }
            }

            Protection.UnprotectSelf();
            // 释放停止事件
            if (stopEvent != null)
            {
                try { stopEvent.Close(); }
                catch { }
            }
            Environment.Exit(0);
        }
    }
}
