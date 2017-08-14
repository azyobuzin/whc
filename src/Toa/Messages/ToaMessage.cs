using ZeroFormatter;

namespace WagahighChoices.Toa.Messages
{
    public abstract class ToaMessage
    {
        [IgnoreFormat]
        public abstract byte MessageCode { get; }

        public abstract int Serialize(ref byte[] buffer, int offset);
    }
}
