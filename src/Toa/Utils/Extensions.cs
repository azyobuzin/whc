using System.IO;
using System.Threading.Tasks;

namespace WagahighChoices.Toa.Utils
{
    internal static class Extensions
    {
        public static async Task<int> ReadExactAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            var bytesRead = 0;

            while (bytesRead < count)
            {
                var i = await stream.ReadAsync(buffer, bytesRead, count - bytesRead);
                if (i == 0) break;
                bytesRead += i;
            }

            return bytesRead;
        }

        public static void Forget(this Task task) { }
    }
}
