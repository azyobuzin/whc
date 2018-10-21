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
    [Command(Name = "toa", FullName = "Toa", Description = "ワガママハイスペック ウィンドウ操作サービス")]
    public class Program
    {
        public static int Main(string[] args)
        {
            return CommandLineApplication.Execute<Program>(args);
        }

        [Option("-d|--directory <dir>", Description = "ワガママハイスペック.exe が存在するディレクトリ")]
        public string Directory { get; set; }

        [Option("-p|--port <port>", Description = "使用するポート番号（デフォルト: 51203）")]
        public int Port { get; set; } = GrpcToaServer.DefaultPort;

        private void OnExecute()
        {
            var display = DisplayIdentifier.Parse(Environment.GetEnvironmentVariable("DISPLAY"));

            // Ctrl + C が押されたときの動作を設定しておく
            var w = new ManualResetEvent(false);
            var cts = new CancellationTokenSource();
            cts.Token.Register(() =>
            {
                Log.WriteMessage("Terminating");
                w.Set();
            });
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            GrpcEnvironment.SetLogger(new ConsoleLogger());

            // サーバー開始
            // 0.0.0.0 を指定: https://github.com/grpc/grpc/issues/10570
            using (var wagahighOperator = LocalWagahighOperator.StartProcessAsync(this.Directory ?? "", display).Result)
            using (var server = new GrpcToaServer("0.0.0.0", this.Port, wagahighOperator))
            {
                server.Start();
                Log.WriteMessage("Listening " + this.Port);
                w.WaitOne();
            }
        }
    }
}
