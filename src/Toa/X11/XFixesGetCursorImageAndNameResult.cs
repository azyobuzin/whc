using System;
using System.Buffers;

namespace WagahighChoices.Toa.X11
{
    public class XFixesGetCursorImageAndNameResult : IDisposable
    {
        public short X { get; }
        public short Y { get; }
        public ushort Width { get; }
        public ushort Height { get; }
        public ushort XHot { get; }
        public ushort YHot { get; }
        public uint CursorSerial { get; }
        public uint CursorAtom { get; }
        public string CursorName { get; }

        private readonly byte[] _cursorImage;
        private readonly int _cursorImageLength;
        public Span<byte> CursorImage => new Span<byte>(this._cursorImage, 0, this._cursorImageLength);

        private bool _disposed;

        public XFixesGetCursorImageAndNameResult(short x, short y, ushort width, ushort height, ushort xHot, ushort yHot, uint cursorSerial, uint cursorAtom, string cursorName, ReadOnlySpan<byte> cursorImage)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
            this.XHot = xHot;
            this.YHot = yHot;
            this.CursorSerial = cursorSerial;
            this.CursorAtom = cursorAtom;
            this.CursorName = cursorName;
            this._cursorImage = ArrayPool<byte>.Shared.Rent(cursorImage.Length);
            cursorImage.CopyTo(this._cursorImage);
            this._cursorImageLength = cursorImage.Length;
        }

        internal XFixesGetCursorImageAndNameResult(ref XFixes.GetCursorImageAndNameReply reply, string cursorName, ReadOnlySpan<byte> cursorImage)
        {
            this.X = reply.X;
            this.Y = reply.Y;
            this.Width = reply.Width;
            this.Height = reply.Height;
            this.XHot = reply.XHot;
            this.YHot = reply.YHot;
            this.CursorSerial = reply.CursorSerial;
            this.CursorAtom = reply.CursorAtom;
            this.CursorName = cursorName;
            this._cursorImage = ArrayPool<byte>.Shared.Rent(cursorImage.Length);
            cursorImage.CopyTo(this._cursorImage);
            this._cursorImageLength = cursorImage.Length;
        }

        public void Dispose()
        {
            if (this._disposed) return;
            this._disposed = true;
            ArrayPool<byte>.Shared.Return(this._cursorImage);
        }
    }
}
