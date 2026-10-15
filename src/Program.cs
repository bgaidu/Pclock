using System;
using System.Drawing;
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
                // 日志滚动：超过 512KB 时清空重建，避免长期运行无限增长
                if (File.Exists(LogPath))
                {
                    try
                    {
                        if (new FileInfo(LogPath).Length > 512 * 1024)
                            File.Delete(LogPath);
                    }
                    catch { }
                }
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

        [DllImport("user32.dll")]
        static extern int GetGuiResources(IntPtr hProcess, int uiFlags);

        static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Exception ex = e.Exception;

            // 附带诊断上下文：当前活动窗体 + 进程 USER/GDI 对象数（用于判断是否句柄泄漏/定位窗口）
            string diag = "";
            try
            {
                Form af = Form.ActiveForm;
                diag = "ActiveForm=" + (af != null ? af.GetType().Name : "null");
                using (System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess())
                {
                    int userObj = GetGuiResources(proc.Handle, 1);   // GR_USEROBJECTS
                    int gdiObj = GetGuiResources(proc.Handle, 0);    // GR_GDIOBJECTS
                    diag += " USERObj=" + userObj + " GDIObj=" + gdiObj;
                }
            }
            catch { }

            Log("UI THREAD EXCEPTION:\n" + ex + "\n[diag] " + diag);

            // 所有 UI 线程异常都记录并弹框提示（完全透明，不再静默/免弹任何一类异常）
            try
            {
                MessageBox.Show("PCLock 出错：\n\n" + ex.Message +
                    "\n\n完整信息已写入 err.txt",
                    "PCLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Log("=== PCLock Main start ===");
            Store.SetLogSink(Log);   // Store（核心层）的日志经此 sink 落到 err.txt；测试构建不编译本文件，Store 自动静默
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

        /// <summary>加载程序图标（AppIcon.ico），缺失或失败时返回 null（调用方回退到默认图标）。</summary>
        internal static Icon LoadAppIcon()
        {
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppIcon.ico");
                if (File.Exists(p)) return new Icon(p);
            }
            catch (Exception ex)
            {
                Program.Log("LoadAppIcon 失败: " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 递归把窗体（含子容器）内标准按钮改为扁平样式。
        /// 原因：FlatStyle.Standard 的按钮在启用视觉样式时，OnPaint 会走
        /// ButtonRenderer.DrawParentBackground，它需要访问控件的 Handle；在窗口关闭/切换的
        /// 竞态下子按钮收到迟到的 WM_PAINT、父窗口已释放时，CreateHandle 会失败
        /// （Win32Exception 0x80004005"创建窗口句柄时出错"）。改扁平走经典绘制路径可根治。
        /// </summary>
        internal static void FlattenButtons(Control parent)
        {
            if (parent == null) return;
            try
            {
                foreach (Control c in parent.Controls)
                {
                    Button b = c as Button;
                    if (b != null)
                    {
                        try { b.FlatStyle = FlatStyle.Flat; } catch { }
                    }
                    if (c.HasChildren) FlattenButtons(c);
                }
            }
            catch { }
        }
    }
}
