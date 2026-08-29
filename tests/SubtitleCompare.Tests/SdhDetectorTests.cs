using SubtitleCompare.Core.Analysis;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Tests;

public class SdhDetectorTests
{
    [Fact]
    public void Metadata_flag_wins()
    {
        var track = new SubtitleTrackInfo { IsHearingImpaired = true };
        var a = SdhDetector.Evaluate(track, Cues("Just dialogue."));
        Assert.True(a.IsLikelySdh);
        Assert.Equal(SdhEvidence.Metadata, a.Evidence);
    }

    [Fact]
    public void Title_SDH_is_detected()
    {
        var track = new SubtitleTrackInfo { Title = "English [SDH]" };
        var a = SdhDetector.Evaluate(track, Cues("Hello."));
        Assert.True(a.IsLikelySdh);
        Assert.Equal(SdhEvidence.Title, a.Evidence);
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
        var a = SdhDetector.Evaluate(new SubtitleTrackInfo(), cues);
        Assert.False(a.IsLikelySdh);
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
        var a = SdhDetector.Evaluate(new SubtitleTrackInfo(), cues);
        Assert.True(a.IsLikelySdh);
        Assert.Equal(SdhEvidence.Heuristic, a.Evidence);
        Assert.Equal("Potential SDH subtitle detected", a.Label);
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
        var a = SdhDetector.Evaluate(new SubtitleTrackInfo(), cues);
        Assert.False(a.IsLikelySdh);
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
