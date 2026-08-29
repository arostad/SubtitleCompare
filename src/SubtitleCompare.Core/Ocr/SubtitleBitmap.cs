namespace SubtitleCompare.Core.Ocr;

/// <summary>
/// Unpremultiplied 32-bit RGBA bitmap (row-major, 4 bytes per pixel).
/// </summary>
public sealed class SubtitleBitmap
{
    public SubtitleBitmap(int width, int height, byte[] rgba)
    {
        if (width < 0 || height < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(rgba);
        var expected = checked(width * height * 4);
        if (rgba.Length < expected)
            throw new ArgumentException($"RGBA buffer is {rgba.Length} bytes; expected at least {expected}.", nameof(rgba));

        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Rgba { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public (byte R, byte G, byte B, byte A) Pixel(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (Rgba[i], Rgba[i + 1], Rgba[i + 2], Rgba[i + 3]);
    }
}
