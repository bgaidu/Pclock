using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>
    /// 主程序：托盘常驻 + 倒计时 + 锁屏调度。
    /// 倒计时基于"剩余秒数"并定期写回注册表，改系统时间、重启都无法重置。
    ///
    /// 本类按职责拆分为多个文件（同一 partial class，行为不变）：
    ///   App.cs           核心：字段 / 构造 / 倒计时 / 锁屏调度 / 离线折算
    ///   App.Tray.cs      托盘与家长设置入口
    ///   App.Guard.cs     看门狗守护（GuardLoop / EnsureWatchdog / WaitWatchdogExit）
    ///   App.Power.cs     关机 / 睡眠事件订阅与处理
    ///   App.Shutdown.cs  退出与卸载
    ///   StartupManager.cs  开机自启（计划任务 / HKLM Run 回退），独立静态类
    /// </summary>
    public partial class App : ApplicationContext
    {
        public static App Instance;

        NotifyIcon tray;
        System.Windows.Forms.Timer timer;
        LockForm lockForm;
        int remaining;          // 剩余秒数
        string currentUser;     // 当前 Windows 用户（按用户分别限制）
        bool stopping;
        bool warned5, warned1; // 本轮是否已弹过 5 分钟 / 1 分钟提醒
        bool paused;            // 家长临时暂停计时（孩子仍可用，但不再倒计时）
        int usedPending;        // 未落盘的"今日已用"秒数（内存累加，见 FlushUsed）
        int persistFailCount;   // 连续持久化失败次数，达阈值进入安全锁屏（见 OnTick）
        DateTime lockStartTime; // 本次锁屏开始时间（用于审计日志计算锁屏时长）
        Thread guardThread;
        EventWaitHandle stopEvent; // 供看门狗监听的停止事件

        public App()
        {
            Instance = this;

            // 创建停止事件（供看门狗监听）。同名事件已存在时构造函数会直接打开它，不会抛异常
            try
            {
                stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, Constants.StopEventName);
            }
            catch (Exception ex)
            {
                Program.Log("停止事件创建失败，看门狗将无法收到退出信号: " + ex.Message);
                stopEvent = null;
            }

            Store.Init();
            Protection.ProtectSelf();     // 自保护：拒绝任务管理器结束进程
            EnsureWatchdog();             // 立即确保看门狗在运行（原依赖 GuardLoop 3 秒后首轮轮询才发现，存在保护空窗）
            StartupManager.Ensure();      // 开机自启（计划任务最高权限，无 UAC 弹窗）

            currentUser = Store.GetCurrentUser();
            remaining = Store.GetRemaining();
            bool midSession = remaining > 0;      // 有未用完的会话；-1/0 = 全新状态
            if (!midSession) remaining = Store.GetDurationMinutes(currentUser) * 60;
            AuditLog.Write("程序启动：用户=" + currentUser + "，剩余=" + FormatRemain(remaining));

            // 托盘图标
            BuildTray();

            // 每秒倒计时
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += OnTick;
            timer.Start();

            // 关机 / 睡眠事件（见 App.Power.cs）
            HookPowerEvents();

            // 看门狗守护线程：看门狗进程被杀则重新拉起
            guardThread = new Thread(GuardLoop);
            guardThread.IsBackground = true;
            guardThread.Start();

            // 重启仍锁：注册表里有锁屏标记，开机后立刻锁屏；
            // 上次是被硬断电打断的会话且时长已被扣光，同样开机即锁
            bool lockOnBoot = false;
            if (midSession && Store.GetLockFlag() != 1) lockOnBoot = DeductOffline();
            if (Store.GetLockFlag() == 1) LockNow(!Store.IsWithinWindow(currentUser));
            else if (lockOnBoot) LockNow(false);
        }

        /// <summary>
        /// 上次运行为硬断电中断时，按 UTC 墙钟折算扣除离线期间的时长，
        /// 防止直接断电"冻结"倒计时。时钟回拨时忽略（不能借此加时）。
        /// 返回时长是否已被扣到 0（需要开机即锁屏）。
        ///
        /// 注意：正常关机/重启时 Shutdown 会设置 LastSeenUtc=0，
        /// 此时不扣除，不会触发锁屏。
        /// 折算规则本身是 Store.DeductOffline 纯函数（有单元测试覆盖），这里只做接线与日志。
        /// </summary>
        bool DeductOffline()
        {
            long last = Store.GetLastSeenUtc();
            Store.OfflineDeductResult r = Store.DeductOffline(remaining, last, DateTime.UtcNow.Ticks);
            remaining = r.Remaining;
            if (!r.Deducted)
            {
                if (last <= 0)
                    Program.Log("DeductOffline: LastSeenUtc=0, 正常退出不扣除");
                else if (r.ElapsedSeconds <= 0)
                    Program.Log("DeductOffline: 时钟回拨，不扣除");
                else
                    Program.Log("DeductOffline: 离线时间 " + r.ElapsedSeconds.ToString("F1") + " 秒 < " +
                        Constants.OfflineDeductThresholdSec + " 秒，视为正常关机不扣除");
                return false;
            }
            Store.SetRemaining(remaining);
            Program.Log("DeductOffline: 离线扣除 " + r.DeductedSeconds + " 秒，剩余 " + remaining + " 秒");
            return remaining == 0;
        }

        void OnTick(object sender, EventArgs e)
        {
            if (stopping || lockForm != null) return;   // 锁屏/退出中不计时（避免退出过程中再次触发 LockNow）
            if (paused) { tray.Text = Strings.Get("电脑锁 - 已暂停"); return; }

            DateTime now = DateTime.Now;
            long utcNow = DateTime.UtcNow.Ticks;

            // P1-12 时间回拨主动检测（默认关闭）。UTC 墙钟只记录、不参与倒计时，
            // 因此与既有「改系统时间不能加时」的断电折算逻辑互不冲突。
            if (Store.IsAntiTamperEnabled())
            {
                long last = Store.GetLastKnownUtc();
                if (TimeTamper.IsBackward(last, utcNow, Constants.TimeTamperToleranceSec))
                {
                    if (!Store.IsTamperLocked())
                    {
                        Store.SetTamperLock(1);
                        AuditLog.Write("检测到系统时间大幅回拨，进入保护性锁定");
                        Program.Log("OnTick: 时间回拨超过容差，锁定");
                    }
                }
                else Store.SetLastKnownUtc(utcNow);
            }

            // P0-5 护眼休息 / 时间异常：截止前硬锁，不计时、不扣时长（UTC 截止，重启不失效）
            long restUntil = Store.GetRestUntilUtc();
            if (Store.IsTamperLocked() || RestCooldown.IsResting(restUntil, utcNow))
            {
                if (lockForm == null)
                    LockNow(true, Store.IsTamperLocked() ? "系统时间异常，保护性锁定" : "护眼休息时间");
                return;
            }

            // P0-4 时段：配置了新的多规则串则优先；否则完全沿用旧的 Win 字符串语义（旧安装行为不变）
            WindowDecision wd = WindowRules.Evaluate(Store.GetRules(currentUser), now);
            if (wd.HasRules)
            {
                if (wd.HardLock) { if (lockForm == null) LockNow(true); return; }
                if (wd.FreeUse) { tray.Text = Strings.Get("电脑锁 - 自由使用（不计时）"); return; }
            }
            else if (!Store.IsWithinWindow(currentUser))
            {
                if (lockForm == null) LockNow(true);
                return;
            }

            // P0-3 每日总额 / 每日解锁次数：达到上限后硬锁（仅家长 PIN 可临时放行）
            if (DailyQuota.ShouldLock(Store.GetUsedToday(currentUser),
                    Store.GetDailyLimitMinutes(currentUser),
                    Store.GetUnlockCountToday(currentUser), Store.GetUnlockLimit(currentUser)))
            {
                if (lockForm == null) LockNow(true, "今日使用额度已用完");
                return;
            }

            remaining--;
            usedPending++;   // 统计今日已用：仅内存累加，落盘见 FlushUsed
            // 用范围判断而非精确相等，避免 Timer 精度漂移跳过提醒
            if (!warned5 && remaining <= Constants.Warn5MinSec) { Warn("电脑还剩 5 分钟使用时间"); warned5 = true; }
            if (!warned1 && remaining <= Constants.Warn1MinSec) { Warn("电脑还剩 1 分钟使用时间"); warned1 = true; }
            if (remaining <= 0)
            {
                FlushUsed();
                BeginRestIfEnabled();
                if (RestCooldown.IsResting(Store.GetRestUntilUtc(), DateTime.UtcNow.Ticks))
                    LockNow(true, "护眼休息时间");
                else
                    LockNow(false);
                return;
            }
            if (remaining % Constants.PersistIntervalSec == 0)
            {
                try
                {
                    Store.SetRemaining(remaining);
                    Store.SetLastSeenUtc(DateTime.UtcNow.Ticks);
                    persistFailCount = 0;
                }
                catch (Exception ex)
                {
                    // Store 内部已做异常保护，这里是双保险：连续失败进入安全态（立即锁屏），
                    // 防止计时状态丢失后被"反复重启"绕过倒计时
                    persistFailCount++;
                    Program.Log("OnTick: 持久化失败第 " + persistFailCount + " 次: " + ex.Message);
                    if (persistFailCount >= 3)
                    {
                        Program.Log("OnTick: 连续持久化失败，进入安全锁屏");
                        FlushUsed();
                        LockNow(false);
                        return;
                    }
                }
                FlushUsed();   // 与剩余时间同频落盘
            }
            tray.Text = Strings.Fmt("电脑锁 - 剩余 {0}", FormatRemain(remaining));
        }

        /// <summary>
        /// 把内存里累计的"今日已用秒数"写回注册表。
        /// 原先 OnTick 每秒直接写一次 HKLM；改为内存累加后与 remaining 同频（每
        /// PersistIntervalSec 秒）落盘，注册表写次数约为原来的 1/15。锁屏/解锁/关机/
        /// 退出等节点均强制 flush，避免丢统计。
        /// </summary>
        void FlushUsed()
        {
            if (usedPending <= 0) return;
            int n = usedPending;
            usedPending = 0;
            try
            {
                Store.AddUsedSeconds(currentUser, n);
                // P1-10：按天落盘累计秒数，供 7 天使用记录视图读取（覆盖写，不额外增加写次数上限）
                Store.RecordUsageDay(currentUser, DateTime.Now, Store.GetUsedToday(currentUser));
            }
            catch (Exception ex) { Program.Log("FlushUsed 失败: " + ex.Message); }
        }

        /// <summary>P0-5：时长用尽时按配置进入护眼休息（截止时间戳 UTC 持久化，重启不失效）</summary>
        void BeginRestIfEnabled()
        {
            int minutes = Store.GetRestMinutes(currentUser);
            if (minutes <= 0) return;
            Store.SetRestUntilUtc(RestCooldown.Deadline(DateTime.UtcNow.Ticks, minutes));
            AuditLog.Write("护眼休息开始：" + minutes + " 分钟");
        }

        static string FormatRemain(int sec)
        {
            int h = sec / 3600;
            int m = (sec % 3600) / 60;
            if (h > 0) return Strings.Fmt("{0} 小时 {1} 分", h, m);
            return Strings.Fmt("{0} 分 {1} 秒", m, sec % 60);
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

        /// <summary>进入锁屏（到时锁定 / 开机带锁标记 / 手动锁定都走这里）。默认按当前时段状态判定原因。</summary>
        public void LockNow()
        {
            LockNow(!Store.IsWithinWindow(currentUser), null);
        }

        public void LockNow(bool dueToWindow) { LockNow(dueToWindow, null); }

        /// <summary>
        /// 进入锁屏。dueToWindow=true 表示硬锁（禁用答题，仅家长 PIN 或条件满足时自动解锁，
        /// 期间不计时不扣时长）；reason 非空时用于区分额度用尽 / 护眼休息 / 时间异常等硬锁原因，
        /// 此时不启用「进入允许时段自动解锁」。
        /// </summary>
        public void LockNow(bool dueToWindow, string reason)
        {
            FlushUsed();                              // 进入锁屏前先结掉本次统计
            if (lockForm != null) return;
            lockStartTime = DateTime.Now;
            AuditLog.Write("锁屏开始：用户=" + currentUser + "，原因=" +
                (dueToWindow ? "不在可用时段（硬锁）" : "使用时长用尽/手动锁定"));
            Store.SetLockFlag(1);                     // 持久化：重启后仍处于锁定
            Protection.SetTaskMgrDisabled(true);      // 锁屏期间禁用任务管理器
            if (!string.IsNullOrEmpty(reason)) AuditLog.Write("硬锁原因：" + reason);
            LockForm lf = new LockForm(dueToWindow, reason);
            lf.UnlockRequested += delegate { Unlock(lf.UnlockReason); };
            lockForm = lf;
            lf.Show();
            lf.Activate();
        }

        /// <summary>当前登录用户（供锁屏界面判断时段）</summary>
        public string CurrentUser { get { return currentUser; } }

        /// <summary>当前用户是否处于允许使用时段内</summary>
        public bool IsWithinWindow() { return Store.IsWithinWindow(currentUser); }

        /// <summary>解锁（答对题或家长PIN成功后调用）。解锁即开始新一轮计时。</summary>
        public void Unlock() { Unlock("未知"); }

        /// <summary>解锁并记录解锁方式（答题/家长密码/时段自动等）到审计日志。</summary>
        public void Unlock(string reason)
        {
            FlushUsed();                               // 新一轮计时开始前先结掉统计
            if (lockForm != null)
            {
                lockForm.AllowClose = true;
                lockForm.Dispose();
                lockForm = null;
            }
            Store.SetLockFlag(0);
            // P1-8：星级奖励在解锁时一次性结算（未启用时恒为 0，行为不变）
            int reward = Store.TakePendingReward();
            remaining = Store.GetDurationMinutes(currentUser) * 60 + reward;
            Store.SetRemaining(remaining);
            Store.SetLastSeenUtc(DateTime.UtcNow.Ticks);   // 新会话从现在起算
            warned5 = warned1 = false;                     // 重置提醒标志
            Protection.SetTaskMgrDisabled(false);
            paused = false;                                // 新会话恢复计时
            Store.ClearRest();                              // P0-5 家长解锁即结束休息
            Store.SetTamperLock(0);                         // P1-12 家长解锁清除时间异常锁定
            Store.IncrementUnlockCount(currentUser);        // P0-3 今日解锁次数（跨天自动归零）

            string duration = "";
            if (lockStartTime != default(DateTime))
            {
                TimeSpan ts = DateTime.Now - lockStartTime;
                duration = "，锁屏时长 " + (int)ts.TotalMinutes + " 分 " + ts.Seconds + " 秒";
                lockStartTime = default(DateTime);
            }
            AuditLog.Write("锁屏结束：用户=" + currentUser + "，方式=" + reason + duration);
        }

        public void ResetRemaining()
        {
            remaining = Store.GetDurationMinutes(currentUser) * 60;
            Store.SetRemaining(remaining);
            Store.SetLastSeenUtc(DateTime.UtcNow.Ticks);
            warned5 = warned1 = false;                     // 重置提醒标志
            paused = false;                                // 恢复计时
        }

        /// <summary>家长临时暂停/继续计时（暂停时孩子仍可使用电脑，但剩余时间不再递减）</summary>
        public void SetPaused(bool p) { paused = p; }
        public bool IsPaused() { return paused; }

        /// <summary>家长为本次会话临时增加使用时间（分钟）</summary>
        public void AddTime(int minutes)
        {
            remaining += minutes * 60;
            if (remaining < 0) remaining = 0;
            Store.SetRemaining(remaining);
        }

        /// <summary>星星兑换等按秒加时：立即生效并持久化（不足 1 分钟也不丢）</summary>
        public void AddTimeSeconds(int seconds)
        {
            if (seconds <= 0) return;
            remaining += seconds;
            Store.SetRemaining(remaining);
        }

        /// <summary>P0-5：家长临时放行 N 分钟（不重置为完整一轮时长，到点再次锁定）</summary>
        public void TempPass(int minutes)
        {
            Unlock("家长临时放行 " + minutes + " 分钟");
            remaining = minutes * 60;
            Store.SetRemaining(remaining);
            Store.SetLastSeenUtc(DateTime.UtcNow.Ticks);
            warned5 = warned1 = false;
        }

        /// <summary>P1-9：练习模式入口（不解锁、不消耗额度；主计时继续，不构成绕过口子）</summary>
        public void OpenPractice()
        {
            try
            {
                using (PracticeForm pf = new PracticeForm()) pf.ShowDialog();
            }
            catch (Exception ex) { Program.Log("OpenPractice 失败: " + ex.Message); }
        }
    }
}
