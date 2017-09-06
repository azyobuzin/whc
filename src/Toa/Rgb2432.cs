using System.Numerics;
using System.Runtime.CompilerServices;
using ImageSharp;
using ImageSharp.PixelFormats;

namespace WagahighChoices.Toa
{
    /// <remarks>リトルエンディアンじゃなかったら死ぬ</remarks>
    public struct Rgb2432 : IPixel<Rgb2432>, IPackedVector<uint>
    {
        public uint PackedValue { get; set; }

        public byte R
        {
            get => (byte)(this.PackedValue >> 8);
            set => this.PackedValue = (this.PackedValue & 0xFFFF00FF) | ((uint)value) << 8;
        }

        public byte G
        {
            get => (byte)(this.PackedValue >> 16);
            set => this.PackedValue = (this.PackedValue & 0xFF00FFFF) | ((uint)value) << 16;
        }

        public byte B
        {
            get => (byte)(this.PackedValue >> 24);
            set => this.PackedValue = (this.PackedValue & 0x00FFFFFF) | ((uint)value) << 24;
        }

        public Rgb2432(byte r, byte g, byte b)
        {
            this.PackedValue = ((uint)r) << 8 | ((uint)g) << 16 | ((uint)b) << 24;
        }

        public static bool operator ==(Rgb2432 x, Rgb2432 y) => x.PackedValue == y.PackedValue;

        public static bool operator !=(Rgb2432 x, Rgb2432 y) => x.PackedValue != y.PackedValue;

        public bool Equals(Rgb2432 other) => this == other;

        public override bool Equals(object obj) => obj is Rgb2432 x && this.Equals(x);

        public override int GetHashCode() => this.PackedValue.GetHashCode();

        public PixelOperations<Rgb2432> CreatePixelOperations() => new PixelOperations<Rgb2432>();

        public void PackFromRgba32(Rgba32 source) => this.PackedValue = source.PackedValue << 8;

        public void PackFromVector4(Vector4 vector)
        {
            var argb = default(Argb32);
            argb.PackFromVector4(vector);
            this = Unsafe.As<Argb32, Rgb2432>(ref argb);
        }

        public void ToBgr24(ref Bgr24 dest)
        {
            dest.B = this.B;
            dest.G = this.G;
            dest.R = this.R;
        }

        public void ToBgra32(ref Bgra32 dest)
        {
            dest.B = this.B;
            dest.G = this.G;
            dest.R = this.R;
            dest.A = 255;
        }

        public void ToRgb24(ref Rgb24 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
        }

        public void ToRgba32(ref Rgba32 dest) => dest.PackedValue = this.PackedValue >> 8 | 0xFF000000;

        public Vector4 ToVector4() => new Vector4(this.R, this.G, this.B, 255) / 255;
    }
}
