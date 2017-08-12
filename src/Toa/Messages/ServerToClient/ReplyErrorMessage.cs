using System;

namespace WagahighChoices.Toa.Messages.ServerToClient
{
    public class ReplyErrorMessage : ISerializableMessage
    {
        public int MessageId { get; }
        public ServerErrorCode ErrorCode { get; }

        public ReplyErrorMessage(int messageId, ServerErrorCode errorCode)
        {
            this.MessageId = messageId;
            this.ErrorCode = errorCode;
        }

        public byte MessageCode => (byte)ServerToClientMessageCode.ReplyError;
        public int ComputeLength() => 5;

        public void Serialize(ArraySegment<byte> dest)
        {
            SerializationUtils.WriteInt(dest.Array, dest.Offset, this.MessageId);
            dest.Array[dest.Offset + 4] = (byte)this.ErrorCode;
        }

        public static ReplyErrorMessage Deserialize(byte[] src)
        {
            return new ReplyErrorMessage(
                SerializationUtils.ReadInt(src, 0),
                (ServerErrorCode)src[4]
            );
        }
    }

    public enum ServerErrorCode : byte
    {
        NotReady = 1,
        ServerError = 2,
    }
}
