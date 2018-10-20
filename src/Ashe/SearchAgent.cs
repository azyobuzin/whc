using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        /// <paramref name="point"/> に移動し、カーソルが変わるまで待つ
        /// </summary>
        private async Task MoveCursorToButtonAsync(PointF point)
        {
            while (true)
            {
                await this.MoveCursorAsync(point).ConfigureAwait(false);
                await Task.Delay(200, this._cancellationToken).ConfigureAwait(false);

                if (await this.CheckCursorIsHandAsync().ConfigureAwait(false)) break;

                await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 次の選択肢に進む
        /// </summary>
        private async Task SkipAsync()
        {
            this._logger.Info("スキップ開始");

            await this.MoveCursorToButtonAsync(CursorPosition.GoToNextSelection).ConfigureAwait(false);
            await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);
            await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);

            await this.MoveCursorToButtonAsync(CursorPosition.Yes).ConfigureAwait(false);

            // スキップ完了をログから検出
            var skipLogTask = this._publishedWagahighLog
                .FirstAsync(log => log.Contains("スキップにかかった時間", StringComparison.Ordinal))
                .ToTask(this._cancellationToken);

            // ムービー突入をログから検出
            var isMovie = false;
            using (this._publishedWagahighLog.Subscribe(log =>
            {
                if (log.Contains("video mode:", StringComparison.Ordinal))
                    isMovie = true;
            }))
            {
                // YES をクリック
                await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);
                await Task.Delay(100, this._cancellationToken).ConfigureAwait(false);

                // 安全地帯へ
                await this.MoveCursorAsync(CursorPosition.Neutral).ConfigureAwait(false);

                await skipLogTask.ConfigureAwait(false);
                await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);
            }

            this._logger.Info("スキップ完了");

            if (isMovie)
            {
                this._logger.Info("ムービーをスキップします。");

                // 画面をクリックして、ムービースキップ（どうせ Wine だとエラーで再生されないから必要ないけど）          
                await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);
                await Task.Delay(1000, this._cancellationToken);

                await this.SkipAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// ゲームをはじめから開始し、最初の選択肢でクイックセーブする
        /// </summary>
        private async Task StartGameAndQuickSaveAsync()
        {
            this._logger.Info("タイトル画面を待っています。");

            for (var retryCount = 1; ; retryCount++)
            {
                // カーソルを「はじめから」ボタンへ
                await this.MoveCursorAsync(CursorPosition.NewGame).ConfigureAwait(false);

                await Task.Delay(200, this._cancellationToken).ConfigureAwait(false);

                // カーソルが hand2 になったらクリック可能
                if (await this.CheckCursorIsHandAsync().ConfigureAwait(false)) break;

                if (retryCount % 5 == 0)
                {
                    this._logger.Error($"{retryCount} 回待機しましたが、タイトル画面になりません。");
                }

                await Task.Delay(2000, this._cancellationToken).ConfigureAwait(false);
            }

            this._logger.Info("ゲームをはじめから開始します。");

            // 「はじめから」をクリック
            await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);

            await Task.Delay(2000, this._cancellationToken).ConfigureAwait(false);

            // 最初の選択肢にジャンプ
            await this.SkipAsync().ConfigureAwait(false);

            // クイックセーブ
            await this.MoveCursorToButtonAsync(CursorPosition.QuickSave).ConfigureAwait(false);
            await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);
            await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);

            this._logger.Info("クイックセーブしました。");

            // カーソルを安全な位置に移動して終了
            await this.MoveCursorAsync(CursorPosition.Neutral).ConfigureAwait(false);
            await Task.Delay(200, this._cancellationToken).ConfigureAwait(false);
        }

        private async Task ProcessJobAsync(Guid jobId, IEnumerable<ChoiceAction> actions)
        {
            this._logger.Info($"ジョブ {jobId} を開始");

            var actionQueue = new Queue<ChoiceAction>(actions);
            var selections = new List<SelectionInfo>();
            Heroine heroine;
            var retryCount = 0;

            while (true)
            {
                var (selection, distance) = await this.GetMostSimilarSelectionAsync().ConfigureAwait(false);

                if (selection == null)
                {
                    retryCount++;
                    if (retryCount % 5 == 0)
                        this._logger.Error($"{retryCount} 回リトライしましたが、一致する選択肢画面が見つかりません。（最短距離: {distance}）");

                    await Task.Delay(1000, this._cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (selection is RouteSpecificSelectionInfo heroineSelection)
                {
                    heroine = heroineSelection.Heroine;
                    this._logger.Info($"{heroine} 個別ルートの選択肢に到達しました。（距離: {distance}）");
                    break;
                }

                this._logger.Info($"選択肢 {selection.Id} （距離: {distance}）");

                // キューから次の選択を取得
                // ネタ切れならば、上を選択
                if (!actionQueue.TryDequeue(out var action))
                    action = ChoiceAction.SelectUpper;

                this._logger.Info((action == ChoiceAction.SelectUpper ? "上" : "下") + " を選択します。");

                await this.MoveCursorAsync(action == ChoiceAction.SelectUpper ? CursorPosition.UpperChoice : CursorPosition.LowerChoice).ConfigureAwait(false);
                await Task.Delay(200, this._cancellationToken).ConfigureAwait(false);

                if (!await this.CheckCursorIsHandAsync().ConfigureAwait(false))
                {
                    this._logger.Info("カーソル画像が変化しないため、リトライします。");
                    await Task.Delay(1000, this._cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);

                retryCount = 0;
                selections.Add(selection);

                // 選択後はスキップを行う
                await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);
                await this.SkipAsync().ConfigureAwait(false);
            }

            // 結果を送信して、クイックロード
            await Task.WhenAll(
                this._searchDirector.ReportResultAsync(jobId, heroine, selections.Select(x => x.Id).ToArray()),
                this.QuickLoadAsync()
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// スクリーンショットを撮影し、最も近い選択肢画面を返す
        /// </summary>
        /// <returns>
        /// 閾値より距離の小さい選択肢画面が見つかった場合、その <see cref="SelectionInfo"/> と、距離を返します。
        /// 見つからなかった場合、 null と、見つかった中での最短距離を返します。
        /// </returns>
        private async Task<(SelectionInfo, int)> GetMostSimilarSelectionAsync()
        {
            // ハミング距離の閾値
            // 各々の最短距離より十分に小さいので、距離がこれ以下のものがあれば、それを返す
            const int threshold = 10;

            var arrayPool = ArrayPool<byte>.Shared;
            var hashArray = arrayPool.Rent(32);
            var hash = new ArraySegment<byte>(hashArray, 0, 32);

            var minDistance = int.MaxValue;

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
                    if (distance < minDistance) minDistance = distance;
                }
            }
            finally
            {
                arrayPool.Return(hashArray);
            }

            return (null, minDistance);
        }

        private async Task QuickLoadAsync()
        {
            await this.MoveCursorToButtonAsync(CursorPosition.QuickLoad).ConfigureAwait(false);
            await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);
            await Task.Delay(500, this._cancellationToken).ConfigureAwait(false);

            await this.MoveCursorToButtonAsync(CursorPosition.Yes).ConfigureAwait(false);
            await this._wagahighOperator.MouseClickAsync().ConfigureAwait(false);
            await Task.Delay(2000, this._cancellationToken).ConfigureAwait(false);

            this._logger.Info("クイックロードしました。");
        }
    }
}
