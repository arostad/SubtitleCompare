namespace SubtitleCompare.Core.Models;

public sealed class ParsedSubtitles
{
    public string Format { get; init; } = "";
    public IReadOnlyList<SubtitleCue> Cues { get; init; } = Array.Empty<SubtitleCue>();
    public string? SourcePath { get; init; }
}
