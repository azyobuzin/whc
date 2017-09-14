using System;
using System.Buffers;

namespace WagahighChoices.Toa.X11
{
    public class GetImageResult : IDisposable
    {
        public byte Depth { get; }
        public VisualType Visual { get; }

        private readonly byte[] _data;
        private readonly int _dataLength;
        public Span<byte> Data => new Span<byte>(this._data, 0, this._dataLength);

        private bool _disposed;

        public GetImageResult(byte depth, VisualType visual, ReadOnlySpan<byte> data)
        {
            this.Depth = depth;
            this.Visual = visual;
            this._data = ArrayPool<byte>.Shared.Rent(data.Length);
            data.CopyTo(this._data);
            this._dataLength = data.Length;
        }

        public void Dispose()
        {
            if (this._disposed) return;
            this._disposed = true;
            ArrayPool<byte>.Shared.Return(this._data);
        }
    }

    public enum GetImageFormat : byte
    {
        XYPixmap = 1,
        ZPixmap = 2,
    }
}
