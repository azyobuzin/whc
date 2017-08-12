using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WagahighChoices.Toa.Utils;

namespace WagahighChoices.Toa
{
    // 改善案
    // WagahighProcess: ワガハイプロセスとハンドルを持ってるクラスがウィンドウ操作を担当
    // ToaServer: TCP サーバとして頑張る
    // Program: ToaServer の立ち上げ → WagahighProcess を作成 → ToaServer に WagahighProcess を注入

    public class Program : IDisposable
    {
        public const int DefaultPort = 51203;

        public static int Main(string[] args)
        {
            if (args.Length < 1) return 1;

            var wagahighDirectory = args[0];
            var port = args.Length >= 2 ? int.Parse(args[1]) : DefaultPort;

            using (var program = new Program(wagahighDirectory, port))
            {
                // TODO: TCPサーバ開始
                program.StartWagahighProcess();

                var w = new ManualResetEvent(false);
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    w.Set();
                };
                w.WaitOne();
            }

            return 0;
        }

        private readonly object _lockObj = new object();

        public string WagahighDirectory { get; }
        public int Port { get; }

        public Process WagahighProcess { get; private set; }
        public IntPtr MainWindowHandle { get; private set; }

        public Program(string wagahighDirectory, int port)
        {
            this.WagahighDirectory = wagahighDirectory;
            this.Port = port;
        }

        public void StartWagahighProcess()
        {
            this.WagahighProcess = Process.Start(
                new ProcessStartInfo(Path.Combine(this.WagahighDirectory.FullName, "ワガママハイスペック.exe"), "-forcelog=clear")
                {
                    WorkingDirectory = this.WagahighDirectory.FullName
                }
            );

            while (true)
            {
                Thread.Sleep(500);
                var mainWindow = NativeUtils.FindMainWindow(this.WagahighProcess.Id);
                if (mainWindow != IntPtr.Zero)
                {
                    this.MainWindowHandle = mainWindow;
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (this.WagahighProcess != null)
            {
                if (!this.WagahighProcess.HasExited)
                {
                    this.WagahighProcess.Kill();
                }

                this.WagahighProcess.Dispose();
                this.WagahighProcess = null;
            }
        }
    }
}
