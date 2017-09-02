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
    }
}
