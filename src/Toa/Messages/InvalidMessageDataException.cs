using System;

namespace WagahighChoices.Toa.Messages
{
    public class InvalidMessageDataException : Exception
    {
        private const string DefaultMessage = "想定されていないメッセージの形式です。";

        public InvalidMessageDataException()
            : base(DefaultMessage)
        { }

        public InvalidMessageDataException(string message)
            : base(message)
        { }

        public InvalidMessageDataException(string message, Exception innerException)
            : base(message, innerException)
        { }

        public InvalidMessageDataException(Exception innerException)
            : base(DefaultMessage, innerException)
        { }
    }
}
