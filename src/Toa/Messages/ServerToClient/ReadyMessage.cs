using System;

namespace WagahighChoices.Toa.Messages.ServerToClient
{
    public class ReadyMessage : ISerializableMessage
    {
        public static ReadyMessage Default { get; } = new ReadyMessage();

        public byte MessageCode => (byte)ServerToClientMessageCode.Ready;
        public int ComputeLength() => 0;
        public void Serialize(ArraySegment<byte> dest) { }
    }
}
