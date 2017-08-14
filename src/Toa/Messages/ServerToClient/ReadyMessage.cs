namespace WagahighChoices.Toa.Messages.ServerToClient
{
    public class ReadyMessage : NoContentMessage
    {
        public static ReadyMessage Default { get; } = new ReadyMessage();

        public override byte MessageCode => (byte)ServerToClientMessageCode.Ready;
    }
}
