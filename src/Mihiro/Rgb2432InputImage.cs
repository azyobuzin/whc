using System.Runtime.CompilerServices;
using WagahighChoices.Toa;

// TODO: Ashe のほうに移動させる
namespace WagahighChoices.Mihiro
{
    /// <summary>
    /// <see cref="Argb32Image"/> の <see cref="Pixel.A"/> を無視してピクセル情報を公開します。
    /// </summary>
    internal struct Rgb2432InputImage : IInputImage
    {
        private readonly Argb32Image _image;
        private readonly byte[] _data;
        private readonly int _offset;

        public Rgb2432InputImage(Argb32Image image)
        {
            this._image = image;
            var data = image.Data;
            this._data = data.Array;
            this._offset = data.Offset;
        }

        public int Width => this._image.Width;

        public int Height => this._image.Height;

        public Pixel GetPixel(int index)
        {
            var x = Unsafe.ReadUnaligned<uint>(ref this._data[this._offset + index * 4]);
            return new Pixel((byte)(x >> 16), (byte)(x >> 8), (byte)x, 255);
        }
    }
}
