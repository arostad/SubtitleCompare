using System.Text;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Parsing;

public sealed class AssParser
{
    public ParsedSubtitles Parse(string content, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        content = StripBom(content).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = content.Split('\n');

        var formatCols = new List<string>
        {
            "Layer", "Start", "End", "Style", "Name",
            "MarginL", "MarginR", "MarginV", "Effect", "Text",
        };

        var inEvents = false;
        var cues = new List<SubtitleCue>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith("!:"))
                continue;

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                inEvents = trimmed.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inEvents)
                continue;

            if (trimmed.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
            {
                var spec = trimmed["Format:".Length..];
                formatCols = spec.Split(',').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
                continue;
            }

            if (!trimmed.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = line[(line.IndexOf(':') + 1)..];
            if (payload.StartsWith(' '))
                payload = payload[1..];

            if (!TryReadDialogue(payload, formatCols, out var start, out var end, out var text))
                continue;

            var display = AssTagStripper.Strip(text).TrimEnd();
            cues.Add(new SubtitleCue
            {
                Index = cues.Count + 1,
                Start = start,
                End = end,
                Text = display,
                RawText = text,
            });
        }

        return new ParsedSubtitles
        {
            Format = "ass",
            Cues = cues,
            SourcePath = sourcePath,
        };
    }

    public ParsedSubtitles ParseFile(string path) =>
        Parse(File.ReadAllText(path, Encoding.UTF8), path);

    private static bool TryReadDialogue(
        string payload,
        List<string> formatCols,
        out TimeSpan start,
        out TimeSpan end,
        out string text)
    {
        start = default;
        end = default;
        text = "";

        var startIdx = IndexOf(formatCols, "Start");
        var endIdx = IndexOf(formatCols, "End");
        var textIdx = IndexOf(formatCols, "Text");
        if (startIdx < 0 || endIdx < 0)
            return false;
        if (textIdx < 0)
            textIdx = formatCols.Count - 1;

        // Text is everything after the textIdx-th comma; earlier fields cannot contain unescaped commas.
        var fields = SplitAssFields(payload, textIdx);
        if (fields.Count <= Math.Max(startIdx, endIdx))
            return false;

        if (!SubtitleTimeParser.TryParseAss(fields[startIdx], out start))
            return false;
        if (!SubtitleTimeParser.TryParseAss(fields[endIdx], out end))
            return false;

        text = textIdx < fields.Count ? fields[textIdx] : "";
        return true;
    }

    private static List<string> SplitAssFields(string payload, int lastFieldIndex)
    {
        var fields = new List<string>();
        var start = 0;
        for (var n = 0; n < lastFieldIndex; n++)
        {
            var comma = payload.IndexOf(',', start);
            if (comma < 0)
            {
                fields.Add(payload[start..]);
                return fields;
            }
            fields.Add(payload[start..comma]);
            start = comma + 1;
        }
        fields.Add(start <= payload.Length ? payload[start..] : "");
        return fields;
    }

    private static int IndexOf(List<string> cols, string name)
    {
        for (var i = 0; i < cols.Count; i++)
        {
            if (cols[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static string StripBom(string content) =>
        content.Length > 0 && content[0] == '\uFEFF' ? content[1..] : content;
}
