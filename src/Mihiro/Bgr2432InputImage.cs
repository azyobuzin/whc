using System.Runtime.InteropServices;
using WagahighChoices.Toa;

// TODO: Ashe のほうに移動させる
namespace WagahighChoices.Mihiro
{
    /// <summary>
    /// <see cref="Bgra32Image"/> の <see cref="Pixel.A"/> を無視してピクセル情報を公開します。
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct Bgr2432InputImage : IInputImage
    {
        private readonly Bgra32Image _image;
        private readonly byte[] _data;
        private readonly int _offset;

        public Bgr2432InputImage(Bgra32Image image)
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
            var baseIndex = this._offset + index * 4;
            return new Pixel(this._data[baseIndex + 2], this._data[baseIndex + 1], this._data[baseIndex], 255);
        }
    }
}
