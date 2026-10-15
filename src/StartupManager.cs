using System;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PCLock
{
    /// <summary>
    /// 开机自启管理：优先创建"登录时运行、最高权限"的计划任务（无 UAC 弹窗），
    /// 失败则退回 HKLM Run 键，最后验证计划任务是否存在。
    /// 从 App 主体抽为独立静态类：自启属于系统集成关注点，与倒计时/锁屏无耦合。
    /// </summary>
    public static class StartupManager
    {
        /// <summary>
        /// 确保开机自启已配置（幂等）。
        /// 注意：schtasks /TR 参数中路径含空格时必须用引号包裹，且引号需要正确转义。
        /// </summary>
        public static void Ensure()
        {
            string exe = Application.ExecutablePath;
            bool taskCreated = false;

            // 方法1：创建计划任务（登录时运行，最高权限）
            try
            {
                // schtasks 的 /TR 参数：值含空格时 schtasks 会在第一个空格处拆分命令，
                // 因此路径本身还要包一层内层引号：/TR "\"C:\path with space\PCLock.exe\""
                // /DELAY 0000:30 表示登录后延迟 30 秒触发（Win7+ 均支持）
                string arguments = "/Create /F /SC ONLOGON /RL HIGHEST /DELAY 0000:30 /TN \"" + Constants.TaskName + "\" /TR \"\\\"" + exe + "\\\"\"";

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
    }
}
