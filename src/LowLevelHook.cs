using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>低级键盘钩子：锁屏期间屏蔽 Win 键、Alt+Tab、Alt+Esc、Ctrl+Esc、Alt+方向键、Shift+Tab</summary>
    public class LowLevelHook : IDisposable
    {
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;
        const int WM_KEYUP = 0x0101;
        const int WM_SYSKEYDOWN = 0x0104;
        const int WM_SYSKEYUP = 0x0105;

        // KBDLLHOOKSTRUCT 结构体（Win2000+ 均支持，Win7 完全兼容）
        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        IntPtr hHook;
        HookProc proc;

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int nVirtKey);

        public LowLevelHook()
        {
            proc = HookCallback;
            try { hHook = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0); }
            catch (Exception) { hHook = IntPtr.Zero; }
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // 使用 KBDLLHOOKSTRUCT 获取按键信息（Win7 完全兼容）
                KBDLLHOOKSTRUCT kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                int vk = (int)kb.vkCode;
                bool isSysKey = (wParam == (IntPtr)WM_SYSKEYDOWN);
                bool isKeyDown = (wParam == (IntPtr)WM_KEYDOWN);

                // 通过 GetAsyncKeyState 检测修饰键状态（在钩子回调中调用是安全的）
                bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;
                bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
                bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;

                // Win 键 (LWin=0x5B, RWin=0x5C)
                if (vk == 0x5B || vk == 0x5C) return (IntPtr)1;
                // Alt+Tab：WM_SYSKEYDOWN 时 Tab 必然带 Alt，直接拦截
                if (isSysKey && vk == 0x09) return (IntPtr)1;
                // Shift+Tab：WM_KEYDOWN + Shift 按下
                if (isKeyDown && vk == 0x09 && shift) return (IntPtr)1;
                // Alt+Esc / Ctrl+Esc
                if (vk == 0x1B && (alt || ctrl)) return (IntPtr)1;
                // Alt+方向键
                if (alt && (vk >= 0x25 && vk <= 0x28)) return (IntPtr)1;
            }
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (hHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hHook);
                hHook = IntPtr.Zero;
            }
        }
    }
}
