using System.Text;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Parsing;

public sealed class SrtParser
{
    public ParsedSubtitles Parse(string content, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        content = StripBom(content).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = content.Split('\n');
        var cues = new List<SubtitleCue>();
        var i = 0;
        while (i < lines.Length)
        {
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                i++;
            if (i >= lines.Length)
                break;

            // Optional numeric index
            var indexLine = lines[i].Trim();
            var cueIndex = cues.Count + 1;
            if (int.TryParse(indexLine, out var parsedIndex) && i + 1 < lines.Length && IsTimingLine(lines[i + 1]))
            {
                cueIndex = parsedIndex;
                i++;
            }

            if (i >= lines.Length || !TrySplitTiming(lines[i], out var start, out var end))
            {
                // Skip a garbage line rather than aborting the whole file.
                i++;
                continue;
            }

            i++;
            var textLines = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                textLines.Add(lines[i]);
                i++;
            }

            var raw = string.Join("\n", textLines);
            var display = AssTagStripper.Strip(raw).TrimEnd();
            cues.Add(new SubtitleCue
            {
                Index = cueIndex,
                Start = start,
                End = end,
                Text = display,
                RawText = raw,
            });
        }

        return new ParsedSubtitles
        {
            Format = "srt",
            Cues = cues,
            SourcePath = sourcePath,
        };
    }

    public ParsedSubtitles ParseFile(string path) =>
        Parse(File.ReadAllText(path, Encoding.UTF8), path);

    private static bool IsTimingLine(string line) =>
        line.Contains("-->", StringComparison.Ordinal);

    private static bool TrySplitTiming(string line, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;
        var arrow = line.IndexOf("-->", StringComparison.Ordinal);
        if (arrow < 0)
            return false;

        var left = line[..arrow].Trim();
        var right = line[(arrow + 3)..].Trim();
        // Drop optional SRT coordinates / VTT cue settings after the end time.
        var space = right.IndexOf(' ');
        if (space >= 0)
            right = right[..space];

        return SubtitleTimeParser.TryParseSrtOrVtt(left, out start)
               && SubtitleTimeParser.TryParseSrtOrVtt(right, out end);
    }

    private static string StripBom(string content) =>
        content.Length > 0 && content[0] == '\uFEFF' ? content[1..] : content;
}
