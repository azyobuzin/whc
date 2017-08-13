using ZeroFormatter;

namespace WagahighChoices.Toa.Messages.ServerToClient
{
    [ZeroFormattable]
    public class ReadyMessage : ToaMessage
    {
        public static ReadyMessage Default { get; } = new ReadyMessage();

        public override byte MessageCode => (byte)ServerToClientMessageCode.Ready;
    }
}
