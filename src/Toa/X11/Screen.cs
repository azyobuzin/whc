using System.Runtime.InteropServices;

namespace WagahighChoices.Toa.X11
{
    [StructLayout(LayoutKind.Auto)]
    public struct Screen
    {
        public uint RootWindow { get; }
        public ushort Width { get; }
        public ushort Height { get; }

        public Screen(uint rootWindow, ushort width, ushort height)
        {
            this.RootWindow = rootWindow;
            this.Width = width;
            this.Height = height;
        }
    }
}
