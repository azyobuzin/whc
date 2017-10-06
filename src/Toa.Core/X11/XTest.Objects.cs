using System.Runtime.InteropServices;
using static WagahighChoices.Toa.X11.X11Client;

namespace WagahighChoices.Toa.X11
{
    partial class XTest
    {
        private const int FakeInputRequestSize = 36;

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = FakeInputRequestSize)]
        private struct FakeInputRequest
        {
            [FieldOffset(0)]
            public ExtensionRequestHeader Header;
            [FieldOffset(4)]
            public XTestFakeEventType Type;
            [FieldOffset(5)]
            public byte Detail;
            [FieldOffset(8)]
            public uint Time;
            [FieldOffset(12)]
            public uint Root;
            [FieldOffset(24)]
            public short RootX;
            [FieldOffset(26)]
            public short RootY;
        }
    }
}
