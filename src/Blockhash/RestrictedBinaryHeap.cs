using System;
using System.Runtime.InteropServices;

namespace WagahighChoices
{
    partial class Blockhash
    {
        [StructLayout(LayoutKind.Auto)]
        internal ref struct RestrictedBinaryHeap<T> where T : IComparable<T>
        {
            private Span<T> _array;
            private int _count;

            public RestrictedBinaryHeap(Span<T> array)
            {
                this._array = array;
                this._count = 0;
            }

            public void Push(T value)
            {
                var nodeIndex = this._count++;

                while (nodeIndex > 0)
                {
                    var parentIndex = (nodeIndex - 1) / 2;

                    if (value.CompareTo(this._array[parentIndex]) <= 0) break;

                    this._array[nodeIndex] = this._array[parentIndex];
                    nodeIndex = parentIndex;
                }

                this._array[nodeIndex] = value;
            }

            public void ReplaceHead(T value)
            {
                var nodeIndex = 0;

                while (true)
                {
                    var childIndex = 2 * nodeIndex + 1;

                    if (childIndex >= this._count) break;

                    if (childIndex + 1 < this._count && this._array[childIndex + 1].CompareTo(this._array[childIndex]) > 0)
                        childIndex++;

                    if (value.CompareTo(this._array[childIndex]) >= 0) break;

                    this._array[nodeIndex] = this._array[childIndex];
                    nodeIndex = childIndex;
                }

                this._array[nodeIndex] = value;
            }

            public T GetHead() => this._array[0];

            public T GetSecond() => this._count < 3 || this._array[1].CompareTo(this._array[2]) >= 0
                ? this._array[1] : this._array[2];
        }
    }
}
