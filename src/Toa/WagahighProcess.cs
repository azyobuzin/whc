using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WagahighChoices.Toa.Utils;

namespace WagahighChoices.Toa
{
    public class WagahighProcess : IDisposable
    {
        public Process Process { get; private set; }
        public IntPtr WindowHandle { get; }
        private bool _killWhenDispose;

        public WagahighProcess(Process process, IntPtr windowHandle, bool killWhenDispose)
        {
            this.Process = process;
            this.WindowHandle = windowHandle;
            this._killWhenDispose = killWhenDispose;
        }

        public static async Task<WagahighProcess> StartAsync(string directory)
        {
            var p = Process.Start(
                new ProcessStartInfo(Path.Combine(directory, "ワガママハイスペック.exe"), "-forcelog=clear")
                {
                    WorkingDirectory = directory
                }
            );

            var mainWindow = IntPtr.Zero;

            for (var count = 0; count < 20; count++)
            {
                await Task.Delay(500).ConfigureAwait(false);
                mainWindow = NativeUtils.FindMainWindow(p.Id);
                if (mainWindow != IntPtr.Zero) break;
            }

            if (mainWindow == IntPtr.Zero)
            {
                if (!p.HasExited) p.Kill();
                p.Dispose();
                throw new TimeoutException("メインウィンドウを取得できませんでした。");
            }

            return new WagahighProcess(p, mainWindow, true);
        }

        public static WagahighProcess FromProcess(int processId)
        {
            var mainWindow = NativeUtils.FindMainWindow(processId);
            if (mainWindow == IntPtr.Zero)
                throw new Exception("メインウィンドウを取得できませんでした。");
            return new WagahighProcess(Process.GetProcessById(processId), mainWindow, false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.Process != null)
            {
                if (this._killWhenDispose && !this.Process.HasExited)
                    this.Process.Kill();

                if (disposing)
                    this.Process.Dispose();

                this.Process = null;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~WagahighProcess()
        {
            this.Dispose(false);
        }
    }
}
