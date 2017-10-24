using System;

namespace WagahighChoices
{
    public struct Pixel : IEquatable<Pixel>
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public Pixel(byte r, byte g, byte b, byte a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        public bool Equals(Pixel other)
        {
            return this.R == other.R && this.G == other.G
                && this.B == other.B && this.A == other.A;
        }

        public override bool Equals(object obj)
        {
            return obj is Pixel p && this.Equals(p);
        }

        public override int GetHashCode()
        {
            return this.R | (this.G << 8) | (this.B << 16) | (this.A << 24);
        }
    }
}
