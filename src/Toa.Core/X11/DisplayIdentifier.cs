using System;

namespace WagahighChoices.Toa.X11
{
    public struct DisplayIdentifier : IEquatable<DisplayIdentifier>
    {
        public string Host { get; }
        public int Display { get; }
        public int Screen { get; }

        public DisplayIdentifier(string host, int display = 0, int screen = 0)
        {
            this.Host = host;
            this.Display = display;
            this.Screen = screen;
        }

        public bool Equals(DisplayIdentifier other)
        {
            return this.Host == other.Host
                && this.Display == other.Display
                && this.Screen == other.Screen;
        }

        public override bool Equals(object obj)
        {
            return obj is DisplayIdentifier x && this.Equals(x);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = this.Host != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(this.Host) : 0;
                hashCode = (hashCode * 397) ^ this.Display;
                hashCode = (hashCode * 397) ^ this.Screen;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return this.Host + ":" + this.Display + "." + this.Screen;
        }

        public static DisplayIdentifier Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return new DisplayIdentifier("localhost");

            var colonIndex = s.IndexOf(':');
            if (colonIndex < 0) return new DisplayIdentifier(s);

            var host = colonIndex == 0 ? "localhost" : s.Remove(colonIndex);

            var dotIndex = s.IndexOf('.', colonIndex + 1);

            var display = int.Parse(s.Substring(colonIndex + 1, (dotIndex < 0 ? s.Length : dotIndex) - colonIndex - 1));
            var screen = dotIndex < 0 ? 0 : int.Parse(s.Substring(dotIndex + 1));

            return new DisplayIdentifier(host, display, screen);
        }
    }
}
