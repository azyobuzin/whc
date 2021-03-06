﻿using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace WagahighChoices
{
    public static partial class Blockhash
    {
        public static void ComputeHash<TImage>(TImage image, Span<byte> destination, int bits = 16)
            where TImage : IInputImage
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (bits < 4 || bits % 4 != 0) throw new ArgumentException("bits が 4 の倍数ではありません。");
            if (destination.Length < bits * bits / 8) throw new ArgumentException("destination が小さすぎます。");

            // ゼロクリア
            destination.Clear();

            if (image.Width % bits == 0 && image.Height % bits == 0)
            {
                ComputeHashQuick(image, bits, destination, ArrayPool<ulong>.Shared);
            }
            else
            {
                ComputeHashSlow(image, bits, destination, ArrayPool<double>.Shared);
            }
        }

        public static int GetDistance(ArraySegment<byte> bs1, ArraySegment<byte> bs2)
        {
            if (bs1.Count != bs2.Count) throw new ArgumentException("bs1 と bs2 の長さが違います。");

            var count = bs1.Count;
            var distance = 0;
            var i = 0;

            if (Vector.IsHardwareAccelerated)
            {
                var vecSize = Vector<byte>.Count;

                for (; i + vecSize <= count; i += vecSize)
                {
                    var vec1 = Vector.AsVectorUInt64(new Vector<byte>(bs1.Array, bs1.Offset + i));
                    var vec2 = Vector.AsVectorUInt64(new Vector<byte>(bs2.Array, bs2.Offset + i));
                    var weight = vec1 ^ vec2;

                    // Vector にビットシフトが入ったら全部 Vector でやりたい
                    for (var j = 0; j < Vector<ulong>.Count; j++)
                        distance += popcount64c(weight[j]);
                }
            }

            for (; count - i >= 8; i += 8)
            {
                distance += popcount64c(
                    Unsafe.ReadUnaligned<ulong>(ref bs1.Array[bs1.Offset + i])
                    ^ Unsafe.ReadUnaligned<ulong>(ref bs2.Array[bs2.Offset + i])
                );
            }

            for (; count - i >= 4; i += 4)
            {
                distance += popcount32(
                   Unsafe.ReadUnaligned<uint>(ref bs1.Array[bs1.Offset + i])
                   ^ Unsafe.ReadUnaligned<uint>(ref bs2.Array[bs2.Offset + i])
                );
            }

            for (; i < count; i++)
            {
                distance += popcount32(
                   (uint)bs1.Array[bs1.Offset + i]
                   ^ bs2.Array[bs2.Offset + i]
                );
            }

            return distance;

            // https://en.wikipedia.org/wiki/Hamming_weight#Efficient_implementation
            int popcount64c(ulong x)
            {
                x -= (x >> 1) & 0x5555555555555555;
                x = (x & 0x3333333333333333) + ((x >> 2) & 0x3333333333333333);
                x = (x + (x >> 4)) & 0x0f0f0f0f0f0f0f0f;
                return (int)((x * 0x0101010101010101) >> 56);
            }

            // https://graphics.stanford.edu/~seander/bithacks.html#CountBitsSetParallel
            int popcount32(uint v)
            {
                v = v - ((v >> 1) & 0x55555555);
                v = (v & 0x33333333) + ((v >> 2) & 0x33333333);
                return (int)(((v + (v >> 4) & 0xF0F0F0F) * 0x1010101) >> 24);
            }
        }

        private static void ComputeHashQuick<TImage>(TImage image, int bits, Span<byte> dest, ArrayPool<ulong> arrayPool)
            where TImage : IInputImage
        {
            var blocksLength = bits * bits;

            // ブロック + 中央値 + 中央値計算用領域
            var blocks = arrayPool.Rent(blocksLength + (blocksLength / 2) + 8);

            try
            {
                var blockWidth = image.Width / bits;
                var blockHeight = image.Height / bits;
                var groupSize = blocksLength / 4;

                Parallel.For(0, blocksLength, blockIndex =>
                {
                    var xStart = (blockIndex % bits) * blockWidth;
                    var xEnd = xStart + blockWidth;

                    var yStart = (blockIndex / bits) * blockHeight;
                    var yEnd = yStart + blockHeight;

                    ulong blockValue = 0;

                    for (var y = yStart; y < yEnd; y++)
                    {
                        var baseIndex = image.Width * y;

                        for (var x = xStart; x < xEnd; x++)
                        {
                            var pixel = image.GetPixel(baseIndex + x);
                            blockValue += pixel.A == 0
                                ? 255UL * 3
                                : (ulong)pixel.R + pixel.G + pixel.B;
                        }
                    }

                    blocks[blockIndex] = blockValue;
                });

                // note: 公式 C 実装では 256
                var h = 255 * 3 * (ulong)blockWidth * (ulong)blockHeight / 2;

                Parallel.For(0, 4, groupIndex =>
                {
                    var heapCapacity = groupSize / 2 + 1;
                    var heap = new RestrictedBinaryHeap<ulong>(new Span<ulong>(
                        blocks, blocksLength + 4 + heapCapacity * groupIndex, groupSize));

                    var blockStart = groupIndex * groupSize;
                    var blockIndex = blockStart;
                    var pushEnd = blockIndex + heapCapacity;
                    var blockEnd = blockIndex + groupSize;

                    for (; blockIndex < pushEnd; blockIndex++)
                        heap.Push(blocks[blockIndex]);

                    for (; blockIndex < blockEnd; blockIndex++)
                    {
                        var block = blocks[blockIndex];
                        if (block < heap.GetHead())
                            heap.ReplaceHead(block);
                    }

                    // note: 公式 C 実装とは違う
                    var med = groupSize % 2 == 0
                        ? (heap.GetHead() + heap.GetSecond()) / 2
                        : heap.GetHead();

                    // 結果を余剰スペースに保存
                    blocks[blocksLength + groupIndex] = med;
                });

                for (var groupIndex = 0; groupIndex < 4; groupIndex++)
                {
                    var med = blocks[blocksLength + groupIndex];
                    var isMedLarger = med > h;

                    var blockIndex = groupIndex * groupSize;
                    var blockEnd = blockIndex + groupSize;

                    for (; blockIndex < blockEnd; blockIndex++)
                    {
                        var block = blocks[blockIndex];
                        if (block > med || (isMedLarger && block == med))
                            dest[blockIndex / 8] |= (byte)(1 << (7 - blockIndex % 8));
                    }
                }
            }
            finally
            {
                arrayPool.Return(blocks);
            }
        }

        private static void ComputeHashSlow<TImage>(TImage image, int bits, Span<byte> dest, ArrayPool<double> arrayPool)
            where TImage : IInputImage
        {
            var blocksLength = bits * bits;

            // ブロック + 中央値 + 中央値計算用領域
            var blocks = arrayPool.Rent(blocksLength + (blocksLength / 2) + 8);

            try
            {
                var blockWidth = (double)image.Width / bits;
                var blockHeight = (double)image.Height / bits;
                var groupSize = blocksLength / 4;

                Parallel.For(0, blocksLength, blockIndex =>
                {
                    var xStart = (blockIndex % bits) * blockWidth;
                    var xEnd = xStart + blockWidth;

                    var yStart = (blockIndex / bits) * blockHeight;
                    var yEnd = yStart + blockHeight;

                    var blockValue = 0.0;

                    var xStartI = (int)xStart;
                    var xEndI = (int)Math.Ceiling(xEnd);
                    var yEndI = (int)Math.Ceiling(yEnd);
                    for (var y = (int)yStart; y < yEndI; y++)
                    {
                        var baseIndex = image.Width * y;
                        var baseWeight =
                            y - yStart is var pxFromTop && pxFromTop < 0 ? 1.0 + pxFromTop
                            : y - yEnd is var pxFromBottom && pxFromBottom > 0 ? 1.0 - pxFromBottom
                            : 1.0;

                        for (var x = xStartI; x < xEndI; x++)
                        {
                            var pixel = image.GetPixel(baseIndex + x);
                            var v = pixel.A == 0
                                ? 255 * 3
                                : pixel.R + pixel.G + pixel.B;

                            var weight = baseWeight;
                            if (x - xStart is var pxFromLeft && pxFromLeft < 0)
                            {
                                weight *= 1.0 + pxFromLeft;
                            }
                            else if (x - xEnd is var pxFromRight && pxFromRight > 0)
                            {
                                weight *= 1.0 - pxFromRight;
                            }

                            blockValue += v * weight;
                        }
                    }

                    blocks[blockIndex] = blockValue;
                });

                // note: 公式 C 実装では 256
                var h = 255 * 3 * blockWidth * blockHeight / 2;

                Parallel.For(0, 4, groupIndex =>
                {
                    var heapCapacity = groupSize / 2 + 1;
                    var heap = new RestrictedBinaryHeap<double>(new Span<double>(
                        blocks, blocksLength + 4 + heapCapacity * groupIndex, groupSize));

                    var blockStart = groupIndex * groupSize;
                    var blockIndex = blockStart;
                    var pushEnd = blockIndex + heapCapacity;
                    var blockEnd = blockIndex + groupSize;

                    for (; blockIndex < pushEnd; blockIndex++)
                        heap.Push(blocks[blockIndex]);

                    for (; blockIndex < blockEnd; blockIndex++)
                    {
                        var block = blocks[blockIndex];
                        if (block < heap.GetHead())
                            heap.ReplaceHead(block);
                    }

                    // note: 公式 C 実装とは違う
                    var med = groupSize % 2 == 0
                        ? (heap.GetHead() + heap.GetSecond()) / 2
                        : heap.GetHead();

                    // 結果を余剰スペースに保存
                    blocks[blocksLength + groupIndex] = med;
                });

                for (var groupIndex = 0; groupIndex < 4; groupIndex++)
                {
                    var med = blocks[blocksLength + groupIndex];
                    var isMedLarger = med > h;

                    var blockIndex = groupIndex * groupSize;
                    var blockEnd = blockIndex + groupSize;

                    for (; blockIndex < blockEnd; blockIndex++)
                    {
                        var block = blocks[blockIndex];
                        if (block > med || (isMedLarger && Math.Abs(block - med) < 1.0))
                            dest[blockIndex / 8] |= (byte)(1 << (7 - blockIndex % 8));
                    }
                }
            }
            finally
            {
                arrayPool.Return(blocks);
            }
        }
    }
}
