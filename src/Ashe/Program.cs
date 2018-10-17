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

        private Logger Logger { get; } = new Logger();

        private async Task<int> OnExecuteAsync()
        {
            var wagahighOperator = await this.CreateWagahighOperator().ConfigureAwait(false);

            SearchDirector searchDirector;
            try
            {
                searchDirector = new ConsoleSearchDirector();
                await this.Logger.SetSearchDirectorAsync(searchDirector).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[{0}] Error: {1}", DateTime.Now, ex);
                wagahighOperator.Dispose();
                return 1;
            }

            using (searchDirector)
            using (wagahighOperator)
            {
                await this.StartGameAndQuickSave(wagahighOperator).ConfigureAwait(false);
            }

            return 0;
        }

        private async Task<WagahighOperator> CreateWagahighOperator()
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

        /// <summary>
        /// ゲームをはじめから開始し、最初の選択肢でクイックセーブする
        /// </summary>
        private async Task StartGameAndQuickSave(WagahighOperator wagahighOperator)
        {
            this.Logger.Info("タイトル画面を待っています。");

            while (true)
            {
                // カーソルを「はじめから」ボタンへ
                var contentSize = await wagahighOperator.GetContentSizeAsync().ConfigureAwait(false);
                await wagahighOperator.SetCursorPositionAsync(
                    (short)(contentSize.Width * CursorPosition.NewGame.X),
                    (short)(contentSize.Height * CursorPosition.NewGame.Y)
                ).ConfigureAwait(false);

                await Task.Delay(200).ConfigureAwait(false);

                // カーソルが hand2 になったらクリック可能
                var cursorImage = await wagahighOperator.GetCursorImageAsync().ConfigureAwait(false);
                if (CursorGlyph.Hand2.IsMatch(cursorImage)) break;

                await Task.Delay(2000).ConfigureAwait(false);
            }

            this.Logger.Info("ゲームをはじめから開始します。");

            // 「はじめから」をクリック
            await wagahighOperator.MouseClickAsync().ConfigureAwait(false);

            await Task.Delay(int.MaxValue).ConfigureAwait(false); // TODO
        }
    }
}
