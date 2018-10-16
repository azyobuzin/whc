using System;
using System.Collections.Immutable;

namespace WagahighChoices.Ashe
{
    internal class CursorGlyph
    {
        public ImmutableArray<ushort> Bitmap { get; }
        public ImmutableArray<ushort> Mask { get; }

        protected CursorGlyph(ImmutableArray<ushort> bitmap, ImmutableArray<ushort> mask)
        {
            if (bitmap.IsDefault || bitmap.Length != 16 || mask.IsDefault || mask.Length != 16)
                throw new ArgumentException("大きさは 16x16 固定");

            this.Bitmap = bitmap;
            this.Mask = mask;
        }

        // TODO: IsMatch(Bgra32Image)

        // https://gitlab.freedesktop.org/xorg/font/cursor-misc/blob/d6b8b52fe052ae4f4a5a953c4ac66826391c8613/cursor.bdf#L1294-1337
        public static CursorGlyph Hand2 { get; } = new CursorGlyph(
            ImmutableArray.Create<ushort>(
                0x0000,
                0x7f80,
                0x8040,
                0x7e20,
                0x1010,
                0x0e10,
                0x1010,
                0x0e28,
                0x1044,
                0x0c82,
                0x0304,
                0x0248,
                0x0110,
                0x00a0,
                0x0040,
                0x0000
            ),
            ImmutableArray.Create<ushort>(
                0x7f80,
                0xffc0,
                0xffe0,
                0xfff0,
                0x7ff8,
                0x1ff8,
                0x3ff8,
                0x1ffc,
                0x3ffe,
                0x1fff,
                0x0ffe,
                0x07fc,
                0x03f8,
                0x01f0,
                0x00e0,
                0x0040
            )
        );
    }
}
