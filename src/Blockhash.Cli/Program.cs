using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using SixLabors.ImageSharp;

namespace WagahighChoices.BlockhashCli
{
    [Command(Name = "blockhash", FullName = "Blockhash CLI")]
    public class Program
    {
        public static int Main(string[] args)
        {
            return CommandLineApplication.Execute<Program>(args);
        }

        [Argument(0), Required]
        public string[] InputFiles { get; set; }

        [Option]
        public int Bits { get; set; } = 16;

        private int OnExecute()
        {
            var hashLength = this.Bits * this.Bits / 8;
            var inputLength = this.InputFiles.Length;
            var results = new byte[hashLength * inputLength];

            // 順番にハッシュを計算
            var successAll = true;
            for (var i = 0; i < this.InputFiles.Length; i++)
            {
                try
                {
                    var span = new Span<byte>(results, hashLength * i, hashLength);

                    using (var image = Image.Load(this.InputFiles[i]))
                    {
                        Blockhash.ComputeHash(new Rgba32InputImage(image), span, this.Bits);
                    }

                    Console.WriteLine("{0}: {1}", i, ToHashString(span));
                }
                catch (Exception ex)
                {
                    if (Debugger.IsAttached) Debugger.Break();
                    Console.Error.WriteLine("{0}: {1}", i, ex);
                    successAll = false;
                }
            }

            if (!successAll) return 1;

            // 最短距離を計算
            if (inputLength > 1)
            {
                var minDistance = int.MaxValue;

                for (var i = 0; i < inputLength; i++)
                {
                    for (var j = 0; j < inputLength; j++)
                    {
                        if (i == j) continue;

                        var bs1 = new ArraySegment<byte>(results, hashLength * i, hashLength);
                        var bs2 = new ArraySegment<byte>(results, hashLength * j, hashLength);
                        var distance = Blockhash.GetDistance(bs1, bs2);

                        if (distance < minDistance) minDistance = distance;
                    }
                }

                Console.WriteLine("Min Distance: {0}", minDistance);
            }

            return 0;
        }

        private static string ToHashString(ReadOnlySpan<byte> hash)
        {
            const string table = "0123456789abcdef";

            var s = new string('\0', hash.Length * 2);
            unsafe
            {
                fixed (char* c = s)
                {
                    for (var i = 0; i < hash.Length; i++)
                    {
                        c[i * 2] = table[hash[i] >> 4];
                        c[i * 2 + 1] = table[hash[i] & 0xf];
                    }
                }
            }

            return s;
        }
    }
}
