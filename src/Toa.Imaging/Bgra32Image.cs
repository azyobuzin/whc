using System;

namespace WagahighChoices.Toa.Imaging
{
    public abstract class Bgra32Image : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public abstract ArraySegment<byte> Data { get; }

        protected Bgra32Image(int width, int height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));

            this.Width = width;
            this.Height = height;
        }

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
