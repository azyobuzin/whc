﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WagahighChoices.Toa.X11;
using ImageSharp;

namespace WagahighChoices.Toa
{
    public class WagahighOperator
    {
        private X11Client _x11Client;
        private uint _screenRootWindow;
        private uint _wagahighWindow;
        private short _contentX;
        private short _contentY;
        private ushort _contentWidth;
        private ushort _contentHeight;

        private WagahighOperator() { }

        public static async Task<WagahighOperator> ConnectAsync(string host, int display, int screen)
        {
            var x11Client = await X11Client.ConnectAsync(host, display).ConfigureAwait(false);

            try
            {
                var s = x11Client.Screens[screen];
                var wagahighWindow = await FindWagahighWindow(x11Client, s.Root).ConfigureAwait(false);
                var contentWindow = await FindContentWindow(x11Client, wagahighWindow).ConfigureAwait(false);
                var contentPoint = await x11Client.TranslateCoordinatesAsync(contentWindow.window, s.Root, 0, 0).ConfigureAwait(false);

                return new WagahighOperator()
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

        public async Task<Image<Rgb2432>> CaptureContentAsync()
        {
            var res = await this._x11Client.GetImageAsync(
                this._screenRootWindow, this._contentX, this._contentY,
                this._contentWidth, this._contentHeight, uint.MaxValue, GetImageFormat.ZPixmap
            ).ConfigureAwait(false);

            if (res.Depth != 24 && res.Depth != 32)
                throw new Exception("非対応の画像形式です。");

            return Image.LoadPixelData<Rgb2432>(new Span<byte>(res.Data), this._contentWidth, this._contentHeight);
        }
    }
}