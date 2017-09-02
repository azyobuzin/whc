namespace WagahighChoices.Toa.X11
{
    public class QueryExtensionResult
    {
        public byte MajorOpcode { get; }
        public byte FirstEvent { get; }
        public byte FirstError { get; }

        public QueryExtensionResult(byte majorOpcode, byte firstEvent, byte firstError)
        {
            this.MajorOpcode = majorOpcode;
            this.FirstEvent = firstEvent;
            this.FirstError = firstError;
        }

        internal unsafe QueryExtensionResult(X11Client.QueryExtensionReply* reply)
        {
            this.MajorOpcode = reply->MajorOpcode;
            this.FirstEvent = reply->FirstEvent;
            this.FirstError = reply->FirstError;
        }
    }
}
