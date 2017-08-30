using System;

namespace WagahighChoices.Toa.X11
{
    public class X11Exception : Exception
    {
        public X11Exception(string message) : base(message) { }
        public X11Exception(string message, Exception innerException) : base(message, innerException) { }
    }
}
