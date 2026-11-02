using System;
using System.IO;
using System.Text;

namespace PCLock
{
    /// <summary>
    /// 按天记录的行为审计日志：每天一个文件（程序目录下 logs\yyyy-MM-dd.log）。
    /// 记录密码输入、设置修改、锁屏/解锁、程序启动/退出等关键事件，便于家长查看。
    /// 与 err.txt（异常/诊断）分离；任何写入失败都静默，绝不影响主程序运行。
    /// </summary>
    public static class AuditLog
    {
        static readonly object Gate = new object();

        /// <summary>日志目录（程序所在目录下的 logs 子目录）</summary>
        public static string LogDirectory
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"); }
        }

        /// <summary>记录一条事件，自动归入当天文件；跨天时创建新文件并写入日期标题。</summary>
        public static void Write(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                DateTime now = DateTime.Now;
                string day = now.ToString("yyyy-MM-dd");
                string path = Path.Combine(LogDirectory, day + ".log");
                lock (Gate)
                {
                    if (!Directory.Exists(LogDirectory))
                        Directory.CreateDirectory(LogDirectory);

                    bool isNew = !File.Exists(path);
                    string line = "[" + now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine;
                    if (isNew) line = "===== " + day + " =====" + Environment.NewLine + line;
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
            catch
            {
                // 审计日志失败不影响主程序
            }
        }
    }
}
