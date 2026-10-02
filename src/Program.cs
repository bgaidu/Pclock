using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PCLock
{
    static class Program
    {
        // === DPI 感知相关 ===
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        static extern int SetProcessDpiAwareness(int awareness);

        // DPI 感知级别
        const int PROCESS_DPI_UNAWARE = 0;
        const int PROCESS_SYSTEM_DPI_AWARE = 1;
        const int PROCESS_PER_MONITOR_DPI_AWARE = 2;

        static string LogPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "err.txt"); }
        }

        internal static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath,
                    "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + msg + Environment.NewLine);
            }
            catch { }
        }

        /// <summary>
        /// 设置 DPI 感知，Win8.1+ 使用 PerMonitor，Win7 回退到系统 DPI 感知
        /// </summary>
        static void SetDpiAwareness()
        {
            try
            {
                // Win8.1+ 尝试设置 Per-Monitor DPI 感知
                int result = SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE);
                if (result == 0)
                {
                    Log("DPI: Per-Monitor DPI Aware (Win8.1+)");
                    return;
                }
            }
            catch (EntryPointNotFoundException)
            {
                // Win7 上不存在此 API，回退
            }
            catch (DllNotFoundException)
            {
                // Win7 上不存在 shcore.dll，回退
            }
            catch (Exception ex)
            {
                Log("DPI: SetProcessDpiAwareness failed: " + ex.Message);
            }

            try
            {
                // Win7/Vista+ 回退：系统 DPI 感知
                bool ok = SetProcessDPIAware();
                Log("DPI: System DPI Aware (Win7 fallback), result=" + ok);
            }
            catch (Exception ex)
            {
                Log("DPI: SetProcessDPIAware failed: " + ex.Message);
            }
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
                // 设置 DPI 感知（必须在创建窗口前调用）
                SetDpiAwareness();

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
