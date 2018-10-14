using System;
using System.Buffers;

namespace WagahighChoices.Toa.Grpc.Internal
{
    internal class Bgra32ImageFromMessagePack : Bgra32Image
    {
        private static readonly ArrayPool<byte> s_pool = ArrayPool<byte>.Shared;

        private byte[] _data;
        private readonly int _dataLength;
        public override ArraySegment<byte> Data => new ArraySegment<byte>(this._data, 0, this._dataLength);

        public Bgra32ImageFromMessagePack(int width, int height, ArraySegment<byte> data)
            : base(width, height)
        {
            this._data = s_pool.Rent(data.Count);
            Buffer.BlockCopy(data.Array, data.Offset, this._data, 0, data.Count);
            this._dataLength = data.Count;
        }

        protected override void Dispose(bool disposing)
        {
            if (this._data != null)
            {
                s_pool.Return(this._data);
                this._data = null;
            }
        }
    }
}
