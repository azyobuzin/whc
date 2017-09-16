using System;
using System.Buffers;

namespace WagahighChoices.Toa.X11
{
    public class GetImageResult : IDisposable
    {
        public byte Depth { get; }
        public VisualType Visual { get; }

        private byte[] _data;
        private readonly int _dataLength;
        public ArraySegment<byte> Data => new ArraySegment<byte>(this._data, 0, this._dataLength);

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
            if (this._data != null)
            {
                ArrayPool<byte>.Shared.Return(this._data);
                this._data = null;
            }
        }
    }

    public enum GetImageFormat : byte
    {
        XYPixmap = 1,
        ZPixmap = 2,
    }
}
