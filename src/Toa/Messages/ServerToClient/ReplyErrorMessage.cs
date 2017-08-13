using ZeroFormatter;

namespace WagahighChoices.Toa.Messages.ServerToClient
{
    [ZeroFormattable]
    public class ReplyErrorMessage : ToaMessage
    {
        public override byte MessageCode => (byte)ServerToClientMessageCode.ReplyError;

        [Index(0)]
        public virtual int MessageId { get; set; }

        [Index(1)]
        public virtual ServerErrorCode ErrorCode { get; set; }

        [Index(2)]
        public virtual string AdditionalMessage { get; set; }

        public ReplyErrorMessage() { }

        public ReplyErrorMessage(int messageId, ServerErrorCode errorCode, string additionalMessage = null)
        {
            this.MessageId = messageId;
            this.ErrorCode = errorCode;
            this.AdditionalMessage = additionalMessage;
        }
    }

    public enum ServerErrorCode : byte
    {
        NotReady = 1,
        ServerError,
        UnsupportedMessageCode,
        InvalidMessage,
    }
}
