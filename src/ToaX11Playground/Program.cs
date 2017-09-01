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

                await PrintTree(client, client.Screens[0].RootWindow, 0).ConfigureAwait(false);
            }
        }

        private static async Task PrintTree(X11Client client, uint window, int depth)
        {
            var windowNameAtom = await client.InternAtomAsync("_NET_WM_NAME", false).ConfigureAwait(false);
            var windowName = await client.GetStringPropertyAsync(window, windowNameAtom).ConfigureAwait(false);

            for (var i = 0; i < depth; i++)
                Console.Write("  ");

            Console.WriteLine("{0} ({1})", window, windowName);

            var result = await client.QueryTreeAsync(window).ConfigureAwait(false);

            foreach (var child in result.Children)
                await PrintTree(client, child, depth + 1).ConfigureAwait(false);
        }
    }
}
