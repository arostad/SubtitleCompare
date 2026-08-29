using System.Text.RegularExpressions;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Analysis;

public enum SdhEvidence
{
    None,
    Metadata,
    Title,
    Heuristic,
}

public readonly record struct SdhAssessment(bool IsLikelySdh, SdhEvidence Evidence, string Label);

/// <summary>
/// SDH / HI detection: trust container flags and titles first, then a
/// density of bracket/paren (and music-note) cues in the text.
/// </summary>
public static class SdhDetector
{
    private static readonly Regex BracketOrParen = new(
        @"\[[^]]+\]|\([^)]+\)",
        RegexOptions.Compiled);

    private static readonly Regex TitleHint = new(
        @"\b(sdh|s\.d\.h|cc|hi|hearing[- ]?impaired|hard of hearing|captions?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static SdhAssessment Evaluate(SubtitleTrackInfo? track, IReadOnlyList<SubtitleCue>? cues)
    {
        if (track?.IsHearingImpaired == true)
            return new(true, SdhEvidence.Metadata, "SDH subtitle (marked in the file)");

        if (track is not null && !string.IsNullOrWhiteSpace(track.Title) && TitleHint.IsMatch(track.Title))
            return new(true, SdhEvidence.Title, "SDH subtitle (from track title)");

        var list = cues ?? Array.Empty<SubtitleCue>();
        if (list.Count == 0)
            return new(false, SdhEvidence.None, "");

        var marked = 0;
        foreach (var cue in list)
        {
            if (IsMarkedCue(cue.Text))
                marked++;
        }

        var ratio = marked / (double)list.Count;
        var likely = (marked >= 8 && ratio >= 0.10) || (marked >= 4 && ratio >= 0.25);
        if (!likely)
            return new(false, SdhEvidence.None, "");

        return new(true, SdhEvidence.Heuristic, "Potential SDH subtitle detected");
    }

    internal static bool IsMarkedCue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (text.Contains('♪') || text.Contains('♫'))
            return true;
        return BracketOrParen.IsMatch(text);
    }
}
