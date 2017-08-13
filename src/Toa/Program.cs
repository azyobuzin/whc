using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.CommandLineUtils;
using WagahighChoices.Toa.Utils;

namespace WagahighChoices.Toa
{
    public static class Program
    {
        public const int DefaultPort = 51203;

        public static int Main(string[] args)
        {
            var app = new CommandLineApplication()
            {
                FullName = "Toa",
                Description = "ワガママハイスペック ウィンドウ操作サービス",
            };

            var directoryOption = app.Option(
                "-d|--directory <dir>",
                "ワガママハイスペック.exe が存在するディレクトリ",
                CommandOptionType.SingleValue
            );

            var portOption = app.Option(
                "-p|--port <port>",
                "使用するポート番号",
                CommandOptionType.SingleValue
            );

            app.OnExecute(() =>
            {
                var port = portOption.HasValue() ? int.Parse(portOption.Value()) : DefaultPort;

                // Ctrl + C が押されたときの動作を設定しておく
                var w = new ManualResetEvent(false);
                var canceled = 0;
                Console.CancelKeyPress += (_, e) =>
                {
                    if (Interlocked.CompareExchange(ref canceled, 1, 0) == 0)
                    {
                        Log.WriteMessage("Terminating");
                        e.Cancel = true;
                        w.Set();
                    }
                };

                using (var server = ToaServer.Start(port))
                {
                    var process = directoryOption.HasValue()
                        ? StartWagahigh(directoryOption.Value())
                        : FindProcess();

                    if (process == null)
                    {
                        Log.WriteMessage("Could not find the process");
                        return 1;
                    }

                    Log.WriteMessage("Process ID: " + process.Process.Id);

                    using (var executor = new CommandExecutor(process))
                    {
                        server.SetExecutor(executor);
                        w.WaitOne();
                    }
                }

                return 0;
            });

            return app.Execute(args);
        }

        private static WagahighProcess StartWagahigh(string directory)
        {
            return WagahighProcess.StartAsync(directory).Result;
        }

        private static WagahighProcess FindProcess()
        {
            var processes = Process.GetProcessesByName("ワガママハイスペック");

            if (processes.Length == 0) return null;

            try
            {
                return WagahighProcess.FromProcess(processes[0].Id);
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
        }
    }
}
