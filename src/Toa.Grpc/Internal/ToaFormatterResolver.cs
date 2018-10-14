using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace WagahighChoices.Toa.Grpc.Internal
{
    internal sealed class ToaFormatterResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new ToaFormatterResolver();

        private static readonly Bgra32ImageFormatter s_argb32ImageFormatter = new Bgra32ImageFormatter();

        private ToaFormatterResolver() { }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T).Equals(typeof(Bgra32Image)))
                return (IMessagePackFormatter<T>)s_argb32ImageFormatter;

            return StandardResolver.Instance.GetFormatter<T>();
        }
    }
}
