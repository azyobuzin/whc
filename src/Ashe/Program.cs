using System;
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
        public int KaorukoPort { get; set; }

        [Option("--toa-host <host>", Description = "接続する Toa のホスト名。指定しない場合はワガママハイスペックを起動します")]
        public string ToaHost { get; set; }

        [Option("--toa-port <port>", Description = "接続する Toa のポート番号（デフォルト: 51203）")]
        public int ToaPort { get; set; } = GrpcToaServer.DefaultPort;

        [Option("-d|--directory <dir>", Description = "ワガママハイスペック.exe が存在するディレクトリ（Toa を使用しない場合）")]
        public string Directory { get; set; }

        private async Task<int> OnExecuteAsync()
        {
            var logger = new Logger();

            WagahighOperator wagahighOperator;
            if (this.ToaHost != null)
            {
                logger.Info($"Toa サーバー {this.ToaHost}:{this.ToaPort} に接続します。");
                var remoteOperator = new GrpcRemoteWagahighOperator(this.ToaHost, this.ToaPort);
                await remoteOperator.ConnectAsync();
                wagahighOperator = remoteOperator;
            }
            else
            {
                logger.Info("ワガママハイスペックを起動します。");
                var display = DisplayIdentifier.Parse(Environment.GetEnvironmentVariable("DISPLAY"));
                wagahighOperator = await LocalWagahighOperator.StartProcessAsync(this.Directory ?? "", display);
            }

            using (wagahighOperator)
            {
                // TODO: 「はじめから」の位置にカーソルを置き、カーソルが hand2 になるまで待つ
                // TODO: 最初の選択肢まで移動し、クイックセーブする
            }

            return 0;
        }
    }
}
