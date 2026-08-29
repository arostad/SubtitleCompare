namespace SubtitleCompare.Core.Ocr;

/// <summary>
/// 8-bit image used as Tesseract input. Ink is 0 (black), paper is 255 (white).
/// </summary>
public sealed class BinaryImage
{
    public static BinaryImage Empty { get; } = new(0, 0, []);

    public BinaryImage(int width, int height, byte[] pixels)
    {
        if (width < 0 || height < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(pixels);
        var expected = checked(width * height);
        if (pixels.Length < expected)
            throw new ArgumentException($"Pixel buffer is {pixels.Length} bytes; expected at least {expected}.", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public byte this[int x, int y] => Pixels[y * Width + x];

    /// <summary>
    /// 8-bit grayscale BMP (bottom-up). Leptonica / Tesseract can load this from memory.
    /// </summary>
    public byte[] ToBmp()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Cannot encode an empty image.");

        var stride = (Width + 3) & ~3;
        var pixelBytes = stride * Height;
        var paletteBytes = 256 * 4;
        var headerSize = 14 + 40;
        var offBits = headerSize + paletteBytes;
        var fileSize = offBits + pixelBytes;
        var dest = new byte[fileSize];

        dest[0] = (byte)'B';
        dest[1] = (byte)'M';
        WriteU32(dest, 2, (uint)fileSize);
        WriteU32(dest, 10, (uint)offBits);

        WriteU32(dest, 14, 40);
        WriteI32(dest, 18, Width);
        WriteI32(dest, 22, Height);
        WriteU16(dest, 26, 1);
        WriteU16(dest, 28, 8);
        WriteU32(dest, 34, (uint)pixelBytes);
        WriteU32(dest, 46, 256);

        for (var i = 0; i < 256; i++)
        {
            var p = headerSize + i * 4;
            dest[p] = (byte)i;
            dest[p + 1] = (byte)i;
            dest[p + 2] = (byte)i;
        }

        for (var y = 0; y < Height; y++)
        {
            var srcY = Height - 1 - y;
            var src = srcY * Width;
            var dst = offBits + y * stride;
            Buffer.BlockCopy(Pixels, src, dest, dst, Width);
        }

        return dest;
    }

    private static void WriteU16(byte[] buf, int offset, ushort value)
    {
        buf[offset] = (byte)value;
        buf[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteU32(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)value;
        buf[offset + 1] = (byte)(value >> 8);
        buf[offset + 2] = (byte)(value >> 16);
        buf[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteI32(byte[] buf, int offset, int value) =>
        WriteU32(buf, offset, (uint)value);
}
