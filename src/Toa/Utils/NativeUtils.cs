using System;
using WinApi.User32;

namespace WagahighChoices.Toa.Utils
{
    internal static class NativeUtils
    {
        // Process.MainWindowHandle は Mono で使えなさそうなので自前実装
        public static IntPtr FindMainWindow(int processId)
        {
            var result = IntPtr.Zero;

            User32Methods.EnumWindows(
                (hWnd, lParam) =>
                {
                    int pid;
                    unsafe
                    {
                        User32Methods.GetWindowThreadProcessId(hWnd, new IntPtr(&pid));
                    }

                    if (pid == processId &&
                        User32Methods.GetWindow(hWnd, (uint)GetWindowFlag.GW_OWNER) == IntPtr.Zero)
                    {
                        // オーナーウィンドウがない → 普通のウィンドウ
                        result = hWnd;
                        return false;
                    }

                    return true;
                },
                IntPtr.Zero
            );

            return result;
        }
    }
}
