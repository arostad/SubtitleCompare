using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Parsing;

/// <summary>
/// Auto-detects SRT / VTT / ASS from content (preferred) or file extension.
/// </summary>
public static class SubtitleParser
{
    public static ParsedSubtitles Parse(string content, string? fileNameOrPath = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var format = Detect(content, fileNameOrPath);
        return format switch
        {
            "vtt" => new VttParser().Parse(content, fileNameOrPath),
            "ass" => new AssParser().Parse(content, fileNameOrPath),
            _ => new SrtParser().Parse(content, fileNameOrPath),
        };
    }

    public static ParsedSubtitles ParseFile(string path)
    {
        var content = File.ReadAllText(path);
        return Parse(content, path);
    }

    public static string Detect(string content, string? fileNameOrPath = null)
    {
        var head = content.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (head.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
            return "vtt";
        if (LooksLikeAss(content))
            return "ass";

        var ext = Path.GetExtension(fileNameOrPath ?? "").ToLowerInvariant();
        return ext switch
        {
            ".vtt" or ".webvtt" => "vtt",
            ".ass" or ".ssa" => "ass",
            _ => "srt",
        };
    }

    private static bool LooksLikeAss(string content)
    {
        var hasEvents = content.Contains("[Events]", StringComparison.OrdinalIgnoreCase);
        var hasDialogue = content.Contains("Dialogue:", StringComparison.OrdinalIgnoreCase);
        var hasScript = content.Contains("[Script Info]", StringComparison.OrdinalIgnoreCase)
                        || content.Contains("ScriptType:", StringComparison.OrdinalIgnoreCase);
        return (hasEvents && hasDialogue) || (hasScript && hasDialogue);
    }
}
