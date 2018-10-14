using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace WagahighChoices.BlockhashCli
{
    internal readonly struct Rgba32InputImage : IInputImage
    {
        private readonly Image<Rgba32> _image;

        public Rgba32InputImage(Image<Rgba32> image)
        {
            this._image = image;
        }

        public int Width => this._image.Width;

        public int Height => this._image.Height;

        public Pixel GetPixel(int index)
        {
            var pixel = this._image.GetPixelSpan()[index];
            return new Pixel(pixel.R, pixel.G, pixel.B, pixel.A);
        }
    }
}
