using System.IO;
using MessagePack;
using MessagePack.Formatters;
using WagahighChoices.Toa.Imaging;

namespace WagahighChoices.Toa.Grpc.Internal
{
    // シリアライズして Dispose するやつ
    internal class Bgra32ImageFormatter : IMessagePackFormatter<Bgra32Image>
    {
        public int Serialize(ref byte[] bytes, int offset, Bgra32Image value, IFormatterResolver formatterResolver)
        {
            if (value == null)
            {
                return MessagePackBinary.WriteNil(ref bytes, offset);
            }

            var startOffset = offset;

            try
            {
                offset += MessagePackBinary.WriteArrayHeader(ref bytes, offset, 3);
                offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.Width);
                offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.Height);
                var data = value.Data;
                offset += MessagePackBinary.WriteBytes(ref bytes, offset, data.Array, data.Offset, data.Count);
            }
            finally
            {
                value.Dispose();
            }

            return offset - startOffset;
        }

        public Bgra32Image Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            if (MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return null;
            }

            var startOffset = offset;

            var arrayCount = MessagePackBinary.ReadArrayHeaderRaw(bytes, offset, out var rs);
            offset += rs;

            if (arrayCount != 3) throw new InvalidDataException();

            var width = MessagePackBinary.ReadInt32(bytes, offset, out rs);
            offset += rs;

            var height = MessagePackBinary.ReadInt32(bytes, offset, out rs);
            offset += rs;

            var data = MessagePackBinary.ReadBytesSegment(bytes, offset, out rs);
            offset += rs;

            readSize = offset - startOffset;
            return new Bgra32ImageFromMessagePack(width, height, data);
        }
    }
}
