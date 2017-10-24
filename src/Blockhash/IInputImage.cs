namespace WagahighChoices
{
    public interface IInputImage
    {
        int Width { get; }
        int Height { get; }
        Pixel GetPixel(int index);
    }
}
