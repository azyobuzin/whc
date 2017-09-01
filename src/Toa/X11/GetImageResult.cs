namespace WagahighChoices.Toa.X11
{
    public class GetImageResult
    {
        public byte Depth { get; }
        public VisualType Visual { get; }
        public byte[] Data { get; }

        public GetImageResult(byte depth, VisualType visual, byte[] data)
        {
            this.Depth = depth;
            this.Visual = visual;
            this.Data = data;
        }
    }

    public enum GetImageFormat : byte
    {
        XYPixmap = 1,
        ZPixmap = 2,
    }
}
