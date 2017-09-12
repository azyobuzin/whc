using System.Threading.Tasks;

namespace WagahighChoices.Toa
{
    internal static class Extensions
    {
        public static ValueTask<T> ToValueTask<T>(this Task<T> task) => new ValueTask<T>(task);
    }
}
