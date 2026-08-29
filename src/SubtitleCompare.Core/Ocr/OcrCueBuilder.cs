using System.Globalization;
using SubtitleCompare.Core.Models;
using SubtitleCompare.Core.Pgs;

namespace SubtitleCompare.Core.Ocr;

/// <summary>
/// Runs a recognize callback on each composed PGS bitmap and builds timed cues.
/// </summary>
public static class OcrCueBuilder
{
    public static ParsedSubtitles Build(
        IReadOnlyList<PgsPresentation> presentations,
        Func<BinaryImage, string> recognize,
        IProgress<OcrProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presentations);
        ArgumentNullException.ThrowIfNull(recognize);

        var cues = new List<SubtitleCue>(presentations.Count);
        var total = presentations.Count;
        progress?.Report(new OcrProgress(0, total, Format(0, total)));

        for (var i = 0; i < presentations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var presentation = presentations[i];
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
            cues.Add(new SubtitleCue
            {
                Index = i + 1,
                Start = presentation.Start,
                End = presentation.End,
                Text = text,
                RawText = text,
            });
            progress?.Report(new OcrProgress(i + 1, total, Format(i + 1, total)));
        }

        return new ParsedSubtitles
        {
            Format = "ocr-pgs",
            Cues = cues,
        };
    }

    /// <summary>
    /// Fixed-width OCR line so digit growth does not reflow the status bar.
    /// Example: <c>OCR  12 of 400 (  3%)</c>.
    /// </summary>
    public static string Format(int current, int total)
    {
        if (total <= 0)
            return "OCR…";
        var pct = (int)Math.Round(100.0 * current / total);
        var width = total.ToString(CultureInfo.InvariantCulture).Length;
        var cur = current.ToString(CultureInfo.InvariantCulture).PadLeft(width);
        var pctText = pct.ToString(CultureInfo.InvariantCulture).PadLeft(3);
        return $"OCR {cur} of {total} ({pctText}%)";
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
