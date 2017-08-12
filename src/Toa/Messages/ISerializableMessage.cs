using System;

namespace WagahighChoices.Toa.Messages
{
    public interface ISerializableMessage
    {
        byte MessageCode { get; }
        int ComputeLength();
        void Serialize(ArraySegment<byte> dest);
    }
}
