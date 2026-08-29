using SubtitleCompare.Core.Ocr;

namespace SubtitleCompare.Tests;

public class OcrImagePreprocessorTests
{
    [Fact]
    public void Light_on_transparent_becomes_black_ink()
    {
        var src = new byte[8 * 4 * 4];
        Paint(src, 8, 2, 1, 3, 1, 255, 255, 255, 255);
        Paint(src, 8, 3, 1, 4, 2, 255, 255, 255, 255);
        var prepared = OcrImagePreprocessor.Prepare(new SubtitleBitmap(8, 4, src), scale: 1, padding: 0);

        Assert.False(prepared.IsEmpty);
        Assert.Equal(0, prepared[0, 0]);
        Assert.Equal(0, prepared[1, 0]);
        Assert.Equal(255, prepared[0, 1]);
        Assert.Equal(0, prepared[1, 1]);
        Assert.True(Count(prepared, 0) >= 4);
        Assert.True(Count(prepared, 255) >= 1);
    }

    [Fact]
    public void Upscales_nearest_neighbor()
    {
        var src = new byte[2 * 1 * 4];
        Paint(src, 2, 0, 0, 0, 0, 255, 255, 255, 255);
        var prepared = OcrImagePreprocessor.Prepare(new SubtitleBitmap(2, 1, src), scale: 3, padding: 0);
        Assert.Equal(3, prepared.Width); // cropped to the single opaque pixel, then ×3
        Assert.Equal(3, prepared.Height);
        Assert.Equal(0, prepared[0, 0]);
        Assert.Equal(0, prepared[2, 2]);
    }

    [Fact]
    public void Dark_text_on_opaque_light_box_is_black_ink()
    {
        var src = new byte[6 * 3 * 4];
        Fill(src, 6, 3, 240, 240, 240, 255);
        Paint(src, 6, 2, 1, 3, 1, 20, 20, 20, 255);
        var prepared = OcrImagePreprocessor.Prepare(new SubtitleBitmap(6, 3, src), scale: 1, padding: 0);

        Assert.False(prepared.IsEmpty);
        Assert.Equal(0, prepared[2, 1]);
        Assert.Equal(255, prepared[0, 0]);
    }

    [Fact]
    public void Empty_or_fully_transparent_returns_empty()
    {
        Assert.True(OcrImagePreprocessor.Prepare(new SubtitleBitmap(4, 2, new byte[32]), scale: 2).IsEmpty);
        Assert.True(OcrImagePreprocessor.Prepare(new SubtitleBitmap(0, 0, []), scale: 2).IsEmpty);
    }

    [Fact]
    public void Bmp_header_is_readable_8bit()
    {
        var pixels = new byte[] { 0, 255, 255, 0 };
        var bmp = new BinaryImage(2, 2, pixels).ToBmp();
        Assert.Equal((byte)'B', bmp[0]);
        Assert.Equal((byte)'M', bmp[1]);
        Assert.Equal(8, bmp[28]); // biBitCount
        Assert.Equal(2, BitConverter.ToInt32(bmp, 18));
        Assert.Equal(2, BitConverter.ToInt32(bmp, 22));
    }

    [Fact]
    public void Cue_builder_uses_recognize_and_normalizes()
    {
        var rgba = new byte[4];
        rgba[0] = rgba[1] = rgba[2] = rgba[3] = 255;
        var presentations = new[]
        {
            new SubtitleCompare.Core.Pgs.PgsPresentation
            {
                Start = TimeSpan.FromSeconds(1),
                End = TimeSpan.FromSeconds(2),
                Bitmap = new SubtitleBitmap(1, 1, rgba),
            },
        };

        var parsed = OcrCueBuilder.Build(presentations, _ => "  Hello   there \n\n");
        Assert.Equal("ocr-pgs", parsed.Format);
        Assert.Single(parsed.Cues);
        Assert.Equal("Hello there", parsed.Cues[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(1), parsed.Cues[0].Start);
    }

    [Fact]
    public void Normalize_trims_and_drops_blank_lines()
    {
        Assert.Equal("A\nB", OcrCueBuilder.Normalize("\n A  \n\n B \n"));
        Assert.Equal("", OcrCueBuilder.Normalize("   "));
    }

    private static void Paint(byte[] rgba, int width, int x0, int y0, int x1, int y1, byte r, byte g, byte b, byte a)
    {
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var i = (y * width + x) * 4;
                rgba[i] = r;
                rgba[i + 1] = g;
                rgba[i + 2] = b;
                rgba[i + 3] = a;
            }
        }
    }

    private static void Fill(byte[] rgba, int width, int height, byte r, byte g, byte b, byte a) =>
        Paint(rgba, width, 0, 0, width - 1, height - 1, r, g, b, a);

    private static int Count(BinaryImage image, byte value)
    {
        var n = 0;
        foreach (var p in image.Pixels)
        {
            if (p == value)
                n++;
        }

        return n;
    }
}
