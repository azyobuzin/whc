using System;
using System.Threading.Tasks;
using WagahighChoices.Toa.X11;

namespace ToaX11Playground
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            Run().Wait();
            Console.ReadLine();
        }

        private static async Task Run()
        {
            using (var client = await X11Client.ConnectAsync("127.0.0.1", 0).ConfigureAwait(false))
            {
                Console.WriteLine("Vender: " + client.ServerVendor);
                Console.WriteLine("Screen Count: " + client.Screens.Count);
            }
        }
    }
}
