using System;
using System.Buffers;

namespace WagahighChoices.Toa
{
    internal static class ArrayPool
    {
        public static RentArray<T> Rent<T>(int size)
        {
            return new RentArray<T>(ArrayPool<T>.Shared.Rent(size));
        }
    }

    internal struct RentArray<T> : IDisposable
    {
        public T[] Array { get; }

        internal RentArray(T[] array)
        {
            this.Array = array;
        }

        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(this.Array);
        }
    }
}
