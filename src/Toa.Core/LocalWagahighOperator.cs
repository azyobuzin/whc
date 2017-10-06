using System;
using System.Threading.Tasks;
using WagahighChoices.Toa.X11;

namespace WagahighChoices.Toa
{
    public class LocalWagahighOperator : WagahighOperator
    {
        private X11Client _x11Client;
        private uint _screenRootWindow;
        private uint _wagahighWindow;
        private short _contentX;
        private short _contentY;
        private ushort _contentWidth;
        private ushort _contentHeight;

        private LocalWagahighOperator() { }

        public static async Task<LocalWagahighOperator> ConnectAsync(DisplayIdentifier displayIdentifier)
        {
            var x11Client = await X11Client.ConnectAsync(displayIdentifier.Host, displayIdentifier.Display).ConfigureAwait(false);

            try
            {
                var s = x11Client.Screens[displayIdentifier.Screen];
                var wagahighWindow = await FindWagahighWindow(x11Client, s.Root).ConfigureAwait(false);

                await x11Client.ConfigureWindowAsync(wagahighWindow, x: 0, y: 0).ConfigureAwait(false);

                var contentWindow = await FindContentWindow(x11Client, wagahighWindow).ConfigureAwait(false);
                var contentPoint = await x11Client.TranslateCoordinatesAsync(contentWindow.window, s.Root, 0, 0).ConfigureAwait(false);

                return new LocalWagahighOperator()
                {
                    _x11Client = x11Client,
                    _screenRootWindow = s.Root,
                    _wagahighWindow = wagahighWindow,
                    _contentX = contentPoint.DstX,
                    _contentY = contentPoint.DstY,
                    _contentWidth = contentWindow.width,
                    _contentHeight = contentWindow.height,
                };
            }
            catch
            {
                x11Client.Dispose();
                throw;
            }
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

        private static async Task<(uint window, ushort width, ushort height)> FindContentWindow(X11Client x11Client, uint wagahighWindow)
        {
            var children = (await x11Client.QueryTreeAsync(wagahighWindow).ConfigureAwait(false)).Children;
            var count = children.Count;

            if (count == 0) throw new Exception("子ウィンドウがありません。");

            var geometries = new GetGeometryResult[count];
            var maxArea = 0;
            var maxIndex = 0;

            for (var i = 0; i < count; i++)
            {
                var g = await x11Client.GetGeometryAsync(children[i]).ConfigureAwait(false);
                geometries[i] = g;

                var area = g.Width * g.Height;
                if (area > maxArea)
                {
                    maxArea = area;
                    maxIndex = i;
                }
            }

            return (children[maxIndex], geometries[maxIndex].Width, geometries[maxIndex].Height);
        }

        public override async Task<Argb32Image> CaptureContentAsync()
        {
            // TODO: GetGeometry しなおすべきでは？

            var res = await this._x11Client.GetImageAsync(
                this._screenRootWindow, this._contentX, this._contentY,
                this._contentWidth, this._contentHeight, uint.MaxValue, GetImageFormat.ZPixmap
            ).ConfigureAwait(false);

            if (res.Depth != 24 && res.Depth != 32)
                throw new Exception("非対応の画像形式です。");

            return new GetImageResultImage(this._contentWidth, this._contentHeight, res);
        }

        public override Task SetCursorPositionAsync(int x, int y)
        {
            checked
            {
                return this._x11Client.XTest.FakeInputAsync(XTestFakeEventType.MotionNotify, 0, 0,
                    this._screenRootWindow, (short)(this._contentX + x), (short)(this._contentY + y));
            }
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

        protected override void Dispose(bool disposing)
        {
            if (!this.IsDisposed && disposing)
                this._x11Client.Dispose();
        }
    }
}
