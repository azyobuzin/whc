using System;
using System.Threading;
using Grpc.Core;
using Grpc.Core.Logging;
using McMaster.Extensions.CommandLineUtils;
using WagahighChoices.Ashe;
using WagahighChoices.Kaoruko.GrpcServer;
using WagahighChoices.Utils;

namespace WagahighChoices.Kaoruko
{
    [Command(Name = "kaoruko", FullName = "Kaoruko", Description = "探索状況管理サーバー")]
    public class Program
    {
        public static int Main(string[] args)
        {
            return CommandLineApplication.Execute<Program>(args);
        }

        [Option("--ashe-port <port>", Description = "Ashe からの接続を受け入れるポート番号（デフォルト: 30222）")]
        public int AshePort { get; set; } = GrpcAsheServerContract.DefaultPort;

        [Option("--web-port <port>", Description = "管理 Web のポート番号（デフォルト: 30416）")]
        public int WebPort { get; set; } = 30416;

        [Option("--db <path>", Description = "データベースのパス（デフォルト: ./kaoruko.sqlite3）")]
        public string DatabasePath { get; set; } = "./kaoruko.sqlite3";

        private int OnExecute()
        {
            var databaseActivator = new DatabaseActivator(this.DatabasePath);
            databaseActivator.Initialize();

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

            using (var server = new GrpcAsheServer("0.0.0.0", this.AshePort, databaseActivator))
            {
                server.Start();
                Log.WriteMessage($"Listening {this.AshePort} for Ashe");
                w.WaitOne();
            }

            return 0;
        }
    }
}
