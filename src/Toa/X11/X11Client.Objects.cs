using System;
using System.Runtime.InteropServices;

namespace WagahighChoices.Toa.X11
{
    partial class X11Client
    {
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

        private const int VisualTypeSize = 24;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = VisualTypeSize)]
        private struct VisualType
        {
            public uint VisualId;
            public VisualClass Class;
            public byte BitsPerRgbValue;
            public ushort ColormapEntries;
            public uint RedMask;
            public uint GreenMask;
            public uint BlueMask;
        }

        private enum VisualClass : byte
        {
            StaticGray = 0,
            GrayScale = 1,
            StaticColor = 2,
            PseudoColor = 3,
            TrueColor = 4,
            DirectColor = 5,
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct EventOrReplyHeader
        {
            [FieldOffset(0)]
            public byte EventType;
            [FieldOffset(2)]
            public ushort SequenceNumber;
            [FieldOffset(4)]
            public int ReplyLength;
        }
    }
}
