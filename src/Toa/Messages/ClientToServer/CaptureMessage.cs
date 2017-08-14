namespace WagahighChoices.Toa.Messages.ClientToServer
{
    public class CaptureMessage : NoContentMessage
    {
        public static CaptureMessage Default { get; } = new CaptureMessage();

        public override byte MessageCode => (byte)ClientToServerMessageCode.Capture;
    }
}
