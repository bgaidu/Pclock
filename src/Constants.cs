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

        // === 年级 / 学科 ===
        public static readonly int[] AllowedGrades = new int[] { 1, 2, 3, 4, 5, 6 };
        public const int Warn5MinSec = 300;      // 5 分钟提醒阈值
        public const int Warn1MinSec = 60;       // 1 分钟提醒阈值
        public const int PersistIntervalSec = 15; // 持久化间隔
        public const int OfflineDeductThresholdSec = 30; // 硬断电离线折算阈值：离线低于此值视为正常关机延迟，不扣时长

        // === 锁屏答题 ===
        public const int TotalQuestions = 10;    // 每次锁屏题数
        public const int WrongCooldownSec = 5;   // 答错冷却秒数

        // === PIN/密码策略 ===
        public const int PinMinLength = 4;
        public const int PinMaxLength = 12;      // 密码最多 12 位（"12 位以下"）
        public const int PinMaxFails = 5;        // 连续失败次数触发冷却
        public const int PinCooldownSec = 30;    // 全局冷却秒数
        // 25000 次：单次验证约 0.2 秒（UI 线程可接受），同时使 4 位 PIN 的离线枚举成本
        // 相比裸 SHA256 提高两万余倍。不要随意调大——验证在 UI 线程同步执行。
        public const int PinHashIterations = 25000;

        /// <summary>密码规则提示文案（设置界面与消息框共用）</summary>
        public const string PinRuleText = "密码需为 4-12 位，可包含数字、字母和特殊符号，不能含空格。";

        /// <summary>
        /// 校验密码是否符合规则：长度 4~12 位，可含数字、字母和特殊符号；
        /// 不允许空白字符与控制字符。默认密码 1234（纯数字）同样符合该规则。
        /// </summary>
        public static bool IsValidPin(string p)
        {
            if (string.IsNullOrEmpty(p)) return false;
            if (p.Length < PinMinLength || p.Length > PinMaxLength) return false;
            foreach (char c in p)
            {
                if (char.IsControl(c) || char.IsWhiteSpace(c)) return false;
            }
            return true;
        }

        // === 看门狗/守护线程 ===
        public const int GuardCheckIntervalMs = 3000;   // 守护/看门狗检查间隔
        public const int WatchdogStartupWaitMs = 8000;  // 看门狗等待主程序启动上限
        public const int WatchdogPollIntervalMs = 500;  // 看门狗轮询间隔
        public const int WatchdogExitWaitMs = 6000;     // 卸载时等待看门狗自行退出的上限

        // === UI ===
        public const int UiTimerIntervalMs = 500; // LockForm 保持全屏/焦点定时器

        // === 默认值 ===
        public const int DefaultGrade = 3;
        public const int DefaultSubject = (int)Subject.Mixed; // 0 = 混合随机
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