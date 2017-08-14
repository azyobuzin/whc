using System;
using ZeroFormatter;

namespace WagahighChoices.Toa.Messages
{
    public abstract class ZeroFormattableMessage : ToaMessage
    {
        public override int Serialize(ref byte[] buffer, int offset)
        {
            return ZeroFormatterSerializer.NonGeneric.Serialize(this.GetType(), ref buffer, offset, this);
        }

        protected static T DeserializeCore<T>(ArraySegment<byte> bytes)
        {
            return ZeroFormatterSerializer.Deserialize<T>(bytes.Array, bytes.Offset);
        }
    }
}
