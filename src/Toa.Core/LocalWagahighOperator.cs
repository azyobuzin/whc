using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WagahighChoices.Toa.X11;

namespace WagahighChoices.Toa
{
    public class LocalWagahighOperator : WagahighOperator
    {
        private static readonly IObservable<string> s_processExitedObservable = Observable.Throw<string>(new Exception("プロセスが終了しました。"));

        private X11Client _x11Client;
        private uint _screenRootWindow;
        private uint _contentWindow;
        private Process _process;
        private IObservable<string> _logStream;

        private LocalWagahighOperator() { }

        public static async Task<LocalWagahighOperator> StartProcessAsync(string directory, DisplayIdentifier displayIdentifier)
        {
            var instance = new LocalWagahighOperator();

            try
            {
                instance.StartProcess(directory);
                await Task.Delay(5000).ConfigureAwait(false);
                await instance.Connect(displayIdentifier).ConfigureAwait(false);
            }
            catch
            {
                instance.Dispose();
                throw;
            }

            return instance;
        }

        private void StartProcess(string directory)
        {
            this._process = Process.Start("wine", "\"" + Path.Combine(directory, "ワガママハイスペック.exe") + "\" -forcelog=clear");

            var logObservable =
                Observable.Defer(() =>
                {
                    var reader = new LogFileReader(Path.Combine(directory, "savedata", "krkr.console.log"));
                    reader.SeekToLastLine();
                    return Observable.Return(reader);
                })
                .SelectMany(reader =>
                    Observable.Interval(new TimeSpan(500 * TimeSpan.TicksPerMillisecond))
                        .Select(_ => reader.Read())
                        .Where(x => x != null)
                        .Finally(reader.Dispose)
                )
                .DelaySubscription(DateTimeOffset.Now.AddTicks(5 * TimeSpan.TicksPerSecond))
                .Merge(
                    Observable.FromEventPattern(x => this._process.Exited += x, x => this._process.Exited -= x)
                        .SelectMany(_ => s_processExitedObservable)
                );

            this._logStream = Observable.Create<string>(observer => (this._process.HasExited ? s_processExitedObservable : logObservable).Subscribe(observer));
        }

        private async Task Connect(DisplayIdentifier displayIdentifier)
        {
            this._x11Client = await X11Client.ConnectAsync(displayIdentifier.Host, displayIdentifier.Display).ConfigureAwait(false);

            var s = this._x11Client.Screens[displayIdentifier.Screen];
            this._screenRootWindow = s.Root;

            var wagahighWindow = await FindWagahighWindow(this._x11Client, s.Root).ConfigureAwait(false);
            await this._x11Client.ConfigureWindowAsync(wagahighWindow, x: 0, y: 0).ConfigureAwait(false);

            this._contentWindow = await FindContentWindow(this._x11Client, wagahighWindow).ConfigureAwait(false);
        }

        private static async Task<uint> FindWagahighWindow(X11Client x11Client, uint root)
        {
            var windowNameAtom = await x11Client.InternAtomAsync("_NET_WM_NAME", false).ConfigureAwait(false);

            foreach (var child in (await x11Client.QueryTreeAsync(root).ConfigureAwait(false)).Children)
            {
                var netWmName = await x11Client.GetStringPropertyAsync(child, windowNameAtom).ConfigureAwait(false);
                if (netWmName == "ワガママハイスペック") return child;
            }

            throw new Exception("ウィンドウが見つかりませんでした。");
        }

        private static async Task<uint> FindContentWindow(X11Client x11Client, uint wagahighWindow)
        {
            var children = (await x11Client.QueryTreeAsync(wagahighWindow).ConfigureAwait(false)).Children;
            var count = children.Count;

            if (count == 0) throw new Exception("子ウィンドウがありません。");

            var maxArea = 0;
            var maxIndex = 0;

            for (var i = 0; i < count; i++)
            {
                var g = await x11Client.GetGeometryAsync(children[i]).ConfigureAwait(false);

                var area = g.Width * g.Height;
                if (area > maxArea)
                {
                    maxArea = area;
                    maxIndex = i;
                }
            }

            return children[maxIndex];
        }

        public override async Task<Argb32Image> CaptureContentAsync()
        {
            var contentGeometry = await this._x11Client.GetGeometryAsync(this._contentWindow).ConfigureAwait(false);
            var contentPoint = await this._x11Client.TranslateCoordinatesAsync(
                this._contentWindow, this._screenRootWindow, 0, 0).ConfigureAwait(false);

            var res = await this._x11Client.GetImageAsync(
                this._screenRootWindow, contentPoint.DstX, contentPoint.DstY,
                contentGeometry.Width, contentGeometry.Height, uint.MaxValue, GetImageFormat.ZPixmap
            ).ConfigureAwait(false);

            if (res.Depth != 24 && res.Depth != 32)
                throw new Exception("非対応の画像形式です。");

            return new GetImageResultImage(contentGeometry.Width, contentGeometry.Height, res);
        }

        public override async Task SetCursorPositionAsync(short x, short y)
        {
            var point = await this._x11Client.TranslateCoordinatesAsync(this._contentWindow, this._screenRootWindow, x, y).ConfigureAwait(false);
            await this._x11Client.XTest.FakeInputAsync(XTestFakeEventType.MotionNotify, 0, 0, this._screenRootWindow, point.DstX, point.DstY).ConfigureAwait(false);
        }

        public override async Task MouseClickAsync()
        {
            await this._x11Client.XTest.FakeInputAsync(
                XTestFakeEventType.ButtonPress,
                1, 0, this._screenRootWindow, 0, 0
            ).ConfigureAwait(false);

            await this._x11Client.XTest.FakeInputAsync(
                XTestFakeEventType.ButtonRelease,
                1, 0, this._screenRootWindow, 0, 0
            ).ConfigureAwait(false);
        }

        public override async Task<Argb32Image> GetCursorImageAsync()
        {
            var res = await this._x11Client.XFixes.GetCursorImageAsync().ConfigureAwait(false);
            return new GetCursorImageResultImage(res);
        }

        public override IObservable<string> LogStream => this._logStream;

        protected override void Dispose(bool disposing)
        {
            if (!this.IsDisposed && disposing)
            {
                this._x11Client?.Dispose();

                if (this._process != null)
                {
                    if (!this._process.HasExited)
                        this._process.Kill();
                    this._process.Dispose();
                    this._process = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
