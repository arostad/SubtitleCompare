namespace SubtitleCompare.Core.Ocr;

/// <summary>
/// Turns a PGS RGBA bitmap into black-on-white ink that Tesseract can read.
/// Blu-ray fonts are usually outlined white on transparent; treating opaque
/// pixels as ink (then upscaling) is the main quality lever.
/// </summary>
public static class OcrImagePreprocessor
{
    public const int DefaultScale = 3;
    public const int DefaultPadding = 8;
    private const byte AlphaOpaque = 200;
    private const byte AlphaContent = 16;

    public static BinaryImage Prepare(SubtitleBitmap source, int scale = DefaultScale, int padding = DefaultPadding)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (scale < 1)
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (padding < 0)
            throw new ArgumentOutOfRangeException(nameof(padding));
        if (source.IsEmpty)
            return BinaryImage.Empty;

        if (!TryScan(source, out var left, out var top, out var right, out var bottom,
                out var content, out var opaque, out var lumaSum))
            return BinaryImage.Empty;

        left = Math.Max(0, left - padding);
        top = Math.Max(0, top - padding);
        right = Math.Min(source.Width - 1, right + padding);
        bottom = Math.Min(source.Height - 1, bottom + padding);

        var cw = right - left + 1;
        var ch = bottom - top + 1;
        var area = source.Width * source.Height;
        var sparseOnTransparent = opaque / (double)content < 0.85
                                  || content / (double)area < 0.85;
        byte[] ink;
        if (sparseOnTransparent)
        {
            ink = AlphaAsInk(source, left, top, cw, ch);
        }
        else
        {
            var gray = new byte[cw * ch];
            var rgba = source.Rgba;
            var srcStride = source.Width * 4;
            for (var y = 0; y < ch; y++)
            {
                var srcRow = (top + y) * srcStride + left * 4;
                var destRow = y * cw;
                for (var x = 0; x < cw; x++)
                {
                    var i = srcRow + x * 4;
                    gray[destRow + x] = rgba[i + 3] < AlphaContent
                        ? (byte)255
                        : Luma(rgba[i], rgba[i + 1], rgba[i + 2]);
                }
            }

            var lightPaper = lumaSum / (double)content > 128;
            ink = Threshold(gray, lightPaper);
        }

        return ScaleNearest(ink, cw, ch, scale);
    }

    /// <summary>
    /// One pass over the RGBA buffer: content bounds plus classify stats.
    /// </summary>
    private static bool TryScan(
        SubtitleBitmap source,
        out int left, out int top, out int right, out int bottom,
        out int content, out int opaque, out long lumaSum)
    {
        left = source.Width;
        top = source.Height;
        right = -1;
        bottom = -1;
        content = 0;
        opaque = 0;
        lumaSum = 0;

        var rgba = source.Rgba;
        var width = source.Width;
        var height = source.Height;
        var stride = width * 4;

        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var i = row + x * 4;
                var a = rgba[i + 3];
                if (a < AlphaContent)
                    continue;

                content++;
                lumaSum += Luma(rgba[i], rgba[i + 1], rgba[i + 2]);
                if (a >= AlphaOpaque)
                    opaque++;

                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
        }

        return content > 0 && right >= left && bottom >= top;
    }

    private static byte[] AlphaAsInk(SubtitleBitmap source, int left, int top, int width, int height)
    {
        var ink = new byte[width * height];
        var rgba = source.Rgba;
        var srcStride = source.Width * 4;
        for (var y = 0; y < height; y++)
        {
            var srcRow = (top + y) * srcStride + left * 4;
            var destRow = y * width;
            for (var x = 0; x < width; x++)
            {
                var a = rgba[srcRow + x * 4 + 3];
                ink[destRow + x] = a >= AlphaContent ? (byte)0 : (byte)255;
            }
        }

        return ink;
    }

    private static byte[] Threshold(byte[] gray, bool lightPaper)
    {
        var threshold = Otsu(gray);
        var ink = new byte[gray.Length];
        for (var i = 0; i < gray.Length; i++)
        {
            var isInk = lightPaper ? gray[i] < threshold : gray[i] > threshold;
            ink[i] = isInk ? (byte)0 : (byte)255;
        }

        return ink;
    }

    internal static byte Otsu(byte[] gray)
    {
        var hist = new int[256];
        foreach (var v in gray)
            hist[v]++;

        var total = gray.Length;
        long sum = 0;
        for (var i = 0; i < 256; i++)
            sum += i * hist[i];

        long sumB = 0;
        var wB = 0;
        var max = 0.0;
        var threshold = 128;
        for (var t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0)
                continue;
            var wF = total - wB;
            if (wF == 0)
                break;
            sumB += t * (long)hist[t];
            var mB = sumB / (double)wB;
            var mF = (sum - sumB) / (double)wF;
            var between = wB * (double)wF * (mB - mF) * (mB - mF);
            if (between >= max)
            {
                max = between;
                threshold = t;
            }
        }

        return (byte)threshold;
    }

    private static BinaryImage ScaleNearest(byte[] src, int width, int height, int scale)
    {
        if (scale == 1)
            return new BinaryImage(width, height, src);

        var dw = width * scale;
        var dh = height * scale;
        var dest = new byte[dw * dh];
        for (var y = 0; y < dh; y++)
        {
            var sy = y / scale;
            var srcRow = sy * width;
            var destRow = y * dw;
            for (var x = 0; x < dw; x++)
                dest[destRow + x] = src[srcRow + (x / scale)];
        }

        return new BinaryImage(dw, dh, dest);
    }

    private static byte Luma(byte r, byte g, byte b) =>
        (byte)Math.Clamp((int)(0.299 * r + 0.587 * g + 0.114 * b + 0.5), 0, 255);
}
