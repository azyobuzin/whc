using System;
using System.Runtime.InteropServices;

namespace WagahighChoices.Toa.Windows
{
    public abstract class DCHandle : SafeHandle
    {
        public DCHandle(bool ownsHandle)
            : base(IntPtr.Zero, ownsHandle)
        { }

        public DCHandle() : this(true) { }

        public override bool IsInvalid => this.handle == IntPtr.Zero;
    }
}
