using System.Text.RegularExpressions;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Analysis;

public enum KindSource
{
    None,
    Flag,
    Title,
    TitleAndFlag,
    Heuristic,
}

public readonly record struct KindAssessment(bool IsMatch, KindSource Source, string Label);

/// <summary>
/// SDH / forced detection: trust container flags and titles first, then a
/// density of bracket/paren (and music-note) cues for unmarked SDH.
/// </summary>
public static class SdhDetector
{
    private static readonly Regex BracketOrParen = new(
        @"\[[^]]+\]|\([^)]+\)",
        RegexOptions.Compiled);

    private static readonly Regex SdhTitleHint = new(
        @"\b(sdh|s\.d\.h|cc|hi|hearing[- ]?impaired|hard of hearing|captions?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ForcedTitleHint = new(
        @"\b(forced|force)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<string> Describe(SubtitleTrackInfo? track, IReadOnlyList<SubtitleCue>? cues)
    {
        var lines = new List<string>(2);
        var sdh = EvaluateSdh(track, cues);
        if (sdh.IsMatch)
            lines.Add(sdh.Label);
        var forced = EvaluateForced(track);
        if (forced.IsMatch)
            lines.Add(forced.Label);
        return lines;
    }

    public static KindAssessment Evaluate(SubtitleTrackInfo? track, IReadOnlyList<SubtitleCue>? cues) =>
        EvaluateSdh(track, cues);

    public static KindAssessment EvaluateSdh(SubtitleTrackInfo? track, IReadOnlyList<SubtitleCue>? cues)
    {
        var fromFlag = track?.IsHearingImpaired == true;
        var fromTitle = track is not null
            && !string.IsNullOrWhiteSpace(track.Title)
            && SdhTitleHint.IsMatch(track.Title);

        if (fromFlag || fromTitle)
            return Labeled("SDH subtitle", fromTitle, fromFlag);

        var list = cues ?? Array.Empty<SubtitleCue>();
        if (list.Count == 0)
            return new(false, KindSource.None, "");

        var marked = 0;
        foreach (var cue in list)
        {
            if (IsMarkedCue(cue.Text))
                marked++;
        }

        var ratio = marked / (double)list.Count;
        var likely = (marked >= 8 && ratio >= 0.10) || (marked >= 4 && ratio >= 0.25);
        if (!likely)
            return new(false, KindSource.None, "");

        return new(true, KindSource.Heuristic, "Potential SDH subtitle detected");
    }

    public static KindAssessment EvaluateForced(SubtitleTrackInfo? track)
    {
        var fromFlag = track?.IsForced == true;
        var fromTitle = track is not null
            && !string.IsNullOrWhiteSpace(track.Title)
            && ForcedTitleHint.IsMatch(track.Title);

        if (fromFlag || fromTitle)
            return Labeled("Forced subtitle", fromTitle, fromFlag);

        return new(false, KindSource.None, "");
    }

    private static KindAssessment Labeled(string kind, bool fromTitle, bool fromFlag)
    {
        if (fromTitle && fromFlag)
            return new(true, KindSource.TitleAndFlag, $"{kind} (from track title & flag)");
        if (fromTitle)
            return new(true, KindSource.Title, $"{kind} (from track title)");
        return new(true, KindSource.Flag, $"{kind} (from track flag)");
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
