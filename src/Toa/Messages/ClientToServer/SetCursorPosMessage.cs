using System;

namespace WagahighChoices.Toa.Messages.ClientToServer
{
    public class SetCursorPosMessage : XYParameterMessage
    {
        public override byte MessageCode => (byte)ClientToServerMessageCode.SetCursorPos;

        public SetCursorPosMessage() { }

        public SetCursorPosMessage(int x, int y)
            : base(x, y)
        { }

        public static SetCursorPosMessage Deserialize(ArraySegment<byte> bytes)
            => DeserializeCore<SetCursorPosMessage>(bytes);
    }
}
