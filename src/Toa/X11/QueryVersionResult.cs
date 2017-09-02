namespace WagahighChoices.Toa.X11
{
    public struct QueryVersionResult
    {
        public uint MajorVersion { get; }
        public uint MinorVersion { get; }

        public QueryVersionResult(uint majorVersion, uint minorVersion)
        {
            this.MajorVersion = majorVersion;
            this.MinorVersion = minorVersion;
        }
    }
}
