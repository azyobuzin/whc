using System.Drawing;
using MessagePack;
using MessagePack.Formatters;

namespace WagahighChoices.GrpcUtils
{
    internal class SizeFormatter : IMessagePackFormatter<Size>
    {
        public int Serialize(ref byte[] bytes, int offset, Size value, IFormatterResolver formatterResolver)
        {
            var startOffset = offset;
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.Width);
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.Height);
            return offset - startOffset;
        }

        public Size Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            var startOffset = offset;

            var width = MessagePackBinary.ReadInt32(bytes, offset, out var rs);
            offset += rs;

            var height = MessagePackBinary.ReadInt32(bytes, offset, out rs);
            offset += rs;

            readSize = offset - startOffset;
            return new Size(width, height);
        }
    }
}
