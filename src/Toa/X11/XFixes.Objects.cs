using System.Runtime.InteropServices;
using static WagahighChoices.Toa.X11.X11Client;

namespace WagahighChoices.Toa.X11
{
    partial class XFixes
    {
        private const int ExtensionRequestHeaderSize = 4;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = ExtensionRequestHeaderSize)]
        private struct ExtensionRequestHeader
        {
            public byte MajorOpcode;
            public byte MinorOpcode;
            public ushort RequestLength;
        }

        private const int QueryVersionRequestSize = 12;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = QueryVersionRequestSize)]
        private struct QueryVersionRequest
        {
            public ExtensionRequestHeader Header;
            public uint ClientMajorVersion;
            public uint ClientMinorVersion;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct QueryVersionReply
        {
            public EventOrReplyHeader Header;
            public uint MajorVersion;
            public uint MinorVersion;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct GetCursorImageReply
        {
            public EventOrReplyHeader Header;
            public short X;
            public short Y;
            public ushort Width;
            public ushort Height;
            public ushort XHot;
            public ushort YHot;
            public uint CursorSerial;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct GetCursorImageAndNameReply
        {
            public EventOrReplyHeader Header;
            public short X;
            public short Y;
            public ushort Width;
            public ushort Height;
            public ushort XHot;
            public ushort YHot;
            public uint CursorSerial;
            public uint CursorAtom;
            public ushort NBytes;
        }
    }
}
