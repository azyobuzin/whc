using System;
using ZeroFormatter.Internal;

namespace WagahighChoices.Toa.Messages
{
    public abstract class XYParameterMessage : ToaMessage
    {
        public int X { get; set; }
        public int Y { get; set; }

        protected XYParameterMessage() { }

        protected XYParameterMessage(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }

        public override int Serialize(ref byte[] buffer, int offset)
        {
            BinaryUtil.EnsureCapacity(ref buffer, offset, 8);
            BinaryUtil.WriteInt32Unsafe(ref buffer, offset, this.X);
            BinaryUtil.WriteInt32Unsafe(ref buffer, offset + 4, this.Y);
            return 8;
        }

        public static T DeserializeCore<T>(ArraySegment<byte> bytes)
            where T : XYParameterMessage, new()
        {
            if (bytes.Count != 8) throw new InvalidMessageDataException();

            var bs = bytes.Array;

            return new T()
            {
                X = BinaryUtil.ReadInt32(ref bs, bytes.Offset),
                Y = BinaryUtil.ReadInt32(ref bs, bytes.Offset + 4)
            };
        }
    }
}
