namespace WagahighChoices.Toa.Messages
{
    public abstract class NoContentMessage : ToaMessage
    {
        public override int Serialize(ref byte[] buffer, int offset) => 0;
    }
}
