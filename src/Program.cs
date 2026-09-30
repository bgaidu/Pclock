using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PCLock
{
    static class Program
    {
        static string LogPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "err.txt"); }
        }

        static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath,
                    "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + msg + Environment.NewLine);
            }
            catch { }
        }

        static void OnUnhandled(object sender, System.UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            Log("UNHANDLED EXCEPTION:\n" + (ex != null ? ex.ToString() : "unknown"));
            try
            {
                MessageBox.Show("PCLock 崩溃：\n\n" + (ex != null ? ex.Message : "unknown") +
                    "\n\n完整信息已写入 err.txt",
                    "PCLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log("UI THREAD EXCEPTION:\n" + e.Exception);
            try
            {
                MessageBox.Show("PCLock 出错：\n\n" + e.Exception.Message +
                    "\n\n完整信息已写入 err.txt",
                    "PCLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Log("=== PCLock Main start ===");
            Log("ExePath=" + Application.ExecutablePath);
            Log("StartupPath=" + Application.StartupPath);
            Log("Args=" + string.Join(" ", args));
            Log("CLR=" + Environment.Version);

            // 全局异常钩子
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            Application.ThreadException += OnThreadException;

            // 看门狗模式
            if (args.Length > 0 && args[0].ToLowerInvariant() == "--watchdog")
            {
                try
                {
                    Log("--- watchdog mode ---");
                    Watchdog.Run();
                    Log("--- watchdog returned ---");
                    return;
                }
                catch (Exception ex)
                {
                    Log("WATCHDOG EXCEPTION:\n" + ex);
                }
                return;
            }

            // 单实例
            bool createdNew = false;
            Mutex m = null;
            try
            {
                m = new Mutex(true, Constants.MutexName, out createdNew);
            }
            catch (Exception ex)
            {
                Log("Mutex create failed: " + ex);
                return;
            }
            if (!createdNew)
            {
                Log("Another instance running, exit.");
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Log("WinForms init done");

                Log("Creating App...");
                App app = new App();
                Log("App ctor done");

                Application.Run(app);
                Log("Application.Run returned");
            }
            catch (Exception ex)
            {
                Log("MAIN EXCEPTION:\n" + ex);
                try
                {
                    MessageBox.Show("PCLock 启动失败：\n\n" + ex.Message +
                        "\n\n完整信息已写入 err.txt",
                        "PCLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
            finally
            {
                Log("--- Main exit ---");
                if (m != null)
                {
                    try { m.ReleaseMutex(); } catch { }
                    m.Close();
                }
            }
        }
    }
}
