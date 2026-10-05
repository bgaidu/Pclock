using System;

namespace PCLock
{
    /// <summary>项目常量集中管理，便于维护和测试</summary>
    public static class Constants
    {
        // === 注册表/计划任务/同步对象名称 ===
        public const string RegPath = "SOFTWARE\\PCLock";
        public const string MutexName = "Local\\PCLock_Main";
        public const string WatchMutexName = "Local\\PCLock_Watchdog";
        public const string StopEventName = "Local\\PCLock_Stop";
        public const string TaskName = "PCLock";

        // === 时间相关（秒）===
        public const int DefaultDurationMinutes = 60;
        public static readonly int[] AllowedDurations = new int[] { 15, 30, 45, 60, 90, 120 };
        public const int Warn5MinSec = 300;      // 5 分钟提醒阈值
        public const int Warn1MinSec = 60;       // 1 分钟提醒阈值
        public const int PersistIntervalSec = 15; // 持久化间隔

        // === 锁屏答题 ===
        public const int TotalQuestions = 10;    // 每次锁屏题数
        public const int WrongCooldownSec = 5;   // 答错冷却秒数

        // === PIN/密码策略 ===
        public const int PinMinLength = 4;
        public const int PinMaxLength = 20;
        public const int PinMaxFails = 5;        // 连续失败次数触发冷却
        public const int PinCooldownSec = 30;    // 全局冷却秒数
        // 25000 次：单次验证约 0.2 秒（UI 线程可接受），同时使 4 位 PIN 的离线枚举成本
        // 相比裸 SHA256 提高两万余倍。不要随意调大——验证在 UI 线程同步执行。
        public const int PinHashIterations = 25000;

        // === 看门狗/守护线程 ===
        public const int GuardCheckIntervalMs = 3000;   // 守护/看门狗检查间隔
        public const int WatchdogStartupWaitMs = 8000;  // 看门狗等待主程序启动上限
        public const int WatchdogPollIntervalMs = 500;  // 看门狗轮询间隔

        // === UI ===
        public const int UiTimerIntervalMs = 500; // LockForm 保持全屏/焦点定时器

        // === 默认值 ===
        public const string DefaultPin = "1234";
        public const string DefaultUninstallPin = "1234";

        // === 低级键盘钩子拦截的虚拟键 ===
        public static readonly int[] BlockedVk = new int[] {
            0x5B, 0x5C,           // 左/右 Win 键
            0x09,                 // Tab（配合 Alt/Shift 单独处理）
            0x1B,                 // Esc（配合 Alt/Ctrl 单独处理）
            0x25, 0x26, 0x27, 0x28 // 方向键（配合 Alt 单独处理）
        };
    }
}