using System.Globalization;
using Grpc.Core;
using Grpc.Core.Logging;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

            GrpcEnvironment.SetLogger(new ConsoleLogger());

            var webHostBuilder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(databaseActivator);
                    services.AddScoped(s => s.GetRequiredService<DatabaseActivator>().CreateConnection());
                })
                .ConfigureLogging(logging => logging.AddConsole())
                .UseKestrel()
                .UseStartup<WebStartup>()
                .UseUrls("http://+:" + this.WebPort.ToString(CultureInfo.InvariantCulture));

            using (var grpcServer = new GrpcAsheServer("0.0.0.0", this.AshePort, databaseActivator))
            using (var webHost = webHostBuilder.Build())
            {
                grpcServer.Start();
                Log.WriteMessage($"Listening {this.AshePort} for Ashe");

                webHost.Start();
                Log.WriteMessage($"Listening {this.WebPort} for Web");

                webHost.WaitForShutdown();
            }

            return 0;
        }
    }
}
