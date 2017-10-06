using System;
using WagahighChoices.Toa.X11;

namespace WagahighChoices.Toa
{
    public abstract class Argb32Image : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public abstract ArraySegment<byte> Data { get; }

        protected bool IsDisposed { get; private set; }

        protected Argb32Image(int width, int height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));

            this.Width = width;
            this.Height = height;
        }

        protected virtual void Dispose(bool disposing)
        {
            this.IsDisposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    internal class GetImageResultImage : Argb32Image
    {
        private readonly GetImageResult _inner;

        public GetImageResultImage(int width, int height, GetImageResult inner)
            : base(width, height)
        {
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            if (inner.Data.Count != width * height * 4)
                throw new ArgumentException("The length of the data is not the expected value.");

            this._inner = inner;
        }

        public override ArraySegment<byte> Data => this._inner.Data;

        protected override void Dispose(bool disposing)
        {
            if (!this.IsDisposed && disposing)
                this._inner.Dispose();
        }
    }

    internal class GetCursorImageResultImage : Argb32Image
    {
        private readonly XFixesGetCursorImageResult _inner;

        public GetCursorImageResultImage(XFixesGetCursorImageResult inner)
            : base(inner.Width, inner.Height)
        {
            this._inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public override ArraySegment<byte> Data => this._inner.CursorImage;

        protected override void Dispose(bool disposing)
        {
            if (!this.IsDisposed && disposing)
                this._inner.Dispose();
        }
    }
}
