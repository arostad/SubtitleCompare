using System.Collections.Concurrent;
using System.IO;
using SubtitleCompare.Core.Ffmpeg;
using SubtitleCompare.Core.Models;
using SubtitleCompare.Core.Ocr;
using SubtitleCompare.Core.Pgs;

namespace SubtitleCompare.App.Ocr;

/// <summary>
/// Extracts a PGS stream, OCRs each cue, and caches the result for the temp session.
/// </summary>
internal sealed class ImageSubtitleLoader
{
    private readonly FfmpegExtract _extractor;
    private readonly ConcurrentDictionary<string, ParsedSubtitles> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSubtitleLoader(FfmpegExtract extractor)
    {
        _extractor = extractor;
    }

    public ParsedSubtitles Load(
        string filePath,
        SubtitleTrackInfo track,
        IProgress<string> status,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(track);

        var key = CacheKey(filePath, track.Index);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        status.Report("Extracting image subtitles…");
        var sup = _extractor.ExtractRaw(filePath, track.Index);
        cancellationToken.ThrowIfCancellationRequested();

        status.Report("Parsing PGS…");
        var presentations = PgsParser.ParseFile(sup);
        if (presentations.Count == 0)
        {
            var empty = new ParsedSubtitles { Format = "ocr-pgs", Cues = Array.Empty<SubtitleCue>(), SourcePath = sup };
            _cache[key] = empty;
            return empty;
        }

        var requested = TessLanguage.FromTag(track.Language);
        var language = TessdataStore.EnsureLanguageAsync(requested, status, cancellationToken)
            .GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();

        status.Report("Starting OCR…");
        using var engine = TesseractOcrEngine.Create(TessdataStore.DataPrefix, language);
        var parsed = OcrCueBuilder.Build(
            presentations,
            engine.Recognize,
            new Progress<OcrProgress>(p => status.Report(p.Message)),
            cancellationToken);

        parsed = new ParsedSubtitles
        {
            Format = parsed.Format,
            Cues = parsed.Cues,
            SourcePath = sup,
        };
        _cache[key] = parsed;
        return parsed;
    }

    private static string CacheKey(string filePath, int subtitleStreamIndex)
    {
        var full = Path.GetFullPath(filePath);
        var stamp = File.GetLastWriteTimeUtc(full).Ticks;
        var size = new FileInfo(full).Length;
        return $"{full}|{stamp}|{size}|{subtitleStreamIndex}|ocr";
    }
}
