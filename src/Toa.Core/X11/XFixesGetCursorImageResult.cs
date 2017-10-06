using System;
using System.Buffers;

namespace WagahighChoices.Toa.X11
{
    public class XFixesGetCursorImageResult : IDisposable
    {
        public short X { get; }
        public short Y { get; }
        public ushort Width { get; }
        public ushort Height { get; }
        public ushort XHot { get; }
        public ushort YHot { get; }
        public uint CursorSerial { get; }

        private byte[] _cursorImage;
        private readonly int _cursorImageLength;
        public ArraySegment<byte> CursorImage => new ArraySegment<byte>(this._cursorImage, 0, this._cursorImageLength);

        public XFixesGetCursorImageResult(short x, short y, ushort width, ushort height, ushort xHot, ushort yHot, uint cursorSerial, ReadOnlySpan<byte> cursorImage)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
            this.XHot = xHot;
            this.YHot = yHot;
            this.CursorSerial = cursorSerial;
            this._cursorImage = ArrayPool<byte>.Shared.Rent(cursorImage.Length);
            cursorImage.CopyTo(this._cursorImage);
            this._cursorImageLength = cursorImage.Length;
        }

        internal XFixesGetCursorImageResult(ref XFixes.GetCursorImageReply reply, ReadOnlySpan<byte> cursorImage)
        {
            this.X = reply.X;
            this.Y = reply.Y;
            this.Width = reply.Width;
            this.Height = reply.Height;
            this.XHot = reply.XHot;
            this.YHot = reply.YHot;
            this.CursorSerial = reply.CursorSerial;
            this._cursorImage = ArrayPool<byte>.Shared.Rent(cursorImage.Length);
            cursorImage.CopyTo(this._cursorImage);
            this._cursorImageLength = cursorImage.Length;
        }

        public void Dispose()
        {
            if (this._cursorImage != null)
            {
                ArrayPool<byte>.Shared.Return(this._cursorImage);
                this._cursorImage = null;
            }
        }
    }
}
