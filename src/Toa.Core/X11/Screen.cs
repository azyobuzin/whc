﻿using System.Runtime.InteropServices;

namespace WagahighChoices.Toa.X11
{
    [StructLayout(LayoutKind.Auto)]
    public struct Screen
    {
        public uint Root { get; }
        public ushort Width { get; }
        public ushort Height { get; }

        public Screen(uint root, ushort width, ushort height)
        {
            this.Root = root;
            this.Width = width;
            this.Height = height;
        }

        internal Screen(ref X11Client.SetupScreenData screen)
        {
            this.Root = screen.Root;
            this.Width = screen.WidthInPixels;
            this.Height = screen.HeightInPixels;
        }
    }
}
