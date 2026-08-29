using System.Collections.Concurrent;
using System.IO;
using SubtitleCompare.Core.Ffmpeg;
using SubtitleCompare.Core.Models;
using SubtitleCompare.Core.Ocr;
using SubtitleCompare.Core.Pgs;
using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.App.Ocr;

/// <summary>
/// Extracts a PGS stream, OCRs each cue, and caches the result for the temp session.
/// </summary>
internal sealed class ImageSubtitleLoader
{
    private readonly FfmpegExtract _extractor;
    private readonly ConcurrentDictionary<string, ParsedSubtitles> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _gates = new(StringComparer.OrdinalIgnoreCase);

    public ImageSubtitleLoader(FfmpegExtract extractor)
    {
        _extractor = extractor;
    }

    public ParsedSubtitles Load(
        string filePath,
        SubtitleTrackInfo track,
        IProgress<OcrProgress> status,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(track);

        var key = CacheKey(filePath, track.Index);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var gate = _gates.GetOrAdd(key, _ => new object());
        lock (gate)
        {
            if (_cache.TryGetValue(key, out cached))
                return cached;

            var parsed = LoadUncached(filePath, track, status, cancellationToken);
            _cache[key] = parsed;
            return parsed;
        }
    }

    private ParsedSubtitles LoadUncached(
        string filePath,
        SubtitleTrackInfo track,
        IProgress<OcrProgress> status,
        CancellationToken cancellationToken)
    {
        status.Report(Busy(LoadSteps.PullingTrack));
        var sup = _extractor.ExtractRaw(filePath, track.Index);
        cancellationToken.ThrowIfCancellationRequested();

        status.Report(Busy(LoadSteps.ParsingPgs));
        var presentations = PgsParser.ParseFile(sup);
        if (presentations.Count == 0)
        {
            return new ParsedSubtitles { Format = "ocr-pgs", Cues = Array.Empty<SubtitleCue>(), SourcePath = sup };
        }

        var requested = TessLanguage.FromTag(track.Language);
        var language = TessdataStore.EnsureLanguageAsync(
                requested,
                new Progress<string>(msg => status.Report(Busy(msg))),
                cancellationToken)
            .GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();

        status.Report(Busy(LoadSteps.StartingOcr));
        var workers = OcrCueBuilder.WorkerCountForMachine(presentations.Count);
        using var pool = TesseractOcrPool.Create(TessdataStore.DataPrefix, language, workers);
        var parsed = OcrCueBuilder.Build(
            presentations,
            pool.Recognize,
            status,
            cancellationToken,
            maxDegreeOfParallelism: workers);

        return new ParsedSubtitles
        {
            Format = parsed.Format,
            Cues = parsed.Cues,
            SourcePath = sup,
        };
    }

    private static OcrProgress Busy(string message) => new(0, 0, message);

    private static string CacheKey(string filePath, int subtitleStreamIndex)
    {
        var full = Path.GetFullPath(filePath);
        var stamp = File.GetLastWriteTimeUtc(full).Ticks;
        var size = new FileInfo(full).Length;
        return $"{full}|{stamp}|{size}|{subtitleStreamIndex}|ocr";
    }
}
