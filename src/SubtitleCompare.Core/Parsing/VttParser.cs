using System.Text;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Parsing;

public sealed class VttParser
{
    public ParsedSubtitles Parse(string content, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        content = StripBom(content).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = content.Split('\n');
        var i = 0;

        if (i < lines.Length && lines[i].StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
            i++;

        var cues = new List<SubtitleCue>();
        while (i < lines.Length)
        {
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                i++;
            if (i >= lines.Length)
                break;

            var line = lines[i];

            // NOTE / STYLE / REGION blocks
            if (IsBlockHeader(line, "NOTE") || IsBlockHeader(line, "STYLE") || IsBlockHeader(line, "REGION"))
            {
                i++;
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                    i++;
                continue;
            }

            // Optional cue identifier
            if (!IsTimingLine(line) && i + 1 < lines.Length && IsTimingLine(lines[i + 1]))
                i++;

            if (i >= lines.Length || !TrySplitTiming(lines[i], out var start, out var end))
            {
                i++;
                continue;
            }

            i++;
            var textLines = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                textLines.Add(StripVttTags(lines[i]));
                i++;
            }

            var raw = string.Join("\n", textLines);
            cues.Add(new SubtitleCue
            {
                Index = cues.Count + 1,
                Start = start,
                End = end,
                Text = raw.TrimEnd(),
                RawText = raw,
            });
        }

        return new ParsedSubtitles
        {
            Format = "vtt",
            Cues = cues,
            SourcePath = sourcePath,
        };
    }

    public ParsedSubtitles ParseFile(string path) =>
        Parse(File.ReadAllText(path, Encoding.UTF8), path);

    private static bool IsBlockHeader(string line, string name) =>
        line.StartsWith(name, StringComparison.OrdinalIgnoreCase)
        && (line.Length == name.Length || char.IsWhiteSpace(line[name.Length]));

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
        var space = right.IndexOfAny([' ', '\t']);
        if (space >= 0)
            right = right[..space];
        return SubtitleTimeParser.TryParseSrtOrVtt(left, out start)
               && SubtitleTimeParser.TryParseSrtOrVtt(right, out end);
    }

    private static string StripVttTags(string line)
    {
        // Drop cue payload tags like <c>, <i>, <00:00:01.000>, timestamps.
        var sb = new StringBuilder(line.Length);
        var i = 0;
        while (i < line.Length)
        {
            if (line[i] == '<')
            {
                var close = line.IndexOf('>', i + 1);
                if (close >= 0)
                {
                    i = close + 1;
                    continue;
                }
            }
            if (line[i] == '&')
            {
                if (line.AsSpan(i).StartsWith("&amp;", StringComparison.Ordinal)) { sb.Append('&'); i += 5; continue; }
                if (line.AsSpan(i).StartsWith("&lt;", StringComparison.Ordinal)) { sb.Append('<'); i += 4; continue; }
                if (line.AsSpan(i).StartsWith("&gt;", StringComparison.Ordinal)) { sb.Append('>'); i += 4; continue; }
                if (line.AsSpan(i).StartsWith("&nbsp;", StringComparison.Ordinal)) { sb.Append(' '); i += 6; continue; }
            }
            sb.Append(line[i]);
            i++;
        }
        return sb.ToString();
    }

    private static string StripBom(string content) =>
        content.Length > 0 && content[0] == '\uFEFF' ? content[1..] : content;
}
