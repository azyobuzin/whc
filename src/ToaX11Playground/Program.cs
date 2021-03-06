﻿using System;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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

                //await PrintTree(client, screen.RootWindow, 0).ConfigureAwait(false);

                var wagahighWindow = await FindWagahighWindow(client, screen.Root).ConfigureAwait(false);
                await client.ConfigureWindowAsync(wagahighWindow, x: 0, y: 0).ConfigureAwait(false);

                using (var getImageResult = await client.GetImageAsync(screen.Root, 0, 0, screen.Width, screen.Height, uint.MaxValue, GetImageFormat.ZPixmap).ConfigureAwait(false))
                {
                    Console.WriteLine("Depth: " + getImageResult.Depth);
                    Console.WriteLine("RGB: {0:x8}, {1:x8}, {2:x8}", getImageResult.Visual.RedMask, getImageResult.Visual.GreenMask, getImageResult.Visual.BlueMask);

                    SaveImage(getImageResult.Data.Array, screen.Width, screen.Height);
                }

                /*
                for (var i = 0; ; i++)
                {
                    var cursor = await client.XFixes.GetCursorImageAsync().ConfigureAwait(false);
                    SaveCursor(cursor.CursorImage, cursor.Width, cursor.Height, $"cursor{i}.png");
                    Console.WriteLine("{0}, {1}", cursor.X, cursor.Y);
                    await Task.Delay(500).ConfigureAwait(false);
                }
                */

                /*
                var rng = new Random();

                while (true)
                {
                    await client.XTest.FakeInputAsync(
                        XTestFakeEventType.MotionNotify,
                        0, 0, screen.Root,
                        (short)rng.Next(screen.Width),
                        (short)rng.Next(screen.Height)
                    ).ConfigureAwait(false);

                    await Task.Delay(500).ConfigureAwait(false);
                }
                */
            }
        }

        private static async Task PrintTree(X11Client client, uint window, int depth)
        {
            var windowNameAtom = await client.InternAtomAsync("_NET_WM_NAME", false).ConfigureAwait(false);
            var windowName = await client.GetStringPropertyAsync(window, windowNameAtom).ConfigureAwait(false);
            var geometry = await client.GetGeometryAsync(window).ConfigureAwait(false);

            for (var i = 0; i < depth; i++)
                Console.Write("  ");

            Console.WriteLine("0x{0:x} {1} {2}x{3}+{4}+{5} {6} {7}",
                window, windowName,
                geometry.Width, geometry.Height,
                geometry.X, geometry.Y,
                geometry.BorderWidth,
                geometry.Depth);

            var result = await client.QueryTreeAsync(window).ConfigureAwait(false);

            foreach (var child in result.Children)
                await PrintTree(client, child, depth + 1).ConfigureAwait(false);
        }

        private static async Task<uint> FindWagahighWindow(X11Client client, uint root)
        {
            var windowNameAtom = await client.InternAtomAsync("_NET_WM_NAME", false).ConfigureAwait(false);

            foreach (var child in (await client.QueryTreeAsync(root).ConfigureAwait(false)).Children)
            {
                if ((await client.GetStringPropertyAsync(child, windowNameAtom).ConfigureAwait(false)) == "ワガママハイスペック")
                    return child;
            }

            throw new Exception();
        }

        private static void SaveImage(byte[] data, int width, int height)
        {
            using (var img = Image.LoadPixelData<Bgra32>(data, width, height))
            {
                for (var y = 0; y < img.Height; y++)
                {
                    for (var x = 0; x < img.Width; x++)
                    {
                        var p = img[x, y];
                        p.A = 255;
                        img[x, y] = p;
                    }
                }

                img.Save("screen0.png");
            }
        }

        private static void SaveCursor(byte[] data, int width, int height, string fileName)
        {
            using (var img = Image.LoadPixelData<Bgra32>(data, width, height))
            {
                img.Save(fileName);
            }
        }
    }
}
