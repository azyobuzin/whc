using System;
using System.Collections.Immutable;

namespace WagahighChoices.Ashe
{
    public class SelectionInfo
    {
        public int Id { get; }
        public byte[] ScreenshotHash { get; } // ImmutableArray にしたい

        public SelectionInfo(int id, string screenshotHash)
        {
            this.Id = id;

            // Blockhash 256bit なので、 64 文字なはず
            if (screenshotHash?.Length != 64) throw new ArgumentException();

            var hash = new byte[32];
            for (var i = 0; i < hash.Length; i++)
            {
                uint HexToUInt(char c)
                {
                    if (c >= '0' && c <= '9') return c - (uint)'0';
                    if (c >= 'a' && c <= 'f') return c - (uint)'a' + 10;
                    if (c >= 'A' && c <= 'F') return c - (uint)'A' + 10;
                    throw new FormatException();
                }

                hash[i] = (byte)(HexToUInt(screenshotHash[i * 2]) << 4 | HexToUInt(screenshotHash[i * 2 + 1]));
            }

            this.ScreenshotHash = hash;
        }

        public override string ToString() => nameof(SelectionInfo) + " " + this.Id;

        // （ここに選択肢の文字列を入れてしまうと MIT License で配布するということに問題が発生してしまうので入れないぞ）
        public static ImmutableArray<SelectionInfo> Selections { get; } = ImmutableArray.Create(
            new SelectionInfo(1, "24f604760c3f6c3f2e7628162ef629b66dbf20097c478d4707c607c407c0cfff"),
            new SelectionInfo(2, "6b006a307f70ffb07fb06db06e706d207ff307e0872606e407600fe003c0ffff"),
            new SelectionInfo(3, "f01df81df80df00df21cf10cfffc0380319f119607b60fa70be00fe00fe3c7e6"),
            new SelectionInfo(4, "1e001000fb8fffbffc0ff81f7e3f00000fc703c783c7c3c683c0c1c0c3c0ffff"),
            new SelectionInfo(5, "fc01fc01fc07fc07fe77fc070ff50000fffc83f9b0039003f00390038e03ffff"),
            new SelectionInfo(6, "e780ff900fc12fc80ff80fd0bff88d00afc00fc0ff40ff406fc03d931ddb09d0"),
            new SelectionInfo(7, "e47ee07ee25c82c44fdcc1c88f70c7e0d7e157017701ff80ff80bea0fe22bc20"),
            new SelectionInfo(8, "81f81ff8bff221808fc2a020bef3a1f3f5b7f933f103c100718ce10f0100ffff"),
            new SelectionInfo(9, "43f009f08fe60bee0ff80ff00ff083f0e34ff7c107c106e100e302670057ffff"),
            new SelectionInfo(10, "001ff9ff031f03170ff709b30ff20256035f03d3b3d293ca580c400c7836ffff"),
            new SelectionInfo(11, "21ff00ff00ff08dc0afe017e2d3838afdbc9ece9e360f200f010c0dcc2ff44fe"),
            new SelectionInfo(12, "01fc1fea33f233c00ff281f03ff013f0b382ff90bfb2848208fc47400780ffff"),
            new RouteSpecificSelectionInfo(13, "3f813f907f80ff007f907e007e707730f7306690e6c09fc39dc08dc0c7e1e7e1", Heroine.Kaoruko),
            new RouteSpecificSelectionInfo(14, "fffc7ce0f8c03800fe42f242f258ff006fa06de0ffe00f029e279e4abff81c00", Heroine.Ashe),
            new RouteSpecificSelectionInfo(15, "f800f818fc38fe3cbe38be10be70bc50bbf0bfe097f000e083f883f0e7f0e3c0", Heroine.Toa),
            new RouteSpecificSelectionInfo(16, "1ffe399c398631c078e078d87c787638039843da43fc61fd33fb03f53bff0000", Heroine.Mihiro)
        );

        public static SelectionInfo GetSelectionById(int id) => Selections[id - 1]; // データ依存ハック
    }

    public class RouteSpecificSelectionInfo : SelectionInfo
    {
        public Heroine Heroine { get; }

        public RouteSpecificSelectionInfo(int id, string screenshotHash, Heroine heroine)
            : base(id, screenshotHash)
        {
            this.Heroine = heroine;
        }
    }
}
