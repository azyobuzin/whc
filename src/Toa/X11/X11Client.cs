using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WagahighChoices.Toa.X11
{
    public class X11Client : IDisposable
    {
        private static readonly ArrayPool<byte> s_pool = ArrayPool<byte>.Shared;
        protected Stream Stream { get; }
        private SetupResponseData _setup;
        private PixmapFormat[] _pixmapFormats;

        protected X11Client(Stream stream)
        {
            this.Stream = stream;
        }

        public static async Task<X11Client> Connect(string host, int display)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, 6000 + display).ConfigureAwait(false);
            var x11Client = new X11Client(tcpClient.GetStream());
            await x11Client.SetupConnection().ConfigureAwait(false);
            return x11Client;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) this.Stream.Dispose();
        }

        public void Dispose() => this.Dispose(true);

        private static int ComputePad(int e) => (4 - (e % 4)) % 4;

        private async Task ReadExact(byte[] buffer, int count)
        {
            for (var i = 0; i < count;)
            {
                var bytesRead = await this.Stream.ReadAsync(buffer, i, count - i).ConfigureAwait(false);
                if (bytesRead == 0) throw new EndOfStreamException();
                i += bytesRead;
            }
        }

        private static string ReadString8(byte[] buffer, int index, int count)
        {
            return Encoding.UTF8.GetString(buffer, index, count);
        }

        private async Task SetupConnection()
        {
            SetupResponseHeader responseHeader;

            using (var rentArray = ArrayPool.Rent<byte>(SetupRequestDataSize))
            {
                var buf = rentArray.Array;

                unsafe
                {
                    fixed (byte* p = buf)
                    {
                        *(SetupRequestData*)p = new SetupRequestData()
                        {
                            ByteOrder = BitConverter.IsLittleEndian ? (byte)0x6c : (byte)0x42,
                            ProtocolMajorVersion = 11,
                            ProtocolMinorVersion = 0,
                            LengthOfAuthorizationProtocolName = 0,
                            LengthOfAuthorizationProtocolData = 0,
                        };
                    }
                }

                await this.Stream.WriteAsync(buf, 0, SetupRequestDataSize).ConfigureAwait(false);

                await this.ReadExact(buf, SetupResponseHeaderSize).ConfigureAwait(false);

                unsafe
                {
                    fixed (byte* p = buf)
                    {
                        responseHeader = *(SetupResponseHeader*)p;
                    }
                }
            }

            var additionalDataLength = responseHeader.LengthOfAdditionalData * 4;
            using (var rentArray = ArrayPool.Rent<byte>(additionalDataLength))
            {
                var buf = rentArray.Array;
                await this.ReadExact(buf, additionalDataLength).ConfigureAwait(false);

                switch (responseHeader.Status)
                {
                    case 0: // Failed
                        HandleRefused(buf);
                        break;
                    case 2: // Authenticate
                        HandleAuthenticationRequired(buf);
                        break;
                    case 1: // Success
                        HandleAccepted(buf);
                        break;
                    default:
                        throw new X11Exception("Unexpected response status");
                }
            }

            void HandleRefused(byte[] additionalData)
            {
                throw new X11Exception(string.Format(
                    "The server (X{0}.{1}) refused the connection: {2}",
                    responseHeader.ProtocolMajorVersion,
                    responseHeader.ProtocolMinorVersion,
                    ReadString8(additionalData, 0, responseHeader.LengthOfReasonIfFailed)
                ));
            }

            void HandleAuthenticationRequired(byte[] additionalData)
            {
                throw new X11Exception("Authentication is required: "
                    + ReadString8(additionalData, 0, additionalDataLength).TrimEnd('\0'));
            }

            void HandleAccepted(byte[] additionalData)
            {
                unsafe
                {
                    fixed (byte* p = additionalData)
                    {
                        this._setup = *(SetupResponseData*)p;
                    }
                }

                // あとで使いそうなので一応取得
                this._pixmapFormats = new ReadOnlySpan<byte>(additionalData,
                    SetupResponseDataSize + this._setup.LengthOfVendor + ComputePad(this._setup.LengthOfVendor),
                    8 * this._setup.NumberOfFormats)
                    .NonPortableCast<byte, PixmapFormat>()
                    .ToArray();

                // TODO: WINDOW の root くらいとりたい
            }
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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = SetupResponseHeaderSize)]
        private struct SetupResponseHeader
        {
            public byte Status;
            public byte LengthOfReasonIfFailed;
            public ushort ProtocolMajorVersion;
            public ushort ProtocolMinorVersion;
            public ushort LengthOfAdditionalData;
        }

        private const int SetupResponseDataSize = 32;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = SetupResponseDataSize)]
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

        private const int PixmapFormatSize = 8;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = PixmapFormatSize)]
        private struct PixmapFormat
        {
            public byte Depth;
            public byte BitsPerPixel;
            public byte ScanlinePad;
        }


    }
}
