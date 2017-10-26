using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace WagahighChoices.Toa.Grpc.Internal
{
    internal sealed class ToaFormatterResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new ToaFormatterResolver();

        private static readonly Argb32ImageFormatter s_argb32ImageFormatter = new Argb32ImageFormatter();

        private ToaFormatterResolver() { }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T).Equals(typeof(Argb32Image)))
                return (IMessagePackFormatter<T>)s_argb32ImageFormatter;

            return StandardResolver.Instance.GetFormatter<T>();
        }
    }
}
