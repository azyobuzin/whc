using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WagahighChoices.Toa.Windows
{
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    public enum GetWindowCmd : uint
    {
        GW_HWNDFIRST,
        GW_HWNDLAST,
        GW_HWNDNEXT,
        GW_HWNDPREV,
        GW_OWNER,
        GW_CHILD,
        GW_ENABLEDPOPUP
    }

    public class BorrowedDCHandle : DCHandle
    {
        public IntPtr Hwnd { get; }

        public BorrowedDCHandle(IntPtr hwnd, IntPtr hdc)
        {
            this.Hwnd = hwnd;
            this.SetHandle(hdc);
        }

        protected override bool ReleaseHandle()
        {
            return User32.ReleaseDC(this.Hwnd, this.handle) == 1;
        }
    }

    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static class User32
    {
        public const string DllName = "user32";

        [DllImport(DllName, EntryPoint = "EnumWindows", ExactSpelling = true, SetLastError = true)]
        private static extern bool _EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        public static bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam)
        {
            var result = _EnumWindows(lpEnumFunc, lParam);
            if (!result)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 0) throw new Win32Exception();
            }
            return result;
        }

        [DllImport(DllName, EntryPoint = "GetClientRect", ExactSpelling = true, SetLastError = true)]
        private static extern bool _GetClientRect(IntPtr hWnd, out Rect lpRect);

        public static Rect GetClientRect(IntPtr hWnd)
        {
            return _GetClientRect(hWnd, out var rect)
                ? rect
                : throw new Win32Exception();
        }

        [DllImport(DllName, EntryPoint = "GetDC", ExactSpelling = true)]
        private static extern IntPtr _GetDC(IntPtr hWnd);

        public static BorrowedDCHandle GetDC(IntPtr hWnd)
        {
            var result = _GetDC(hWnd);
            if (result == IntPtr.Zero) throw new Exception();
            return new BorrowedDCHandle(hWnd, result);
        }

        [DllImport(DllName, EntryPoint = "GetWindow", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr _GetWindow(IntPtr hWnd, GetWindowCmd uCmd);

        public static IntPtr GetWindow(IntPtr hWnd, GetWindowCmd uCmd, bool throwIfNull = false)
        {
            var result = _GetWindow(hWnd, uCmd);
            if (result == IntPtr.Zero && throwIfNull)
                throw new Win32Exception();
            return result;
        }

        [DllImport(DllName, ExactSpelling = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport(DllName, ExactSpelling = true)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport(DllName, ExactSpelling = true)]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    }
}
