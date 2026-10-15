using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PCLock
{
    /// <summary>App 的拆分文件：关机 / 睡眠事件（同 partial class，行为不变）</summary>
    public partial class App : ApplicationContext
    {
        /// <summary>
        /// 订阅关机 / 睡眠事件。
        /// 正常注销/关机（开始菜单关机、重启、注销）时清掉时间戳并保存剩余时间，
        /// 下次开机不按离线时长扣减；硬断电收不到该事件，仍会在下次开机扣离线时长。
        /// 若关机被其他程序取消，OnTick 会在 15 秒内重新写回时间戳。
        /// 休眠/睡眠同样"不扣时长"：挂起时清时间戳、保存剩余，醒来后继续倒计时。
        /// （OnTick 在系统挂起期间停摆，醒来继续递减，本就不扣；此处显式清时间戳使其语义与关机一致。）
        /// </summary>
        void HookPowerEvents()
        {
            SystemEvents.SessionEnding += OnSessionEnding;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            if (stopping) return;
            try
            {
                FlushUsed();
                Store.SetRemaining(remaining);
                Store.SetLastSeenUtc(0);
            }
            catch (Exception ex)
            {
                Program.Log("OnSessionEnding 失败: " + ex.Message);
            }
        }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (stopping) return;
            // 休眠/睡眠：不扣时长，仅保存剩余并清时间戳（与正常关机一致）。
            if (e.Mode == PowerModes.Suspend)
            {
                try
                {
                    FlushUsed();
                    Store.SetRemaining(remaining);
                    Store.SetLastSeenUtc(0);
                }
                catch (Exception ex)
                {
                    Program.Log("OnPowerModeChanged(Suspend) 失败: " + ex.Message);
                }
            }
            // Resume：不处理，OnTick 醒来后继续递减。
        }
    }
}
