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
            try { self = new Mutex(true, App.WatchMutexName); }
            catch (Exception) { }

            EventWaitHandle stop = null;
            try { stop = EventWaitHandle.OpenExisting(App.StopEventName); }
            catch (Exception) { }

            string exe = Application.ExecutablePath;
            try { File.AppendAllText(LogFile, "exe=" + exe + "\n"); } catch { }
            while (true)
            {
                Thread.Sleep(3000);
                if (stop != null && stop.WaitOne(0)) break;

                bool mainAlive = true;
                try { Mutex.OpenExisting(App.MutexName); }
                catch (WaitHandleCannotBeOpenedException) { mainAlive = false; }
                catch (Exception) { mainAlive = true; }

                if (!mainAlive)
                {
                    int flag = 0;
                    try { flag = Store.GetLockFlag(); } catch { }
                    try { File.AppendAllText(LogFile, "mainAlive=false, LockFlag=" + flag + "\n"); } catch { }
                    if (flag == 1)
                    {
                        Protection.SetTaskMgrDisabled(true);
                        try { Process.Start(exe); } catch (Exception) { }
                        Thread.Sleep(8000);   // 等主程序启动
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
    }
}
