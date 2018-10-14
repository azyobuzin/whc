using System;
using System.Threading;
using Grpc.Core;
using Grpc.Core.Logging;
using McMaster.Extensions.CommandLineUtils;
using WagahighChoices.Toa.Grpc;
using WagahighChoices.Toa.X11;
using WagahighChoices.Utils;

namespace WagahighChoices.Toa.Standalone
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication()
            {
                FullName = "Toa",
                Description = "ワガママハイスペック ウィンドウ操作サービス",
            };

            app.HelpOption("-?|-h|--help");

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
                var directory = directoryOption.Value() ?? "";
                var port = portOption.HasValue() ? int.Parse(portOption.Value()) : GrpcToaServer.DefaultPort;
                var display = DisplayIdentifier.Parse(Environment.GetEnvironmentVariable("DISPLAY"));

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

                GrpcEnvironment.SetLogger(new ConsoleLogger());

                // サーバー開始
                // 0.0.0.0 を指定: https://github.com/grpc/grpc/issues/10570
                using (var wagahighOperator = LocalWagahighOperator.StartProcessAsync(directory, display).Result)
                using (var server = new GrpcToaServer("0.0.0.0", port, wagahighOperator))
                {
                    server.Start();
                    Log.WriteMessage("Listening " + port);
                    w.WaitOne();
                }

                return 0;
            });

            return app.Execute(args);
        }
    }
}
