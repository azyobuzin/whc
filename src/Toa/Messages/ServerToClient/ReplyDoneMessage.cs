using ZeroFormatter;

namespace WagahighChoices.Toa.Messages.ServerToClient
{
    [ZeroFormattable]
    public class ReplyDoneMessage : NoContentMessage
    {
        public static ReplyDoneMessage Default { get; } = new ReplyDoneMessage();

        public override byte MessageCode => (byte)ServerToClientMessageCode.ReplyDone;
    }
}
