using System.Buffers.Binary;
using SubtitleCompare.Core.Pgs;

namespace SubtitleCompare.Tests;

public class PgsParserTests
{
    private const uint OneSecond = 90_000;
    private const byte White = 1;
    private const byte Clear = 0;

    [Fact]
    public void Parse_rejects_non_pgs()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PgsParser.Parse("not a sup"u8.ToArray()));
        Assert.Contains("PGS", ex.Message);
    }

    [Fact]
    public void Parse_empty_returns_no_cues()
    {
        Assert.Empty(PgsParser.Parse(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Parse_roundtrip_timestamps_and_pixels()
    {
        var pixels = new byte[]
        {
            White, White, Clear, Clear,
            White, Clear, White, Clear,
        };
        var sup = PgsTestFile.Build(
            new PgsTestCue(OneSecond, 2 * OneSecond, 10, 20, 4, 2, pixels),
            new PgsTestCue(3 * OneSecond, 4 * OneSecond, 0, 0, 2, 1, [White, White]));

        var cues = PgsParser.Parse(sup);
        Assert.Equal(2, cues.Count);

        Assert.Equal(TimeSpan.FromSeconds(1), cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2), cues[0].End);
        Assert.Equal(4, cues[0].Bitmap.Width);
        Assert.Equal(2, cues[0].Bitmap.Height);

        AssertWhite(cues[0].Bitmap, 0, 0);
        AssertWhite(cues[0].Bitmap, 1, 0);
        AssertClear(cues[0].Bitmap, 2, 0);
        AssertWhite(cues[0].Bitmap, 0, 1);
        AssertClear(cues[0].Bitmap, 1, 1);
        AssertWhite(cues[0].Bitmap, 2, 1);

        Assert.Equal(TimeSpan.FromSeconds(3), cues[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(4), cues[1].End);
        Assert.Equal(2, cues[1].Bitmap.Width);
        Assert.Equal(1, cues[1].Bitmap.Height);
    }

    [Fact]
    public void Parse_uses_next_composition_as_end_when_clear_is_missing()
    {
        var first = PgsTestFile.DisplaySet(
            pts: OneSecond,
            epochStart: true,
            objects: [new PcsRef(0, 0, 0)],
            palette: true,
            objectId: 0,
            width: 2,
            height: 1,
            pixels: [White, White],
            clear: false);
        var second = PgsTestFile.DisplaySet(
            pts: 2 * OneSecond,
            epochStart: false,
            objects: [new PcsRef(0, 0, 0)],
            palette: false,
            objectId: 0,
            width: 2,
            height: 1,
            pixels: [White, Clear],
            clear: false);

        var cues = PgsParser.Parse(Concat(first, second));
        Assert.Equal(2, cues.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2), cues[0].End);
        Assert.Equal(TimeSpan.FromSeconds(2), cues[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(7), cues[1].End); // +5s fallback
    }

    [Fact]
    public void Parse_reassembles_fragmented_ods()
    {
        var pixels = new byte[]
        {
            White, White, White, White,
            White, Clear, Clear, White,
        };
        var rle = PgsTestFile.EncodeRle(4, 2, pixels);
        Assert.True(rle.Length > 4, "need a run long enough to split");

        var split = rle.Length / 2;
        var first = rle[..split];
        var rest = rle[split..];

        var bytes = new List<byte>();
        bytes.AddRange(PgsTestFile.Pcs(OneSecond, epochStart: true, objects: [new PcsRef(1, 5, 6)]));
        bytes.AddRange(PgsTestFile.Pds(OneSecond));
        bytes.AddRange(PgsTestFile.OdsFragment(OneSecond, 1, 4, 2, rle.Length, first, firstInSequence: true, lastInSequence: false));
        bytes.AddRange(PgsTestFile.OdsFragment(OneSecond, 1, 4, 2, rle.Length, rest, firstInSequence: false, lastInSequence: true));
        bytes.AddRange(PgsTestFile.End(OneSecond));
        bytes.AddRange(PgsTestFile.Pcs(2 * OneSecond, epochStart: false, objects: []));
        bytes.AddRange(PgsTestFile.End(2 * OneSecond));

        var cues = PgsParser.Parse(bytes.ToArray());
        Assert.Single(cues);
        Assert.Equal(4, cues[0].Bitmap.Width);
        Assert.Equal(2, cues[0].Bitmap.Height);
        AssertWhite(cues[0].Bitmap, 0, 0);
        AssertWhite(cues[0].Bitmap, 3, 1);
        AssertClear(cues[0].Bitmap, 1, 1);
    }

    [Fact]
    public void ParseFile_reads_on_disk_sup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pgs-{Guid.NewGuid():N}.sup");
        try
        {
            File.WriteAllBytes(path, PgsTestFile.Build(new PgsTestCue(OneSecond, 2 * OneSecond, 0, 0, 2, 1, [White, White])));
            var cues = PgsParser.ParseFile(path);
            Assert.Single(cues);
            Assert.Equal(TimeSpan.FromSeconds(1), cues[0].Start);
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    private static void AssertWhite(SubtitleCompare.Core.Ocr.SubtitleBitmap bitmap, int x, int y)
    {
        var px = bitmap.Pixel(x, y);
        Assert.True(px.A > 200, $"expected opaque at {x},{y} but A={px.A}");
        Assert.True(px.R > 240 && px.G > 240 && px.B > 240, $"expected white at {x},{y} got {px.R},{px.G},{px.B}");
    }

    private static void AssertClear(SubtitleCompare.Core.Ocr.SubtitleBitmap bitmap, int x, int y)
    {
        Assert.Equal(0, bitmap.Pixel(x, y).A);
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var n = parts.Sum(p => p.Length);
        var dest = new byte[n];
        var o = 0;
        foreach (var p in parts)
        {
            p.CopyTo(dest, o);
            o += p.Length;
        }

        return dest;
    }
}

internal readonly record struct PgsTestCue(
    uint StartPts,
    uint EndPts,
    int X,
    int Y,
    int Width,
    int Height,
    byte[] Pixels);

internal readonly record struct PcsRef(int Id, int X, int Y);

internal static class PgsTestFile
{
    public static byte[] Build(params PgsTestCue[] cues)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < cues.Length; i++)
        {
            var cue = cues[i];
            bytes.AddRange(DisplaySet(
                cue.StartPts,
                epochStart: i == 0,
                objects: [new PcsRef(i, cue.X, cue.Y)],
                palette: i == 0,
                objectId: i,
                width: cue.Width,
                height: cue.Height,
                pixels: cue.Pixels,
                clear: false));
            bytes.AddRange(Pcs(cue.EndPts, epochStart: false, objects: []));
            bytes.AddRange(End(cue.EndPts));
        }

        return bytes.ToArray();
    }

    public static byte[] DisplaySet(
        uint pts,
        bool epochStart,
        IReadOnlyList<PcsRef> objects,
        bool palette,
        int objectId,
        int width,
        int height,
        byte[] pixels,
        bool clear)
    {
        var bytes = new List<byte>();
        bytes.AddRange(Pcs(pts, epochStart, objects));
        if (palette)
            bytes.AddRange(Pds(pts));
        if (!clear)
            bytes.AddRange(Ods(pts, objectId, width, height, pixels));
        bytes.AddRange(End(pts));
        return bytes.ToArray();
    }

    public static byte[] Pcs(uint pts, bool epochStart, IReadOnlyList<PcsRef> objects)
    {
        var payload = new byte[11 + objects.Count * 8];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0), 1920);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2), 1080);
        payload[4] = 0x10;
        payload[7] = epochStart ? (byte)0x80 : (byte)0x00;
        payload[9] = 0; // palette id
        payload[10] = (byte)objects.Count;
        var o = 11;
        foreach (var obj in objects)
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(o), (ushort)obj.Id);
            o += 2;
            payload[o++] = 0;
            payload[o++] = 0;
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(o), (ushort)obj.X);
            o += 2;
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(o), (ushort)obj.Y);
            o += 2;
        }

        return Segment(pts, PgsParser.PresentationSegment, payload);
    }

    public static byte[] Pds(uint pts)
    {
        var payload = new byte[2 + 10];
        payload[0] = 0;
        payload[1] = 0;
        payload[2] = 0;
        payload[3] = 0;
        payload[4] = 128;
        payload[5] = 128;
        payload[6] = 0;
        payload[7] = 1;
        payload[8] = 255;
        payload[9] = 128;
        payload[10] = 128;
        payload[11] = 255;
        return Segment(pts, PgsParser.PaletteSegment, payload);
    }

    public static byte[] Ods(uint pts, int id, int width, int height, byte[] pixels)
    {
        var rle = EncodeRle(width, height, pixels);
        return OdsFragment(pts, id, width, height, rle.Length, rle, firstInSequence: true, lastInSequence: true);
    }

    public static byte[] OdsFragment(
        uint pts,
        int id,
        int width,
        int height,
        int fullRleLength,
        byte[] rlePart,
        bool firstInSequence,
        bool lastInSequence)
    {
        byte seq = 0;
        if (firstInSequence) seq |= 0x80;
        if (lastInSequence) seq |= 0x40;

        if (!firstInSequence)
        {
            var cont = new byte[4 + rlePart.Length];
            BinaryPrimitives.WriteUInt16BigEndian(cont.AsSpan(0), (ushort)id);
            cont[2] = 0;
            cont[3] = seq;
            rlePart.CopyTo(cont, 4);
            return Segment(pts, PgsParser.ObjectSegment, cont);
        }

        var payload = new byte[11 + rlePart.Length];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0), (ushort)id);
        payload[2] = 0;
        payload[3] = seq;
        var dataLen = 4 + fullRleLength;
        payload[4] = (byte)(dataLen >> 16);
        payload[5] = (byte)(dataLen >> 8);
        payload[6] = (byte)dataLen;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(7), (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(9), (ushort)height);
        rlePart.CopyTo(payload, 11);
        return Segment(pts, PgsParser.ObjectSegment, payload);
    }

    public static byte[] End(uint pts) => Segment(pts, PgsParser.EndSegment, []);

    public static byte[] EncodeRle(int width, int height, byte[] pixels)
    {
        var dest = new List<byte>(pixels.Length + height * 2);
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            var x = 0;
            while (x < width)
            {
                var color = pixels[row + x];
                var run = 1;
                while (x + run < width && pixels[row + x + run] == color && run < 16383)
                    run++;
                WriteRun(dest, color, run);
                x += run;
            }

            dest.Add(0);
            dest.Add(0);
        }

        return dest.ToArray();
    }

    private static void WriteRun(List<byte> dest, byte color, int run)
    {
        if (color != 0 && run == 1)
        {
            dest.Add(color);
            return;
        }

        dest.Add(0);
        if (color == 0)
        {
            if (run < 64)
            {
                dest.Add((byte)run);
            }
            else
            {
                dest.Add((byte)(0x40 | (run >> 8)));
                dest.Add((byte)run);
            }

            return;
        }

        if (run < 64)
        {
            dest.Add((byte)(0x80 | run));
            dest.Add(color);
        }
        else
        {
            dest.Add((byte)(0xC0 | (run >> 8)));
            dest.Add((byte)run);
            dest.Add(color);
        }
    }

    private static byte[] Segment(uint pts, byte type, byte[] payload)
    {
        var buf = new byte[13 + payload.Length];
        buf[0] = (byte)'P';
        buf[1] = (byte)'G';
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(2), pts);
        buf[10] = type;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(11), (ushort)payload.Length);
        payload.CopyTo(buf.AsSpan(13));
        return buf;
    }
}
