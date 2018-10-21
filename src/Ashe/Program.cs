using System;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using WagahighChoices.Toa;
using WagahighChoices.Toa.Grpc;
using WagahighChoices.Toa.X11;

namespace WagahighChoices.Ashe
{
    [Command(Name = "ashe", FullName = "Ashe", Description = "探索ワーカー")]
    public class Program
    {
        public static int Main(string[] args)
        {
            return CommandLineApplication.Execute<Program>(args);
        }

        [Option("--kaoruko-host <host>", Description = "接続する Kaoruko のホスト名。指定しない場合はコンソールから指示を受け付けます")]
        public string KaorukoHost { get; set; }

        [Option("--kaoruko-port <port>", Description = "接続する Kaoruko のポート番号（デフォルト: 50222）")]
        public int KaorukoPort { get; set; } = GrpcAsheServerContract.DefaultPort;

        [Option("--toa-host <host>", Description = "接続する Toa のホスト名。指定しない場合はワガママハイスペックを起動します")]
        public string ToaHost { get; set; }

        [Option("--toa-port <port>", Description = "接続する Toa のポート番号（デフォルト: 51203）")]
        public int ToaPort { get; set; } = GrpcToaServer.DefaultPort;

        [Option("-d|--directory <dir>", Description = "ワガママハイスペック.exe が存在するディレクトリ（Toa を使用しない場合）")]
        public string Directory { get; set; }

        private Logger Logger { get; } = new Logger();

        private async Task<int> OnExecuteAsync()
        {
            var wagahighOperator = await this.CreateWagahighOperatorAsync().ConfigureAwait(false);

            SearchDirector searchDirector;
            try
            {
                searchDirector = await this.CreateSearchDirectorAsync().ConfigureAwait(false);
                await this.Logger.SetSearchDirectorAsync(searchDirector).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[{0}] Error: {1}", DateTime.Now, ex);
                wagahighOperator.Dispose();
                return 1;
            }

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            using (var agent = new SearchAgent(this.Logger, wagahighOperator, searchDirector, cts.Token))
            {
                try
                {
                    await agent.RunAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.Logger.Info("キャンセルされました。");
                }
            }

            return 0;
        }

        private async Task<WagahighOperator> CreateWagahighOperatorAsync()
        {
            if (this.ToaHost != null)
            {
                this.Logger.Info($"Toa サーバー {this.ToaHost}:{this.ToaPort} に接続します。");
                var remoteOperator = new GrpcRemoteWagahighOperator(this.ToaHost, this.ToaPort);
                await remoteOperator.ConnectAsync().ConfigureAwait(false);
                return remoteOperator;
            }
            else
            {
                this.Logger.Info("ワガママハイスペックを起動します。");
                var display = DisplayIdentifier.Parse(Environment.GetEnvironmentVariable("DISPLAY"));
                return await LocalWagahighOperator.StartProcessAsync(this.Directory ?? "", display).ConfigureAwait(false);
            }
        }

        private async Task<SearchDirector> CreateSearchDirectorAsync()
        {
            if (this.KaorukoHost != null)
            {
                this.Logger.Info($"Kaoruko サーバー {this.KaorukoHost}:{this.KaorukoPort} に接続します。");
                var remoteSearchDirector = new GrpcRemoteSearchDirector(this.KaorukoHost, this.KaorukoPort);
                await remoteSearchDirector.ConnectAsync().ConfigureAwait(false);
                return remoteSearchDirector;
            }

            return new ConsoleSearchDirector();
        }
    }
}
