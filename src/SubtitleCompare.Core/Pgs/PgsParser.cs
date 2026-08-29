using System.Buffers.Binary;
using SubtitleCompare.Core.Ocr;

namespace SubtitleCompare.Core.Pgs;

/// <summary>
/// Parses a raw HDMV Presentation Graphic Stream (<c>.sup</c>) into timed bitmaps.
/// Segment type IDs match the Blu-ray spec / ffmpeg (<c>0x14</c> PDS, <c>0x15</c> ODS,
/// <c>0x16</c> PCS, <c>0x17</c> WDS, <c>0x80</c> END).
/// </summary>
public static class PgsParser
{
    public const byte PaletteSegment = 0x14;
    public const byte ObjectSegment = 0x15;
    public const byte PresentationSegment = 0x16;
    public const byte WindowSegment = 0x17;
    public const byte EndSegment = 0x80;

    private const int HeaderSize = 13;
    private const int MaxDimension = 4096;
    private static readonly TimeSpan FallbackDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MinDuration = TimeSpan.FromMilliseconds(40);

    public static IReadOnlyList<PgsPresentation> ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = File.ReadAllBytes(path);
        return Parse(bytes);
    }

    public static IReadOnlyList<PgsPresentation> Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return Array.Empty<PgsPresentation>();
        if (data.Length < HeaderSize || data[0] != (byte)'P' || data[1] != (byte)'G')
            throw new InvalidOperationException("Not a PGS / .sup stream (missing PG header).");

        var segments = ReadSegments(data);
        return Compose(segments);
    }

    internal static IReadOnlyList<PgsSegment> ReadSegments(ReadOnlySpan<byte> data)
    {
        var list = new List<PgsSegment>();
        var offset = 0;
        while (offset + HeaderSize <= data.Length)
        {
            if (data[offset] != (byte)'P' || data[offset + 1] != (byte)'G')
                break;

            var pts = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 2, 4));
            var type = data[offset + 10];
            var size = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 11, 2));
            offset += HeaderSize;
            if (offset + size > data.Length)
                break;

            var payload = data.Slice(offset, size).ToArray();
            offset += size;
            list.Add(new PgsSegment(type, pts, payload));
        }

        return list;
    }

    private static IReadOnlyList<PgsPresentation> Compose(IReadOnlyList<PgsSegment> segments)
    {
        var objects = new Dictionary<int, PgsObject>();
        var palettes = new Dictionary<int, Rgba[]>();
        var results = new List<PgsPresentation>();
        TimeSpan? openStart = null;
        SubtitleBitmap? openBitmap = null;

        foreach (var set in GroupDisplaySets(segments))
        {
            var pcs = default(PcsInfo?);
            foreach (var seg in set)
            {
                if (seg.Type != PresentationSegment)
                    continue;
                if (TryReadPcs(seg, out var info))
                    pcs = info;
                break;
            }

            if (pcs is { } start && (start.CompositionState >> 6) != 0)
            {
                objects.Clear();
                palettes.Clear();
            }

            foreach (var seg in set)
            {
                if (seg.Type == PaletteSegment)
                    ApplyPalette(seg.Payload, palettes);
                else if (seg.Type == ObjectSegment)
                    ApplyObject(seg.Payload, objects);
            }

            if (pcs is not { } presentation)
                continue;

            var pts = PtsToTime(presentation.Pts);
            CloseOpen(results, ref openStart, ref openBitmap, pts);

            if (presentation.ObjectCount == 0 || presentation.Objects.Count == 0)
                continue;

            if (!palettes.TryGetValue(presentation.PaletteId, out var palette))
                continue;

            var bitmap = ComposeBitmap(presentation.Objects, objects, palette);
            if (bitmap is null || bitmap.IsEmpty)
                continue;

            openStart = pts;
            openBitmap = bitmap;
        }

        if (openStart is { } lastStart && openBitmap is not null)
        {
            var end = lastStart + FallbackDuration;
            results.Add(new PgsPresentation { Start = lastStart, End = end, Bitmap = openBitmap });
        }

        return results;
    }

    private static void CloseOpen(
        List<PgsPresentation> results,
        ref TimeSpan? openStart,
        ref SubtitleBitmap? openBitmap,
        TimeSpan end)
    {
        if (openStart is not { } start || openBitmap is null)
            return;

        if (end <= start)
            end = start + MinDuration;
        results.Add(new PgsPresentation { Start = start, End = end, Bitmap = openBitmap });
        openStart = null;
        openBitmap = null;
    }

    private static List<List<PgsSegment>> GroupDisplaySets(IReadOnlyList<PgsSegment> segments)
    {
        var sets = new List<List<PgsSegment>>();
        var current = new List<PgsSegment>();
        foreach (var seg in segments)
        {
            if (seg.Type == PresentationSegment && current.Count > 0)
            {
                sets.Add(current);
                current = [];
            }

            current.Add(seg);
            if (seg.Type == EndSegment && current.Count > 0)
            {
                sets.Add(current);
                current = [];
            }
        }

        if (current.Count > 0)
            sets.Add(current);
        return sets;
    }

    private static bool TryReadPcs(PgsSegment seg, out PcsInfo info)
    {
        info = default;
        var p = seg.Payload;
        if (p.Length < 11)
            return false;

        var o = 0;
        o += 2; // video width
        o += 2; // video height
        o += 1; // frame rate
        o += 2; // composition number
        var state = p[o++];
        o += 1; // palette update flag
        var paletteId = p[o++];
        var count = p[o++];
        var objects = new List<PcsObject>(count);
        for (var i = 0; i < count; i++)
        {
            if (o + 8 > p.Length)
                break;
            var id = BinaryPrimitives.ReadUInt16BigEndian(p.AsSpan(o, 2));
            o += 2;
            o += 1; // window id
            var flags = p[o++];
            var x = BinaryPrimitives.ReadUInt16BigEndian(p.AsSpan(o, 2));
            o += 2;
            var y = BinaryPrimitives.ReadUInt16BigEndian(p.AsSpan(o, 2));
            o += 2;
            if ((flags & 0x80) != 0)
            {
                if (o + 8 > p.Length)
                    break;
                o += 8; // crop rect; blit the full object like ffmpeg
            }

            objects.Add(new PcsObject(id, x, y));
        }

        info = new PcsInfo(seg.Pts, state, paletteId, count, objects);
        return true;
    }

    private static void ApplyPalette(byte[] payload, Dictionary<int, Rgba[]> palettes)
    {
        if (payload.Length < 2)
            return;
        var id = payload[0];
        if (!palettes.TryGetValue(id, out var colors))
        {
            colors = new Rgba[256];
            palettes[id] = colors;
        }

        var o = 2;
        while (o + 5 <= payload.Length)
        {
            var entry = payload[o++];
            var y = payload[o++];
            var cr = payload[o++];
            var cb = payload[o++];
            var a = payload[o++];
            colors[entry] = YCbCrToRgba(y, cr, cb, a);
        }
    }

    private static void ApplyObject(byte[] payload, Dictionary<int, PgsObject> objects)
    {
        if (payload.Length < 4)
            return;
        var id = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2));
        var seq = payload[3];

        if ((seq & 0x80) == 0)
        {
            if (!objects.TryGetValue(id, out var existing))
                return;
            existing.AppendRle(payload.AsSpan(4));
            return;
        }

        if (payload.Length < 11)
            return;
        var declared = (payload[4] << 16) | (payload[5] << 8) | payload[6];
        var rleLen = declared - 4;
        if (rleLen < 0)
            return;
        var width = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(7, 2));
        var height = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(9, 2));
        if (width == 0 || height == 0 || width > MaxDimension || height > MaxDimension)
            return;

        var rle = payload.AsSpan(11);
        objects[id] = new PgsObject(id, width, height, rle);
    }

    private static SubtitleBitmap? ComposeBitmap(
        IReadOnlyList<PcsObject> refs,
        Dictionary<int, PgsObject> objects,
        Rgba[] palette)
    {
        var decoded = new List<(PgsObject Obj, PcsObject Place, byte[] Indices)>();
        foreach (var place in refs)
        {
            if (!objects.TryGetValue(place.Id, out var obj))
                continue;
            var indices = obj.Decode();
            if (indices.Length == 0)
                continue;
            decoded.Add((obj, place, indices));
        }

        if (decoded.Count == 0)
            return null;

        var minX = decoded.Min(d => d.Place.X);
        var minY = decoded.Min(d => d.Place.Y);
        var maxX = decoded.Max(d => d.Place.X + d.Obj.Width);
        var maxY = decoded.Max(d => d.Place.Y + d.Obj.Height);
        if (minX < 0)
            minX = 0;
        if (minY < 0)
            minY = 0;
        var width = maxX - minX;
        var height = maxY - minY;
        if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
            return null;

        var rgba = new byte[width * height * 4];
        foreach (var (obj, place, indices) in decoded)
        {
            var ox = place.X - minX;
            var oy = place.Y - minY;
            for (var y = 0; y < obj.Height; y++)
            {
                var dy = oy + y;
                if ((uint)dy >= (uint)height)
                    continue;
                for (var x = 0; x < obj.Width; x++)
                {
                    var dx = ox + x;
                    if ((uint)dx >= (uint)width)
                        continue;
                    var color = palette[indices[y * obj.Width + x]];
                    if (color.A == 0)
                        continue;
                    var i = (dy * width + dx) * 4;
                    rgba[i] = color.R;
                    rgba[i + 1] = color.G;
                    rgba[i + 2] = color.B;
                    rgba[i + 3] = color.A;
                }
            }
        }

        return new SubtitleBitmap(width, height, rgba);
    }

    internal static TimeSpan PtsToTime(uint pts) =>
        TimeSpan.FromTicks(pts * TimeSpan.TicksPerSecond / 90_000);

    internal static Rgba YCbCrToRgba(byte y, byte cr, byte cb, byte a)
    {
        var Y = y;
        var Cr = cr - 128.0;
        var Cb = cb - 128.0;
        var r = Clamp(Y + 1.402 * Cr);
        var g = Clamp(Y - 0.344136 * Cb - 0.714136 * Cr);
        var b = Clamp(Y + 1.772 * Cb);
        return new Rgba(r, g, b, a);
    }

    private static byte Clamp(double v) =>
        (byte)Math.Clamp((int)Math.Round(v), 0, 255);

    internal readonly record struct PgsSegment(byte Type, uint Pts, byte[] Payload);

    private readonly record struct PcsInfo(
        uint Pts,
        byte CompositionState,
        int PaletteId,
        int ObjectCount,
        List<PcsObject> Objects);

    private readonly record struct PcsObject(int Id, int X, int Y);

    internal readonly record struct Rgba(byte R, byte G, byte B, byte A);

    private sealed class PgsObject
    {
        private readonly List<byte> _rle = [];
        private byte[]? _indices;

        public PgsObject(int id, int width, int height, ReadOnlySpan<byte> first)
        {
            Id = id;
            Width = width;
            Height = height;
            _rle.AddRange(first.ToArray());
        }

        public int Id { get; }
        public int Width { get; }
        public int Height { get; }

        public void AppendRle(ReadOnlySpan<byte> more) => _rle.AddRange(more.ToArray());

        public byte[] Decode()
        {
            if (_indices is not null)
                return _indices;
            _indices = DecodeRle(_rle, Width, Height);
            return _indices;
        }
    }

    internal static byte[] DecodeRle(IReadOnlyList<byte> rle, int width, int height)
    {
        var dest = new byte[width * height];
        var i = 0;
        var pixel = 0;
        var line = 0;
        var limit = width * height;

        while (i < rle.Count && line < height)
        {
            var color = rle[i++];
            var run = 1;
            if (color == 0)
            {
                if (i >= rle.Count)
                    break;
                var flags = rle[i++];
                run = flags & 0x3F;
                if ((flags & 0x40) != 0)
                {
                    if (i >= rle.Count)
                        break;
                    run = (run << 8) | rle[i++];
                }

                color = (flags & 0x80) != 0
                    ? i < rle.Count ? rle[i++] : (byte)0
                    : (byte)0;
            }

            if (run > 0)
            {
                var remaining = limit - pixel;
                if (run > remaining)
                    run = remaining;
                if (run <= 0)
                    break;
                dest.AsSpan(pixel, run).Fill(color);
                pixel += run;
            }
            else
            {
                line++;
                if (pixel % width != 0)
                    pixel = line * width;
            }
        }

        return dest;
    }
}
