namespace WagahighChoices.Toa.Messages
{
    internal static class SerializationUtils
    {
        public static void WriteInt(byte[] bs, int offset, int value)
        {
            var ui = (uint)value;
            bs[offset] = (byte)ui;
            bs[offset + 1] = (byte)(ui >> 8);
            bs[offset + 2] = (byte)(ui >> 16);
            bs[offset + 3] = (byte)(ui >> 24);
        }

        public static int ReadInt(byte[] bs, int offset)
        {
            return bs[offset] | bs[offset + 1] << 8 | bs[offset + 2] << 16 | bs[offset + 3] << 24;
        }
    }
}
