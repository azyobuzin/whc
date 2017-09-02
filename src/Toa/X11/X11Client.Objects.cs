using System;
using System.Runtime.InteropServices;

namespace WagahighChoices.Toa.X11
{
    partial class X11Client
    {
        internal enum ErrorCode : byte
        {
            Request = 1,
            Value = 2,
            Window = 3,
            Pixmap = 4,
            Atom = 5,
            Cursor = 6,
            Font = 7,
            Match = 8,
            Drawable = 9,
            Access = 10,
            Alloc = 11,
            Colormap = 12,
            GContext = 13,
            IDChoice = 14,
            Name = 15,
            Length = 16,
            Implementation = 17,
        }

        private const int SetupRequestDataSize = 12;

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = SetupRequestDataSize)]
        private struct SetupRequestData
        {
            [FieldOffset(0)]
            public byte ByteOrder;
            [FieldOffset(2)]
            public ushort ProtocolMajorVersion;
            [FieldOffset(4)]
            public ushort ProtocolMinorVersion;
            [FieldOffset(6)]
            public ushort LengthOfAuthorizationProtocolName;
            [FieldOffset(8)]
            public ushort LengthOfAuthorizationProtocolData;
        }

        private const int SetupResponseHeaderSize = 8;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SetupResponseHeader
        {
            public byte Status;
            public byte LengthOfReasonIfFailed;
            public ushort ProtocolMajorVersion;
            public ushort ProtocolMinorVersion;
            public ushort LengthOfAdditionalData;
        }

        private const int SetupResponseDataSize = 32;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SetupResponseData
        {
            public uint ReleaseNumber;
            public uint ResourceIdBase;
            public uint ResouceIdMask;
            public uint MotionBufferSize;
            public ushort LengthOfVendor;
            public ushort MaximumRequestLength;
            public byte NumberOfScreens;
            public byte NumberOfFormats;
            public byte ImageByteOrder; // TODO: enum
            public byte BitmapFormatBitOrder; // TODO: enum
            public byte BitmapFormatScanlineUnit;
            public byte BitmapFormatScanlinePad;
            public byte MinKeycode; // TODO: enum
            public byte MaxKeycode; // TODO: enum
        }

        private const int SetupScreenDataSize = 40;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SetupScreenData
        {
            public uint Root;
            public uint DefaultColormap;
            public uint WhitePixel;
            public uint BlackPixel;
            public SetOfEvent CurrentInputMasks;
            public ushort WidthInPixels;
            public ushort HeightInPixels;
            public ushort WidthInMillimeters;
            public ushort HeightInMillimeters;
            public ushort MinInstalledMaps;
            public ushort MaxInstalledMaps;
            public uint RootVisual;
            public byte BackingStores; // TODO: enum
            public bool SaveUnders;
            public byte RootDepth;
            public byte NumberOfAllowedDepths;
        }

        [Flags]
        private enum SetOfEvent : uint
        {
            KeyPress = 0x00000001,
            KeyRelease = 0x00000002,
            ButtonPress = 0x00000004,
            ButtonRelease = 0x00000008,
            EnterWindow = 0x00000010,
            LeaveWindow = 0x00000020,
            PointerMotion = 0x00000040,
            PointerMotionHint = 0x00000080,
            Button1Motion = 0x00000100,
            Button2Motion = 0x00000200,
            Button3Motion = 0x00000400,
            Button4Motion = 0x00000800,
            Button5Motion = 0x00001000,
            ButtonMotion = 0x00002000,
            KeymapState = 0x00004000,
            Exposure = 0x00008000,
            VisibilityChange = 0x00010000,
            StructureNotify = 0x00020000,
            ResizeRedirect = 0x00040000,
            SubstructureNotify = 0x00080000,
            SubstructureRedirect = 0x00100000,
            FocusChange = 0x00200000,
            PropertyChange = 0x00400000,
            ColormapChange = 0x00800000,
            OwnerGrabButton = 0x01000000,
        }

        private const int SetupDepthDataSize = 8;

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct SetupDepthData
        {
            [FieldOffset(0)]
            public byte Depth;
            [FieldOffset(2)]
            public ushort NumberOfVisuals;
        }

        internal const int VisualTypeSize = 24;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct EventOrReplyHeader
        {
            public byte EventType;
            public ErrorCode ErrorCode;
            public ushort SequenceNumber;
            public int ReplyLength;
        }

        private const int ConfigureWindowRequestSize = 12;

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = ConfigureWindowRequestSize)]
        private struct ConfigureWindowRequest
        {
            [FieldOffset(0)]
            public byte Opcode;
            [FieldOffset(2)]
            public ushort RequestLength;
            [FieldOffset(4)]
            public uint Window;
            [FieldOffset(8)]
            public ushort ValueMask;
        }

        private const int GetGeometryRequestSize = 8;

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = GetGeometryRequestSize)]
        private struct GetGeometryRequest
        {
            [FieldOffset(0)]
            public byte Opcode;
            [FieldOffset(2)]
            public ushort RequestLength;
            [FieldOffset(4)]
            public uint Drawable;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct GetGeometryReply
        {
            public byte Reply;
            public byte Depth;
            public ushort SequenceNumber;
            public uint ReplyLength;
            public uint Root;
            public short X;
            public short Y;
            public ushort Width;
            public ushort Height;
            public ushort BorderWidth;
        }

        private const int QueryTreeRequestSize = 8;

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = QueryTreeRequestSize)]
        private struct QueryTreeRequest
        {
            [FieldOffset(0)]
            public byte Opcode;
            [FieldOffset(2)]
            public ushort RequestLength;
            [FieldOffset(4)]
            public uint Window;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct QueryTreeReply
        {
            public EventOrReplyHeader Header;
            public uint Root;
            public uint Parent;
            public ushort NumberOfChildren;
        }

        private const int InternAtomRequestSize = 8;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = InternAtomRequestSize)]
        private struct InternAtomRequest
        {
            public byte Opcode;
            public bool OnlyIfExists;
            public ushort RequestLength;
            public ushort LengthOfName;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct InternAtomReply
        {
            public EventOrReplyHeader Header;
            public uint Atom;
        }

        private const int GetAtomNameRequestSize = 8;

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = GetAtomNameRequestSize)]
        private struct GetAtomNameRequest
        {
            [FieldOffset(0)]
            public byte Opcode;
            [FieldOffset(2)]
            public ushort RequestLength;
            [FieldOffset(4)]
            public uint Atom;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GetAtomNameReply
        {
            public EventOrReplyHeader Header;
            public ushort LengthOfName;
        }

        private const int GetPropertyRequestSize = 24;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GetPropertyRequest
        {
            public byte Opcode;
            public bool Delete;
            public ushort RequestLength;
            public uint Window;
            public uint Property;
            public uint Type;
            public uint LongOffset;
            public uint LongLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GetPropertyReply
        {
            public byte Reply;
            public byte Format;
            public ushort SequenceNumber;
            public uint ReplyLength;
            public uint Type;
            public uint BytesAfter;
            public uint LengthOfValue;
        }

        private const int GetImageRequestSize = 20;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = GetImageRequestSize)]
        private struct GetImageRequest
        {
            public byte Opcode;
            public GetImageFormat Format;
            public ushort RequestLength;
            public uint Drawable;
            public short X;
            public short Y;
            public ushort Width;
            public ushort Height;
            public uint PlaneMask;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GetImageReply
        {
            public byte Reply;
            public byte Depth;
            public ushort SequenceNumber;
            public uint ReplyLength;
            public uint Visual;
        }

        private const int QueryExtensionRequestSize = 8;

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = QueryExtensionRequestSize)]
        private struct QueryExtensionRequest
        {
            [FieldOffset(0)]
            public byte Opcode;
            [FieldOffset(2)]
            public ushort RequestLength;
            [FieldOffset(4)]
            public ushort LengthOfName;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct QueryExtensionReply
        {
            public EventOrReplyHeader Header;
            public bool Present;
            public byte MajorOpcode;
            public byte FirstEvent;
            public byte FirstError;
        }
    }
}
