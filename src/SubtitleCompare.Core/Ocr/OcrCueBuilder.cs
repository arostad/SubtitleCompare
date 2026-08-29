using System.Globalization;
using SubtitleCompare.Core.Models;
using SubtitleCompare.Core.Pgs;

namespace SubtitleCompare.Core.Ocr;

/// <summary>
/// Runs a recognize callback on each composed PGS bitmap and builds timed cues.
/// Tesseract itself is not thread-safe; pass a thread-safe recognize (a pool)
/// when <paramref name="maxDegreeOfParallelism"/> is greater than 1.
/// </summary>
public static class OcrCueBuilder
{
    public const int MaxWorkers = 4;
    public const int MinCuesForParallel = 8;

    public static ParsedSubtitles Build(
        IReadOnlyList<PgsPresentation> presentations,
        Func<BinaryImage, string> recognize,
        IProgress<OcrProgress>? progress = null,
        CancellationToken cancellationToken = default,
        int maxDegreeOfParallelism = 1)
    {
        ArgumentNullException.ThrowIfNull(presentations);
        ArgumentNullException.ThrowIfNull(recognize);
        if (maxDegreeOfParallelism < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

        var total = presentations.Count;
        if (total == 0)
        {
            progress?.Report(new OcrProgress(0, 0, Format(0, 0)));
            return new ParsedSubtitles { Format = "ocr-pgs", Cues = Array.Empty<SubtitleCue>() };
        }

        var workers = WorkerCount(total, maxDegreeOfParallelism);
        progress?.Report(new OcrProgress(0, total, Format(0, total)));

        var cues = workers == 1
            ? BuildSerial(presentations, recognize, progress, cancellationToken)
            : BuildParallel(presentations, recognize, progress, cancellationToken, workers);

        return new ParsedSubtitles
        {
            Format = "ocr-pgs",
            Cues = cues,
        };
    }

    /// <summary>
    /// How many OCR workers to use. One engine per worker; stay serial
    /// on short tracks so engine startup is not worse than the OCR itself.
    /// Cap at <see cref="MaxWorkers"/> so tessdata stays off the heap.
    /// </summary>
    public static int WorkerCount(int cueCount, int requested = MaxWorkers)
    {
        if (cueCount < MinCuesForParallel || requested <= 1)
            return 1;
        return Math.Clamp(Math.Min(requested, cueCount), 1, MaxWorkers);
    }

    public static int WorkerCountForMachine(int cueCount) =>
        WorkerCount(cueCount, Environment.ProcessorCount);

    /// <summary>
    /// Cue count on the overlay and status bar. Example: <c>OCR 120 / 800</c>.
    /// </summary>
    public static string Format(int current, int total)
    {
        if (total <= 0)
            return "OCR…";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"OCR {current} / {total}");
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var t = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var lines = t.Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var cleaned = CollapseSpaces(line.Trim());
            if (cleaned.Length > 0)
                kept.Add(cleaned);
        }

        return string.Join('\n', kept);
    }

    private static SubtitleCue[] BuildSerial(
        IReadOnlyList<PgsPresentation> presentations,
        Func<BinaryImage, string> recognize,
        IProgress<OcrProgress>? progress,
        CancellationToken cancellationToken)
    {
        var cues = new SubtitleCue[presentations.Count];
        for (var i = 0; i < presentations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            cues[i] = RecognizeOne(presentations[i], i, recognize);
            progress?.Report(new OcrProgress(i + 1, presentations.Count, Format(i + 1, presentations.Count)));
        }

        return cues;
    }

    private static SubtitleCue[] BuildParallel(
        IReadOnlyList<PgsPresentation> presentations,
        Func<BinaryImage, string> recognize,
        IProgress<OcrProgress>? progress,
        CancellationToken cancellationToken,
        int workers)
    {
        var cues = new SubtitleCue[presentations.Count];
        var done = 0;
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = workers,
            CancellationToken = cancellationToken,
        };

        Parallel.For(0, presentations.Count, options, i =>
        {
            cues[i] = RecognizeOne(presentations[i], i, recognize);
            var current = Interlocked.Increment(ref done);
            progress?.Report(new OcrProgress(current, presentations.Count, Format(current, presentations.Count)));
        });

        return cues;
    }

    private static SubtitleCue RecognizeOne(
        PgsPresentation presentation,
        int index,
        Func<BinaryImage, string> recognize)
    {
        var prepared = OcrImagePreprocessor.Prepare(presentation.Bitmap);
        string text;
        try
        {
            text = prepared.IsEmpty ? "" : recognize(prepared) ?? "";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            text = "";
        }

        text = Normalize(text);
        return new SubtitleCue
        {
            Index = index + 1,
            Start = presentation.Start,
            End = presentation.End,
            Text = text,
            RawText = text,
        };
    }

    private static string CollapseSpaces(string line)
    {
        if (!line.Contains("  ", StringComparison.Ordinal))
            return line;
        var chars = new char[line.Length];
        var n = 0;
        var wasSpace = false;
        foreach (var c in line)
        {
            var space = c is ' ' or '\t';
            if (space)
            {
                if (wasSpace)
                    continue;
                chars[n++] = ' ';
                wasSpace = true;
            }
            else
            {
                chars[n++] = c;
                wasSpace = false;
            }
        }

        return new string(chars, 0, n);
    }
}
