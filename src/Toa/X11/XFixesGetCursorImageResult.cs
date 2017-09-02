namespace WagahighChoices.Toa.X11
{
    public class XFixesGetCursorImageResult
    {
        public short X { get; }
        public short Y { get; }
        public ushort Width { get; }
        public ushort Height { get; }
        public ushort XHot { get; }
        public ushort YHot { get; }
        public uint CursorSerial { get; }
        public byte[] CursorImage { get; }

        public XFixesGetCursorImageResult(short x, short y, ushort width, ushort height, ushort xHot, ushort yHot, uint cursorSerial, byte[] cursorImage)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
            this.XHot = xHot;
            this.YHot = yHot;
            this.CursorSerial = cursorSerial;
            this.CursorImage = cursorImage;
        }

        internal unsafe XFixesGetCursorImageResult(XFixes.GetCursorImageReply* reply, byte[] cursorImage)
        {
            this.X = reply->X;
            this.Y = reply->Y;
            this.Width = reply->Width;
            this.Height = reply->Height;
            this.XHot = reply->XHot;
            this.YHot = reply->YHot;
            this.CursorSerial = reply->CursorSerial;
            this.CursorImage = cursorImage;
        }
    }
}
