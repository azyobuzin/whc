using System;

namespace WagahighChoices.Toa.Messages.ClientToServer
{
    public class MouseClickMessage : XYParameterMessage
    {
        public override byte MessageCode => (byte)ClientToServerMessageCode.MouseClick;

        public MouseClickMessage() { }

        public MouseClickMessage(int x, int y)
            : base(x, y)
        { }

        public static MouseClickMessage Deserialize(ArraySegment<byte> bytes)
            => DeserializeCore<MouseClickMessage>(bytes);
    }
}
