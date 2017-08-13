using ZeroFormatter;

namespace WagahighChoices.Toa.Messages
{
    public abstract class ToaMessage
    {
        [IgnoreFormat]
        public abstract byte MessageCode { get; }
    }
}
