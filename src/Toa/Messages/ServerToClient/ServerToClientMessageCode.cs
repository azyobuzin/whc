namespace WagahighChoices.Toa.Messages.ServerToClient
{
    public enum ServerToClientMessageCode : byte
    {
        Ready = 1,
        ReplyError,
        ReplyDone,
        LogData,
        CaptureResult,
    }
}
