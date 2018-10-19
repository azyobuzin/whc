using System;
using System.Buffers;
using System.Drawing;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using WagahighChoices.Toa;

namespace WagahighChoices.Ashe
{
    internal class SearchAgent : IDisposable
    {
        private static readonly Random s_random = new Random();

        private readonly Logger _logger;
        private readonly WagahighOperator _wagahighOperator;
        private readonly SearchDirector _searchDirector;
        private readonly CancellationToken _cancellationToken;
        private IObservable<string> _publishedWagahighLog;
        private readonly SingleAssignmentDisposable _publishedWagahighLogDisposable = new SingleAssignmentDisposable();

        public SearchAgent(Logger logger, WagahighOperator wagahighOperator, SearchDirector searchDirector, CancellationToken cancellationToken)
        {
            this._logger = logger;
            this._wagahighOperator = wagahighOperator;
            this._searchDirector = searchDirector;
            this._cancellationToken = cancellationToken;
        }

        public void Dispose()
        {
            void TryDispose(Action dispose)
            {
                try
                {
                    dispose();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[{0}] Dispose Error: " + ex);
                }
            }

            TryDispose(this._publishedWagahighLogDisposable.Dispose);
            TryDispose(this._wagahighOperator.Dispose);
            TryDispose(this._searchDirector.Dispose);
        }

        public async Task RunAsync()
        {
            // ログを publish
            var connectableWagahighLog = this._wagahighOperator.LogStream.Publish();
            this._publishedWagahighLogDisposable.Disposable = connectableWagahighLog.Connect();
            this._publishedWagahighLog = connectableWagahighLog;

            await this.StartGameAndQuickSaveAsync().ConfigureAwait(false);

            while (true)
            {
                var result = await this._searchDirector.SeekDirectionAsync().ConfigureAwait(false);

                switch (result.Kind)
                {
                    case SeekDirectionResultKind.Ok:
                        await this.ProcessJobAsync(result.JobId, result.Actions).ConfigureAwait(false);
                        break;
                    case SeekDirectionResultKind.Finished:
                        this._logger.Info("終了通知を受け取りました。");
                        return;
                    default:
                        // 5 ～ 10 秒後にリトライ
                        var waitSecs = 5.0 + s_random.NextDouble() * 5.0;
                        await Task.Delay(TimeSpan.FromSeconds(waitSecs), this._cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
        }

        /// <summary>
        /// 比(0.0-1.0)で表された位置にカーソルを移動
        /// </summary>
        private async Task MoveCursorAsync(PointF point)
        {
            var contentSize = await this._wagahighOperator.GetContentSizeAsync().ConfigureAwait(false);

            await this._wagahighOperator.SetCursorPositionAsync(
                (short)(contentSize.Width * point.X),
                (short)(contentSize.Height * point.Y)
            ).ConfigureAwait(false);
        }

        private async Task<bool> CheckCursorIsHandAsync()
        {
            var cursorImage = await this._wagahighOperator.GetCursorImageAsync().ConfigureAwait(false);
            return CursorGlyph.Hand2.IsMatch(cursorImage);
        }

        /// <summary>
        /// 次の選択肢に進む
        /// </summary>
        private async Task SkipAsync()
        {
            this._logger.Info("スキップ開始");

            while (true)
            {
                await this.MoveCursorAsync(CursorPosition.GoToNextSelection).ConfigureAwait(false);
                await Task.Delay(200, this._cancellationToken).ConfigureAwait(false);

                if (await this.CheckCursorIsHandAsync().ConfigureAwait(false)) break;

                await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);
            }

            await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);
            await Task.Delay(1000, this._cancellationToken).ConfigureAwait(false);

            while (true)
            {
                await this.MoveCursorAsync(CursorPosition.Yes).ConfigureAwait(false);
                await Task.Delay(200, this._cancellationToken).ConfigureAwait(false);

                if (await this.CheckCursorIsHandAsync().ConfigureAwait(false)) break;

                await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);
            }

            var skipLogTask = this._publishedWagahighLog
                .FirstAsync(log => log.Contains("スキップにかかった時間", StringComparison.Ordinal))
                .ToTask(this._cancellationToken);

            await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);

            await skipLogTask.ConfigureAwait(false);

            this._logger.Info("スキップ完了");
        }

        /// <summary>
        /// ゲームをはじめから開始し、最初の選択肢でクイックセーブする
        /// </summary>
        private async Task StartGameAndQuickSaveAsync()
        {
            this._logger.Info("タイトル画面を待っています。");

            // TODO: いくら待っても hand2 にならないときにログを吐く
            while (true)
            {
                // カーソルを「はじめから」ボタンへ
                await this.MoveCursorAsync(CursorPosition.NewGame).ConfigureAwait(false);

                await Task.Delay(200, this._cancellationToken).ConfigureAwait(false);

                // カーソルが hand2 になったらクリック可能
                if (await this.CheckCursorIsHandAsync().ConfigureAwait(false)) break;

                await Task.Delay(2000, this._cancellationToken).ConfigureAwait(false);
            }

            this._logger.Info("ゲームをはじめから開始します。");

            // 「はじめから」をクリック
            await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);

            await Task.Delay(2000, this._cancellationToken).ConfigureAwait(false);

            // 最初の選択肢にジャンプ
            await this.SkipAsync().ConfigureAwait(false);

            // クイックセーブ
            while (true)
            {
                await this.MoveCursorAsync(CursorPosition.QuickSave).ConfigureAwait(false);
                await Task.Delay(200, this._cancellationToken).ConfigureAwait(false);

                if (await this.CheckCursorIsHandAsync().ConfigureAwait(false)) break;

                await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);
            }

            await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);
            await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);

            this._logger.Info("クイックセーブしました。");

            // カーソルを安全な位置に移動して終了
            await this.MoveCursorAsync(CursorPosition.Neutral).ConfigureAwait(false);
            await Task.Delay(200, this._cancellationToken).ConfigureAwait(false);
        }

        private async Task ProcessJobAsync(Guid jobId, ChoiceAction[] actions)
        {
            this._logger.Info($"ジョブ {jobId} を開始");

            // 最初の選択肢の情報を取得
            // TODO: GetMostSimilarSelectionAsync

            // TODO: このメソッドを抜けるときは、必ずクイックロード済み
        }

        /// <summary>
        /// スクリーンショットを撮影し、最も近い選択肢画面を返す
        /// </summary>
        private async Task<(SelectionInfo, int)> GetMostSimilarSelectionAsync()
        {
            // ハミング距離の閾値
            // 各々の最短距離より十分に小さいので、距離がこれ以下のものがあれば、それを返す
            const int threshold = 10;

            var arrayPool = ArrayPool<byte>.Shared;
            var hash = arrayPool.Rent(32);
            try
            {
                using (var screenshot = await this._wagahighOperator.CaptureContentAsync().ConfigureAwait(false))
                {
                    Blockhash.ComputeHash(new Bgr2432InputImage(screenshot), hash);
                }

                foreach (var si in SelectionInfo.Selections)
                {
                    var distance = Blockhash.GetDistance(hash, si.ScreenshotHash);
                    if (distance <= threshold) return (si, distance);
                }
            }
            finally
            {
                arrayPool.Return(hash);
            }

            return (null, 0);
        }
    }
}
