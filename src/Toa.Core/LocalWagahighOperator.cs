using System;
using System.Diagnostics;
using System.IO;
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
            const string exeName = "ワガママハイスペック.exe";
            this._process = Process.Start("wine", "\"" + JoinPathInWindows(directory, exeName) + "\" -forcelog=clear");

            var logFilePath = Path.Combine(ToUnixPath(directory), "savedata", "krkr.console.log");

            // プロセス開始から 5 秒間はログファイルにアクセスさせない
            var allowedToAccessAt = DateTime.UtcNow.AddTicks(5 * TimeSpan.TicksPerSecond);

            var logObservable =
                Observable.Create<string>(async (observer, cancellationToken) =>
                {
                    var now = DateTime.UtcNow;
                    if (now < allowedToAccessAt)
                        await Task.Delay(allowedToAccessAt - now, cancellationToken).ConfigureAwait(false);

                    using (var reader = new LogFileReader(logFilePath))
                    {
                        reader.SeekToLastLine();

                        while (true)
                        {
                            while (reader.Read() is string log)
                                observer.OnNext(log);

                            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                        }
                    }
                })
                .Merge(
                    Observable.FromEventPattern(
                        x => this._process.Exited += x,
                        x => { if (this._process != null) this._process.Exited -= x; }
                    )
                    .SelectMany(_ => s_processExitedObservable)
                );

            this._logStream = Observable.Create<string>(observer => (this._process.HasExited ? s_processExitedObservable : logObservable).Subscribe(observer));
        }

        private static string JoinPathInWindows(string path1, string path2)
        {
            return string.IsNullOrEmpty(path1) ? path2
                : path1[path1.Length - 1] is var lastChar && (lastChar == '\\' || lastChar == '/') ? path1 + path2
                : path1 + "\\" + path2;
        }

        private static bool IsAbsolutePathInWindows(string path)
        {
            if (path.Length < 2 || path[1] != ':') return false;
            var driveLetter = path[0];
            if (!((driveLetter >= 'A' && driveLetter <= 'Z') || (driveLetter >= 'a' && driveLetter <= 'z'))) return false;
            return path.Length == 2 || path[2] == '\\' || path[2] == '/';
        }

        private static string ToUnixPath(string pathInWindows)
        {
            if (string.IsNullOrEmpty(pathInWindows)) return "";
            if (!IsAbsolutePathInWindows(pathInWindows)) return pathInWindows.Replace('\\', '/');

            var winePrefix = Environment.GetEnvironmentVariable("WINEPREFIX");
            if (string.IsNullOrEmpty(winePrefix))
                winePrefix = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wine");

            var driveLetter = char.ToLowerInvariant(pathInWindows[0]);
            var driveRoot = Path.Combine(winePrefix, "dosdevices", driveLetter + ":");

            return pathInWindows.Length >= 4
                ? Path.Combine(driveRoot, pathInWindows.Substring(3).Replace('\\', '/'))
                : driveRoot;
        }

        private async Task Connect(DisplayIdentifier displayIdentifier)
        {
            this._x11Client = await X11Client.ConnectAsync(displayIdentifier.Host, displayIdentifier.Display).ConfigureAwait(false);

            var s = this._x11Client.Screens[displayIdentifier.Screen];
            this._screenRootWindow = s.Root;

            while (true) // ウィンドウが見つかるまでループ
            {
                await Task.Delay(1000).ConfigureAwait(false);

                var wagahighWindow = await FindWagahighWindow(this._x11Client, s.Root).ConfigureAwait(false);
                if (!wagahighWindow.HasValue) continue;

                var contentWindow = await FindContentWindow(this._x11Client, wagahighWindow.Value).ConfigureAwait(false);
                if (!contentWindow.HasValue) continue;

                this._contentWindow = contentWindow.Value;
                await this._x11Client.ConfigureWindowAsync(wagahighWindow.Value, x: 0, y: 0).ConfigureAwait(false);
                return;
            }
        }

        private static async Task<uint?> FindWagahighWindow(X11Client x11Client, uint root)
        {
            var windowNameAtom = await x11Client.InternAtomAsync("_NET_WM_NAME", false).ConfigureAwait(false);

            foreach (var child in (await x11Client.QueryTreeAsync(root).ConfigureAwait(false)).Children)
            {
                var netWmName = await x11Client.GetStringPropertyAsync(child, windowNameAtom).ConfigureAwait(false);
                if (netWmName == "ワガママハイスペック") return child;
            }

            return null;
        }

        private static async Task<uint?> FindContentWindow(X11Client x11Client, uint wagahighWindow)
        {
            var children = (await x11Client.QueryTreeAsync(wagahighWindow).ConfigureAwait(false)).Children;
            var count = children.Count;

            if (count == 0) return null;

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

        public override async Task<Bgra32Image> CaptureContentAsync()
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

        public override async Task<Bgra32Image> GetCursorImageAsync()
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
