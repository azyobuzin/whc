using System;
using System.Threading.Tasks;
using ImageSharp;
using ImageSharp.PixelFormats;
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

                var screen = client.Screens[0];

                await PrintTree(client, screen.RootWindow, 0).ConfigureAwait(false);

                var getImageResult = await client.GetImageAsync(screen.RootWindow, 0, 0, screen.Width, screen.Height, uint.MaxValue, GetImageFormat.ZPixmap).ConfigureAwait(false);
                Console.WriteLine("Depth: " + getImageResult.Depth);
                Console.WriteLine("RGB: {0:x8}, {1:x8}, {2:x8}", getImageResult.Visual.RedMask, getImageResult.Visual.GreenMask, getImageResult.Visual.BlueMask);

                SaveImage(getImageResult.Data, screen.Width, screen.Height);
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

        private static void SaveImage(byte[] data, int width, int height)
        {
            using (var img = Image.LoadPixelData<Argb32>(new Span<byte>(data), width, height))
            {
                var pixels = img.Pixels;
                for (var i = 0; i < pixels.Length; i++)
                    pixels[i].A = 255;

                img.Save("screen0.png");
            }
        }
    }
}
