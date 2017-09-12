namespace WagahighChoices.Toa.X11
{
    public struct XFixesQueryVersionResult
    {
        public uint MajorVersion { get; }
        public uint MinorVersion { get; }

        public XFixesQueryVersionResult(uint majorVersion, uint minorVersion)
        {
            this.MajorVersion = majorVersion;
            this.MinorVersion = minorVersion;
        }

        internal XFixesQueryVersionResult(ref XFixes.QueryVersionReply reply)
        {
            this.MajorVersion = reply.MajorVersion;
            this.MinorVersion = reply.MinorVersion;
        }
    }
}
