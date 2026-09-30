using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>
    /// 看门狗模式（PCLock.exe --watchdog）：
    /// 同样受 DACL 保护；发现主进程消失时——
    ///   若处于锁屏状态 → 立即重新拉起主程序（开机带锁标记会自动继续锁屏）；
    ///   若主程序正常退出（收到停止信号）→ 自行退出。
    /// </summary>
    public static class Watchdog
    {
        static string LogDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }
        static string LogFile
        {
            get { return System.IO.Path.Combine(LogDir, "watchdog_log.txt"); }
        }
        static string CrashFile
        {
            get { return System.IO.Path.Combine(LogDir, "watchdog_crash.log"); }
        }

        public static void Run()
        {
            try
            {
                File.WriteAllText(LogFile, "Run started\n");
                RunInternal();
                File.WriteAllText(LogFile, "RunInternal returned\n");
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(CrashFile, ex.ToString()); } catch { }
                Environment.Exit(1);
            }
        }

        static void RunInternal()
        {
            // 必须先初始化注册表访问（watchdog 是独立进程，Store.root 在此为空）
            try { File.AppendAllText(LogFile, "Store.Init\n"); } catch { }
            try { Store.Init(); }
            catch (Exception ex)
            {
                try { File.AppendAllText(LogFile, "Store.Init FAILED: " + ex.Message + "\n"); } catch { }
            }

            try { File.AppendAllText(LogFile, "ProtectSelf\n"); } catch { }
            Protection.ProtectSelf();
            try { File.AppendAllText(LogFile, "SetTaskMgrDisabled\n"); } catch { }
            int lockFlag = 0;
            try { lockFlag = Store.GetLockFlag(); }
            catch (Exception ex)
            {
                try { File.AppendAllText(LogFile, "GetLockFlag FAILED: " + ex.Message + "\n"); } catch { }
            }
            Protection.SetTaskMgrDisabled(lockFlag == 1);
            try { File.AppendAllText(LogFile, "After SetTaskMgrDisabled, LockFlag=" + lockFlag + "\n"); } catch { }

            Mutex self = null;
            try { self = new Mutex(true, Constants.WatchMutexName); }
            catch (Exception) { }

            EventWaitHandle stop = null;
            try { stop = EventWaitHandle.OpenExisting(Constants.StopEventName); }
            catch (Exception) { }

            string exe = Application.ExecutablePath;
            try { File.AppendAllText(LogFile, "exe=" + exe + "\n"); } catch { }
            while (true)
            {
                Thread.Sleep(Constants.GuardCheckIntervalMs);
                if (stop != null && stop.WaitOne(0)) break;

                bool mainAlive = true;
                try { Mutex.OpenExisting(Constants.MutexName); }
                catch (WaitHandleCannotBeOpenedException) { mainAlive = false; }
                // 仅捕获 WaitHandleCannotBeOpenedException 判定为进程死亡
                // 其他异常（如 UnauthorizedAccessException）不应被视为进程存活，记录日志并保守处理

                if (!mainAlive)
                {
                    int flag = 0;
                    try { flag = Store.GetLockFlag(); } catch { }
                    try { File.AppendAllText(LogFile, "mainAlive=false, LockFlag=" + flag + "\n"); } catch { }
                    if (flag == 1)
                    {
                        Protection.SetTaskMgrDisabled(true);
                        try { Process.Start(exe); } catch (Exception) { }
                        // 不再固定 Sleep 8 秒，改用轮询检测 Mutex 是否重新出现
                        WaitForMainProcessStart(exe);
                    }
                    else
                    {
                        try { File.AppendAllText(LogFile, "breaking\n"); } catch { }
                        break;   // 主程序已正常退出，跟随退出
                    }
                }
            }

            try { File.AppendAllText(LogFile, "UnprotectSelf\n"); } catch { }
            Protection.UnprotectSelf();
            if (self != null)
            {
                try { self.ReleaseMutex(); } catch (Exception) { }
                try { self.Close(); } catch (Exception) { }
            }
        }

        /// <summary>轮询等待主程序启动（通过 Mutex 出现判断），最多等待 WatchdogStartupWaitMs</summary>
        static void WaitForMainProcessStart(string exe)
        {
            int elapsed = 0;
            while (elapsed < Constants.WatchdogStartupWaitMs)
            {
                Thread.Sleep(Constants.WatchdogPollIntervalMs);
                elapsed += Constants.WatchdogPollIntervalMs;
                try
                {
                    Mutex.OpenExisting(Constants.MutexName);
                    try { File.AppendAllText(LogFile, "Main process restarted (mutex detected)\n"); } catch { }
                    return; // 主程序已启动
                }
                catch (WaitHandleCannotBeOpenedException) { /* 继续等待 */ }
                catch (Exception) { /* 其他异常记录日志但继续等待 */ }
            }
            try { File.AppendAllText(LogFile, "Main process restart timeout\n"); } catch { }
        }
    }
}
