namespace WagahighChoices.Toa.X11
{
    public class TranslateCoordinatesResult
    {
        public bool SameScreen { get; }
        public uint Child { get; }
        public short DstX { get; }
        public short DstY { get; }

        public TranslateCoordinatesResult(bool sameScreen, uint child, short dstX, short dstY)
        {
            this.SameScreen = sameScreen;
            this.Child = child;
            this.DstX = dstX;
            this.DstY = dstY;
        }

        internal TranslateCoordinatesResult(ref X11Client.TranslateCoordinatesReply reply)
        {
            this.SameScreen = reply.SameScreen;
            this.Child = reply.Child;
            this.DstX = reply.DstX;
            this.DstY = reply.DstY;
        }
    }
}
