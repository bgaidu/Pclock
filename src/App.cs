using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PCLock
{
    /// <summary>
    /// 主程序：托盘常驻 + 倒计时 + 锁屏调度。
    /// 倒计时基于"剩余秒数"并定期写回注册表，改系统时间、重启都无法重置。
    /// </summary>
    public class App : ApplicationContext
    {
        public static App Instance;

        NotifyIcon tray;
        System.Windows.Forms.Timer timer;
        LockForm lockForm;
        int remaining;          // 剩余秒数
        bool stopping;
        bool warned5, warned1; // 本轮是否已弹过 5 分钟 / 1 分钟提醒
        Thread guardThread;
        EventWaitHandle stopEvent; // 供看门狗监听的停止事件

        public App()
        {
            Instance = this;

            // 创建停止事件（供看门狗监听），必须在 Store.Init 之前创建，确保看门狗能打开
            try
            {
                stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, Constants.StopEventName);
            }
            catch (Exception)
            {
                // 已存在则打开现有的
                try { stopEvent = EventWaitHandle.OpenExisting(Constants.StopEventName); }
                catch { stopEvent = null; }
            }

            Store.Init();
            Protection.ProtectSelf();     // 自保护：拒绝任务管理器结束进程
            EnsureStartup();              // 开机自启（计划任务最高权限，无 UAC 弹窗）

            remaining = Store.GetRemaining();
            bool midSession = remaining > 0;      // 有未用完的会话；-1/0 = 全新状态
            if (!midSession) remaining = Store.GetDurationMinutes() * 60;

            // 托盘图标
            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Shield;
            tray.Text = "电脑锁 - 运行中";
            tray.Visible = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("设置…（需家长PIN）", null, delegate { ShowSettings(); });
            menu.Items.Add("立即锁定", null, delegate { LockNow(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowSettings(); };

            // 每秒倒计时
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += OnTick;
            timer.Start();

            // 看门狗守护线程：看门狗进程被杀则重新拉起
            guardThread = new Thread(GuardLoop);
            guardThread.IsBackground = true;
            guardThread.Start();

            // 重启仍锁：注册表里有锁屏标记，开机后立刻锁屏；
            // 上次是被硬断电打断的会话且时长已被扣光，同样开机即锁
            bool lockOnBoot = false;
            if (midSession && Store.GetLockFlag() != 1) lockOnBoot = DeductOffline();
            if (Store.GetLockFlag() == 1 || lockOnBoot) LockNow();
        }

        /// <summary>
        /// 上次运行为硬断电中断时，按 UTC 墙钟折算扣除离线期间的时长，
        /// 防止直接断电"冻结"倒计时。时钟回拨时忽略（不能借此加时）。
        /// 返回时长是否已被扣到 0（需要开机即锁屏）。
        /// </summary>
        bool DeductOffline()
        {
            long last = Store.GetLastSeenUtc();
            if (last <= 0) return false;   // 上次是正常退出（时间戳已清），不扣
            
            // 使用浮点除法并向上取整，避免整数除法向下取整导致少扣时间
            double elapsedSec = (DateTime.UtcNow.Ticks - last) / (double)TimeSpan.TicksPerSecond;
            if (elapsedSec <= 0) return false;
            
            long elapsedCeil = (long)Math.Ceiling(elapsedSec);
            remaining -= (int)Math.Min(elapsedCeil, (long)int.MaxValue);
            if (remaining < 0) remaining = 0;
            Store.SetRemaining(remaining);
            return remaining == 0;
        }

        void OnTick(object sender, EventArgs e)
        {
            if (lockForm != null) return;   // 锁屏期间不计时
            remaining--;
            // 用范围判断而非精确相等，避免 Timer 精度漂移跳过提醒
            if (!warned5 && remaining <= Constants.Warn5MinSec) { Warn("电脑还剩 5 分钟使用时间"); warned5 = true; }
            if (!warned1 && remaining <= Constants.Warn1MinSec) { Warn("电脑还剩 1 分钟使用时间"); warned1 = true; }
            if (remaining <= 0) { LockNow(); return; }
            if (remaining % Constants.PersistIntervalSec == 0)
            {
                Store.SetRemaining(remaining);
                Store.SetLastSeenUtc(DateTime.UtcNow.Ticks);
            }
            tray.Text = "电脑锁 - 剩余 " + FormatRemain(remaining);
        }

        static string FormatRemain(int sec)
        {
            int h = sec / 3600;
            int m = (sec % 3600) / 60;
            if (h > 0) return h + " 小时 " + m + " 分";
            return m + " 分 " + (sec % 60) + " 秒";
        }

        void Warn(string text)
        {
            try
            {
                tray.BalloonTipTitle = "电脑锁";
                tray.BalloonTipText = text;
                tray.ShowBalloonTip(10000);
            }
            catch (Exception) { }
        }

        /// <summary>进入锁屏（到时锁定 / 开机带锁标记 / 手动锁定都走这里）</summary>
        public void LockNow()
        {
            if (lockForm != null) return;
            Store.SetLockFlag(1);                     // 持久化：重启后仍处于锁定
            Protection.SetTaskMgrDisabled(true);      // 锁屏期间禁用任务管理器
            lockForm = new LockForm();
            lockForm.UnlockRequested += delegate { Unlock(); };
            lockForm.Show();
            lockForm.Activate();
        }

        /// <summary>解锁（答对题或家长PIN成功后调用）。解锁即开始新一轮计时。</summary>
        public void Unlock()
        {
            if (lockForm != null)
            {
                lockForm.AllowClose = true;
                lockForm.Close();
                lockForm = null;
            }
            Store.SetLockFlag(0);
            remaining = Store.GetDurationMinutes() * 60;
            Store.SetRemaining(remaining);
            Store.SetLastSeenUtc(DateTime.UtcNow.Ticks);   // 新会话从现在起算
            warned5 = warned1 = false;                     // 重置提醒标志
            Protection.SetTaskMgrDisabled(false);
        }

        public void ResetRemaining()
        {
            remaining = Store.GetDurationMinutes() * 60;
            Store.SetRemaining(remaining);
            Store.SetLastSeenUtc(DateTime.UtcNow.Ticks);
            warned5 = warned1 = false;                     // 重置提醒标志
        }

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
                catch (Exception) { wdAlive = true; }
                if (!wdAlive)
                {
                    try { Process.Start(Application.ExecutablePath, "--watchdog"); }
                    catch (Exception) { }
                    Thread.Sleep(5000);
                }
                if (lockForm != null) Protection.SetTaskMgrDisabled(true);
            }
        }

        public void ShowSettings()
        {
            bool ok = false;
            using (PinDialog pd = new PinDialog("家长验证", "请输入家长 PIN：",
                delegate(string s) { return Store.VerifyPin(s); }))
            {
                ok = (pd.ShowDialog() == DialogResult.OK);
            }
            if (!ok) return;
            using (SettingsForm sf = new SettingsForm()) sf.ShowDialog();
        }

        /// <summary>
        /// 开机自启：优先创建"登录时运行、最高权限"的计划任务（无UAC弹窗），失败则退回 HKLM Run 键。
        /// 注意：schtasks /TR 参数中路径含空格时必须用引号包裹，且引号需要正确转义。
        /// </summary>
        void EnsureStartup()
        {
            string exe = Application.ExecutablePath;
            bool taskCreated = false;

            // 方法1：创建计划任务（登录时运行，最高权限）
            try
            {
                // schtasks 的 /TR 参数：路径含空格时必须用引号包裹
                // /DELAY 0000:30 表示登录后延迟 30 秒触发（Win7+ 均支持）
                string arguments = "/Create /F /SC ONLOGON /RL HIGHEST /DELAY 0000:30 /TN \"" + Constants.TaskName + "\" /TR \"" + exe + "\"";
                
                ProcessStartInfo psi = new ProcessStartInfo("schtasks", arguments);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit(10000);
                    
                    if (p.ExitCode == 0)
                    {
                        taskCreated = true;
                        Program.Log("EnsureStartup: 计划任务创建成功");
                    }
                    else
                    {
                        Program.Log("EnsureStartup: 计划任务创建失败，ExitCode=" + p.ExitCode);
                        Program.Log("EnsureStartup: stdout=" + output);
                        Program.Log("EnsureStartup: stderr=" + error);
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log("EnsureStartup: 计划任务创建异常: " + ex.Message);
            }

            // 方法2：回退到 HKLM Run 键
            if (!taskCreated)
            {
                try
                {
                    using (RegistryKey k = Registry.LocalMachine.CreateSubKey(
                        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run"))
                    {
                        k.SetValue("PCLock", "\"" + exe + "\"");
                        Program.Log("EnsureStartup: 已写入 HKLM Run 键回退");
                    }
                }
                catch (Exception ex)
                {
                    Program.Log("EnsureStartup: HKLM Run 键写入失败: " + ex.Message);
                }
            }

            // 验证：检查计划任务是否存在
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks",
                    "/Query /TN \"" + Constants.TaskName + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    if (p.ExitCode == 0 && output.Contains(Constants.TaskName))
                    {
                        Program.Log("EnsureStartup: 验证计划任务存在");
                    }
                    else
                    {
                        Program.Log("EnsureStartup: 验证计划任务不存在！");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log("EnsureStartup: 验证计划任务异常: " + ex.Message);
            }
        }

        /// <summary>退出 / 卸载</summary>
        public void Shutdown(bool uninstall)
        {
            stopping = true;
            try
            {
                if (lockForm != null)
                {
                    lockForm.AllowClose = true;
                    lockForm.Close();
                    lockForm = null;
                }
                Store.SetLockFlag(0);
                Protection.SetTaskMgrDisabled(false);
                if (!uninstall)
                {
                    // 与退出确认框语义一致："下次开机自动重新开始"——
                    // 重置为完整时长，并清掉断电折算时间戳，下次开机不扣离线时长
                    remaining = Store.GetDurationMinutes() * 60;
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
                try { Store.RemoveAll(); } catch (Exception) { }
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("schtasks",
                        "/Delete /F /TN \"" + Constants.TaskName + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    using (Process p = Process.Start(psi)) { p.WaitForExit(5000); }
                }
                catch (Exception) { }
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
                catch (Exception) { }
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
