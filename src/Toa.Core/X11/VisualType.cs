using System.Runtime.InteropServices;

namespace WagahighChoices.Toa.X11
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = X11Client.VisualTypeSize)]
    public struct VisualType
    {
        public uint VisualId;
        public VisualClass Class;
        public byte BitsPerRgbValue;
        public ushort ColormapEntries;
        public uint RedMask;
        public uint GreenMask;
        public uint BlueMask;
    }

    public enum VisualClass : byte
    {
        StaticGray = 0,
        GrayScale = 1,
        StaticColor = 2,
        PseudoColor = 3,
        TrueColor = 4,
        DirectColor = 5,
    }
}
