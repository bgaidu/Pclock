using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PCLock
{
    /// <summary>
    /// 系统屏保集成：把本程序注册为 Windows 屏保（.scr），并响应屏保协议参数。
    /// - 注册：复制自身为同目录 PCLock.scr，写入 HKCU\Control Panel\Desktop 的 SCRNSAVE.EXE 等。
    /// - /s：全屏锁屏（解锁即退出进程，符合屏保行为）。
    /// - /c：打开家长设置（无常驻实例时提示）。
    /// - /p：预览（极简窗体，嵌入系统预览窗口）。
    /// </summary>
    static class ScreenSaver
    {
        const string DesktopKey = "Control Panel\\Desktop";
        const int DefaultTimeoutSec = 60;   // 空闲多少秒后启动屏保

        /// <summary>.scr 目标路径：与当前 exe 同目录的 PCLock.scr</summary>
        public static string ScrPath
        {
            get
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
                return Path.Combine(dir, "PCLock.scr");
            }
        }

        public static bool IsRegistered()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(DesktopKey))
                {
                    if (k == null) return false;
                    string s = k.GetValue("SCRNSAVE.EXE") as string;
                    if (string.IsNullOrEmpty(s) || !s.Equals(ScrPath, StringComparison.OrdinalIgnoreCase))
                        return false;
                    return (k.GetValue("ScreenSaveActive") as string ?? "0") == "1";
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>注册为系统屏保（复制为 .scr 并写入注册表）。返回是否成功。</summary>
        public static bool Register()
        {
            try
            {
                string src = Application.ExecutablePath;
                string dst = ScrPath;
                if (!src.Equals(dst, StringComparison.OrdinalIgnoreCase))
                    File.Copy(src, dst, true);
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(DesktopKey))
                {
                    k.SetValue("SCRNSAVE.EXE", dst);
                    k.SetValue("ScreenSaveActive", "1");
                    k.SetValue("ScreenSaverIsSecure", "1");   // 安全屏保：不显示登录提示，直接锁屏
                    k.SetValue("ScreenSaveTimeOut", DefaultTimeoutSec.ToString());
                }
                return true;
            }
            catch (Exception ex)
            {
                Program.Log("ScreenSaver.Register failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>取消系统屏保注册（仅当我们是当前的屏保时才清理，避免误删别人的设置）。</summary>
        public static bool Unregister()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(DesktopKey))
                {
                    k.SetValue("ScreenSaveActive", "0");
                    string cur = k.GetValue("SCRNSAVE.EXE") as string;
                    if (!string.IsNullOrEmpty(cur) &&
                        cur.Equals(ScrPath, StringComparison.OrdinalIgnoreCase))
                        k.SetValue("SCRNSAVE.EXE", "");
                }
                try { if (File.Exists(ScrPath)) File.Delete(ScrPath); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                Program.Log("ScreenSaver.Unregister failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>/s 模式：全屏锁屏，解锁即退出（符合屏保进程语义）。</summary>
        public static void RunSaver()
        {
            LockForm lf = new LockForm(false);
            lf.UnlockRequested += (s, e) =>
            {
                lf.AllowClose = true;
                lf.Close();
            };
            Application.Run(lf);
        }

        /// <summary>/c 模式：配置。无常驻实例时提示用户回到主程序设置。</summary>
        public static void RunConfig()
        {
            if (App.Instance != null)
                Application.Run(new SettingsForm());
            else
                MessageBox.Show("请先启动电脑锁，在托盘图标的「家长设置」中配置屏保与各项参数。",
                    "电脑锁", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // === /p 预览：嵌入系统提供的预览窗口 ===
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct RECT { public int left, top, right, bottom; }

        /// <summary>/p 模式：在系统屏保设置的小预览窗内显示本程序标识。</summary>
        public static void RunPreview(string[] args)
        {
            IntPtr parent = IntPtr.Zero;
            long v = 0, v2 = 0;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.Length >= 2 && (a[0] == '/' || a[0] == '-') &&
                    (a[1] == 'p' || a[1] == 'P'))
                {
                    string rest = a.Substring(2).TrimStart(':');
                    if (rest.Length > 0)
                    {
                        if (long.TryParse(rest, out v)) parent = (IntPtr)v;
                    }
                    else if (i + 1 < args.Length && long.TryParse(args[i + 1], out v2))
                        parent = (IntPtr)v2;
                }
            }

            Form f = new Form();
            f.FormBorderStyle = FormBorderStyle.None;
            f.BackColor = Color.FromArgb(12, 18, 32);
            f.ShowInTaskbar = false;
            Label lbl = new Label();
            lbl.Text = "PCLock";
            lbl.ForeColor = Color.Silver;
            lbl.AutoSize = true;
            f.Controls.Add(lbl);
            if (parent != IntPtr.Zero)
            {
                f.Load += (s, e) =>
                {
                    try
                    {
                        SetParent(f.Handle, parent);
                        RECT r;
                        if (GetClientRect(parent, out r))
                            f.SetBounds(0, 0, r.right - r.left, r.bottom - r.top);
                    }
                    catch { }
                };
            }
            Application.Run(f);
        }
    }
}
