using System;
using WagahighChoices.Toa.Windows;

namespace WagahighChoices.Toa.Utils
{
    internal static class NativeUtils
    {
        // Process.MainWindowHandle は Mono で使えなさそうなので自前実装
        public static IntPtr FindMainWindow(int processId)
        {
            var result = IntPtr.Zero;

            User32.EnumWindows(
                (hWnd, lParam) =>
                {
                    User32.GetWindowThreadProcessId(hWnd, out var pid);

                    if (pid == processId &&
                        User32.GetWindow(hWnd, GetWindowCmd.GW_OWNER) == IntPtr.Zero)
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
