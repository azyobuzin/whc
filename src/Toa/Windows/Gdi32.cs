using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WagahighChoices.Toa.Windows
{
    public enum BitmapCompression : uint
    {
        BI_RGB = 0,
        BI_RLE8 = 1,
        BI_RLE4 = 2,
        BI_BITFIELDS = 3,
        BI_JPEG = 4,
        BI_PNG = 5
    }

    public enum DibColorTableIdentifier : uint
    {
        DIB_RGB_COLORS = 0,
        DIB_PAL_COLORS = 1
    }

    public enum RasterOperationCode : uint
    {
        SRCCOPY = 0x00CC0020,
        SRCPAINT = 0x00EE0086,
        SRCAND = 0x008800C6,
        SRCINVERT = 0x00660046,
        SRCERASE = 0x00440328,
        NOTSRCCOPY = 0x00330008,
        NOTSRCERASE = 0x001100A6,
        MERGECOPY = 0x00C000CA,
        MERGEPAINT = 0x00BB0226,
        PATCOPY = 0x00F00021,
        PATPAINT = 0x00FB0A09,
        PATINVERT = 0x005A0049,
        DSTINVERT = 0x00550009,
        BLACKNESS = 0x00000042,
        WHITENESS = 0x00FF0062,
        NOMIRRORBITMAP = 0x80000000,
        CAPTUREBLT = 0x40000000
    }

    public class BitmapHandle : SafeHandle
    {
        public BitmapHandle() : base(IntPtr.Zero, true)
        { }

        public override bool IsInvalid => this.handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            return Gdi32.DeleteObject(this.handle);
        }
    }

    public class GdiDCHandle : DCHandle
    {
        protected override bool ReleaseHandle()
        {
            return Gdi32.DeleteDC(this.handle);
        }
    }

    public struct BitmapInfoHeader
    {
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public BitmapCompression Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    public struct RgbQuad
    {
        public byte Blue;
        public byte Green;
        public byte Red;
        public byte Reserved;
    }

    public class BitmapInfo
    {
        public BitmapInfoHeader Header { get; set; }
        public IReadOnlyList<RgbQuad> Colors { get; set; }

        internal unsafe byte[] ToBytes()
        {
            var colorCount = this.Colors?.Count ?? 0;
            var colorsBytes = (colorCount <= 0 ? 1 : colorCount) * sizeof(RgbQuad);

            var bs = new byte[
                sizeof(uint) // biSize
                + sizeof(BitmapInfoHeader) // bmiHeader
                + colorsBytes // bmiColors
            ];

            fixed (byte* ptr = bs)
            {
                *(uint*)ptr = (uint)(sizeof(uint) + sizeof(BitmapInfoHeader)); // biSize
                *(BitmapInfoHeader*)(ptr + sizeof(uint)) = this.Header; // other fields

                var dstColors = (RgbQuad*)(ptr + sizeof(uint) + sizeof(BitmapInfoHeader));

                if (colorCount >= 1)
                {
                    if (this.Colors is RgbQuad[] arrColors)
                    {
                        fixed (RgbQuad* srcColors = arrColors)
                        {
                            Buffer.MemoryCopy(srcColors, dstColors, colorsBytes, arrColors.Length * sizeof(RgbQuad));
                        }
                    }
                    else
                    {
                        for (var i = 0; i < colorCount; i++)
                        {
                            dstColors[i] = this.Colors[i];
                        }
                    }
                }
            }

            return bs;
        }

        internal unsafe void Return(byte[] bs)
        {
            fixed (byte* ptr = bs)
            {
                this.Header = *(BitmapInfoHeader*)(ptr + sizeof(uint));
            }
        }
    }

    public static class Gdi32
    {
        public const string DllName = "gdi32";

        [DllImport(DllName, EntryPoint = "BitBlt", ExactSpelling = true, SetLastError = true)]
        private static extern bool _BitBlt(DCHandle hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, DCHandle hdcSrc, int nXSrc, int nYSrc, RasterOperationCode dwRop);

        public static void BitBlt(DCHandle hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, DCHandle hdcSrc, int nXSrc, int nYSrc, RasterOperationCode dwRop)
        {
            if (!_BitBlt(hdcDest, nXDest, nYDest, nWidth, nHeight, hdcSrc, nXSrc, nYSrc, dwRop))
                throw new Win32Exception();
        }

        [DllImport(DllName, EntryPoint = "CreateCompatibleBitmap", ExactSpelling = true)]
        private static extern BitmapHandle _CreateCompatibleBitmap(DCHandle hdc, int nWidth, int nHeight);

        public static BitmapHandle CreateCompatibleBitmap(DCHandle hdc, int nWidth, int nHeight)
        {
            var result = _CreateCompatibleBitmap(hdc, nWidth, nHeight);
            if (result.IsInvalid) throw new Exception();
            return result;
        }

        [DllImport(DllName, EntryPoint = "CreateCompatibleDC", ExactSpelling = true)]
        private static extern GdiDCHandle _CreateCompatibleDC(DCHandle hdc);

        public static GdiDCHandle CreateCompatibleDC(DCHandle hdc)
        {
            var result = _CreateCompatibleDC(hdc);
            if (result.IsInvalid) throw new Exception();
            return result;
        }

        [DllImport(DllName, ExactSpelling = true)]
        internal static extern bool DeleteDC(IntPtr hdc);

        [DllImport(DllName, ExactSpelling = true)]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport(DllName, EntryPoint = "GetDIBits", ExactSpelling = true)]
        private static extern int _GetDIBits(DCHandle hdc, BitmapHandle hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, [In, Out] byte[] lpbi, DibColorTableIdentifier uUsage);

        public static int GetDIBits(DCHandle hdc, BitmapHandle hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, BitmapInfo lpbi, DibColorTableIdentifier uUsage)
        {
            var bsBitmapInfo = lpbi.ToBytes();
            var result = _GetDIBits(hdc, hbmp, uStartScan, cScanLines, lpvBits, bsBitmapInfo, uUsage);
            lpbi.Return(bsBitmapInfo);

            if (result == 0) throw new Exception();
            return result;
        }

        [DllImport(DllName, ExactSpelling = true)]
        public static extern IntPtr SelectObject(DCHandle hdc, IntPtr hgdiobj);
    }
}
