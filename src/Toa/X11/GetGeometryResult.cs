namespace WagahighChoices.Toa.X11
{
    public class GetGeometryResult
    {
        public uint Root { get; }
        public byte Depth { get; }
        public short X { get; }
        public short Y { get; }
        public ushort Width { get; }
        public ushort Height { get; }
        public ushort BorderWidth { get; }

        public GetGeometryResult(uint root, byte depth, short x, short y, ushort width, ushort height, ushort borderWidth)
        {
            this.Root = root;
            this.Depth = depth;
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
            this.BorderWidth = borderWidth;
        }

        internal GetGeometryResult(ref X11Client.GetGeometryReply reply)
        {
            this.Root = reply.Root;
            this.Depth = reply.Depth;
            this.X = reply.X;
            this.Y = reply.Y;
            this.Width = reply.Width;
            this.Height = reply.Height;
            this.BorderWidth = reply.BorderWidth;
        }
    }
}
