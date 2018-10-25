using System.Drawing;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using WagahighChoices.Toa.Imaging;

namespace WagahighChoices.GrpcUtils
{
    public sealed class WhcFormatterResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new WhcFormatterResolver();

        private static readonly Bgra32ImageFormatter s_argb32ImageFormatter = new Bgra32ImageFormatter();
        private static readonly SizeFormatter s_sizeFormatter = new SizeFormatter();

        private WhcFormatterResolver() { }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T).Equals(typeof(Bgra32Image)))
                return (IMessagePackFormatter<T>)s_argb32ImageFormatter;

            if (typeof(T).Equals(typeof(Size)))
                return (IMessagePackFormatter<T>)s_sizeFormatter;

            return StandardResolver.Instance.GetFormatter<T>();
        }
    }
}
