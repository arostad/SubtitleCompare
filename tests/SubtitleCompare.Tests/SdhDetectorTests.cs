using SubtitleCompare.Core.Analysis;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Tests;

public class SdhDetectorTests
{
    [Fact]
    public void Hearing_impaired_flag_only()
    {
        var track = new SubtitleTrackInfo { IsHearingImpaired = true };
        var a = SdhDetector.EvaluateSdh(track, Cues("Just dialogue."));
        Assert.True(a.IsMatch);
        Assert.Equal(KindSource.Flag, a.Source);
        Assert.Equal("SDH subtitle track (from track flag)", a.Label);
    }

    [Fact]
    public void Title_SDH_only()
    {
        var track = new SubtitleTrackInfo { Title = "English [SDH]" };
        var a = SdhDetector.EvaluateSdh(track, Cues("Hello."));
        Assert.True(a.IsMatch);
        Assert.Equal(KindSource.Title, a.Source);
        Assert.Equal("SDH subtitle track (from track title)", a.Label);
    }

    [Fact]
    public void Title_and_hearing_impaired_flag()
    {
        var track = new SubtitleTrackInfo { Title = "English [SDH]", IsHearingImpaired = true };
        var a = SdhDetector.EvaluateSdh(track, Cues("Hello."));
        Assert.True(a.IsMatch);
        Assert.Equal(KindSource.TitleAndFlag, a.Source);
        Assert.Equal("SDH subtitle track (from track title & flag)", a.Label);
    }

    [Fact]
    public void Generic_title_with_hearing_impaired_flag_is_flag_only()
    {
        var track = new SubtitleTrackInfo { Title = "English", IsHearingImpaired = true };
        var a = SdhDetector.EvaluateSdh(track, Cues("Hello."));
        Assert.Equal("SDH subtitle track (from track flag)", a.Label);
    }

    [Fact]
    public void Plain_dialogue_is_not_SDH()
    {
        var cues = Cues(
            "What's going on?",
            "I don't know.",
            "Keep moving.",
            "Stay down.",
            "Over there.",
            "We should wait.",
            "No.",
            "Yes.",
            "Alright.",
            "Come on.");
        var a = SdhDetector.EvaluateSdh(new SubtitleTrackInfo(), cues);
        Assert.False(a.IsMatch);
    }

    [Fact]
    public void Many_bracket_cues_are_SDH()
    {
        var cues = new List<SubtitleCue>();
        for (var i = 0; i < 20; i++)
        {
            cues.Add(Cue(i % 2 == 0
                ? $"[gunfire] Get down {i}"
                : $"Just talking {i}"));
        }
        var a = SdhDetector.EvaluateSdh(new SubtitleTrackInfo(), cues);
        Assert.True(a.IsMatch);
        Assert.Equal(KindSource.Heuristic, a.Source);
        Assert.Equal("Potential SDH subtitle track detected", a.Label);
    }

    [Fact]
    public void Occasional_paren_is_not_enough()
    {
        var cues = Cues(
            "Hello (quietly).",
            "How are you?",
            "Fine.",
            "Okay.",
            "See you.",
            "Later.",
            "Bye.",
            "Wait.",
            "Now.",
            "Go.");
        var a = SdhDetector.EvaluateSdh(new SubtitleTrackInfo(), cues);
        Assert.False(a.IsMatch);
    }

    [Fact]
    public void Forced_flag_only()
    {
        var track = new SubtitleTrackInfo { IsForced = true };
        var a = SdhDetector.EvaluateForced(track);
        Assert.True(a.IsMatch);
        Assert.Equal(KindSource.Flag, a.Source);
        Assert.Equal("Forced subtitle track (from track flag)", a.Label);
    }

    [Fact]
    public void Forced_title_only()
    {
        var track = new SubtitleTrackInfo { Title = "English Forced" };
        var a = SdhDetector.EvaluateForced(track);
        Assert.Equal("Forced subtitle track (from track title)", a.Label);
    }

    [Fact]
    public void Forced_title_and_flag()
    {
        var track = new SubtitleTrackInfo { Title = "Forced", IsForced = true };
        var a = SdhDetector.EvaluateForced(track);
        Assert.Equal("Forced subtitle track (from track title & flag)", a.Label);
    }

    [Fact]
    public void Describe_includes_both_SDH_and_forced()
    {
        var track = new SubtitleTrackInfo
        {
            Title = "English [SDH]",
            IsHearingImpaired = true,
            IsForced = true,
        };
        var lines = SdhDetector.Describe(track, Cues("Hello."));
        Assert.Equal(
            new[]
            {
                "SDH subtitle track (from track title & flag)",
                "Forced subtitle track (from track flag)",
            },
            lines);
    }

    private static List<SubtitleCue> Cues(params string[] texts) =>
        texts.Select((t, i) => Cue(t, i)).ToList();

    private static SubtitleCue Cue(string text, int i = 0) =>
        new()
        {
            Index = i,
            Start = TimeSpan.FromSeconds(i),
            End = TimeSpan.FromSeconds(i + 1),
            Text = text,
            RawText = text,
        };
}
