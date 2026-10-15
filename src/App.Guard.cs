using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>App 的拆分文件：看门狗守护（同 partial class，行为不变）</summary>
    public partial class App : ApplicationContext
    {
        /// <summary>守护线程：看门狗进程被杀 → 重新拉起；锁屏期间持续确保任务管理器被禁用</summary>
        void GuardLoop()
        {
            while (!stopping)
            {
                Thread.Sleep(Constants.GuardCheckIntervalMs);
                if (stopping) break;
                bool wdAlive = true;
                try { Mutex.OpenExisting(Constants.WatchMutexName); }
                catch (WaitHandleCannotBeOpenedException) { wdAlive = false; }
                catch (Exception ex)
                {
                    // 其他异常（如 UnauthorizedAccessException）保守视为存活，但记录日志
                    wdAlive = true;
                    Program.Log("GuardLoop: 看门狗状态检测异常（保守视为存活）: " + ex.Message);
                }
                if (!wdAlive)
                {
                    try { Process.Start(Application.ExecutablePath, "--watchdog"); }
                    catch (Exception ex) { Program.Log("GuardLoop: 看门狗重新拉起失败: " + ex.Message); }
                    Thread.Sleep(5000);
                }
                if (lockForm != null) Protection.SetTaskMgrDisabled(true);
            }
        }

        /// <summary>
        /// 确保看门狗进程在运行（幂等）。原先仅依赖 GuardLoop 首轮 3 秒轮询才发现并拉起
        /// 看门狗，开机自启后的前 3 秒处于无看门狗保护的空窗；改为主程序构造时立即检查一次。
        /// </summary>
        void EnsureWatchdog()
        {
            try
            {
                Mutex.OpenExisting(Constants.WatchMutexName);
                return;   // 看门狗已在运行
            }
            catch (WaitHandleCannotBeOpenedException) { }
            catch (Exception ex)
            {
                // 其他异常保守处理：不在此处拉起，交给 GuardLoop 后续轮询
                Program.Log("EnsureWatchdog: 看门狗状态检测异常: " + ex.Message);
                return;
            }
            try
            {
                Process.Start(Application.ExecutablePath, "--watchdog");
                Program.Log("EnsureWatchdog: 看门狗已启动");
            }
            catch (Exception ex)
            {
                Program.Log("EnsureWatchdog: 启动失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 等待看门狗进程自行退出（收到停止事件后最多一个轮询周期内退出）。
        /// 看门狗以 Local\PCLock_Watchdog 互斥体标识存活，轮询该互斥体消失即视为已退出；
        /// 未等待就删目录会因文件占用导致 rd /s /q 失败、残留进程与文件。
        /// </summary>
        void WaitWatchdogExit()
        {
            int waited = 0;
            while (waited < Constants.WatchdogExitWaitMs)
            {
                try
                {
                    Mutex.OpenExisting(Constants.WatchMutexName);
                    // 仍存活：继续等待
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    Program.Log("WaitWatchdogExit: 看门狗已退出");
                    return;
                }
                catch (Exception) { }
                Thread.Sleep(Constants.WatchdogPollIntervalMs);
                waited += Constants.WatchdogPollIntervalMs;
            }
            Program.Log("WaitWatchdogExit: 等待超时（" + Constants.WatchdogExitWaitMs + "ms），继续卸载；删目录可能因文件占用失败");
        }
    }
}
