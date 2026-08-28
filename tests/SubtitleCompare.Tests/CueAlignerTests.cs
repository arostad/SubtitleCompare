using SubtitleCompare.Core.Alignment;
using SubtitleCompare.Core.Models;
using SubtitleCompare.Core.Parsing;

namespace SubtitleCompare.Tests;

public class CueAlignerTests
{
    private static ParsedSubtitles A => new SrtParser().ParseFile(SampleFiles.A);
    private static ParsedSubtitles B => new SrtParser().ParseFile(SampleFiles.B);
    private static ParsedSubtitles C => new SrtParser().ParseFile(SampleFiles.C);

    [Fact]
    public void Two_track_alignment_pairs_overlaps_and_keeps_unmatched()
    {
        var rows = CueAligner.Align(A.Cues, B.Cues);

        Assert.True(rows.Count >= 10);
        Assert.Equal(rows.OrderBy(r => r.Timestamp).Select(r => r.Timestamp), rows.Select(r => r.Timestamp));

        // Opening greetings overlap and pair.
        var hello = rows.First(r => r.CueA is not null && r.CueA.Text.Contains("Hello there"));
        Assert.NotNull(hello.CueB);
        Assert.Contains("doing?", hello.CueB!.Text);

        // A's walk line is missing from B.
        var walk = rows.Single(r => r.CueA is not null && r.CueA.Text.Contains("walk"));
        Assert.Null(walk.CueB);
        Assert.True(walk.IsUnmatched);

        // B's extra "around the corner" does not overlap any A cue.
        var extra = rows.Single(r => r.CueB is not null && r.CueB.Text.Contains("corner"));
        Assert.Null(extra.CueA);
        Assert.True(extra.IsUnmatched);

        // Shifted "After you" still overlaps.
        var after = rows.First(r => r.CueA is not null && r.CueA.Text.StartsWith("After you"));
        Assert.NotNull(after.CueB);
        Assert.Contains("please", after.CueB!.Text);

        // True unique at 00:00:30.
        var unique = rows.Single(r => r.CueB is not null && r.CueB.Text.Contains("Wait for me"));
        Assert.Null(unique.CueA);
        Assert.Equal(TimeSpan.FromSeconds(30), unique.Timestamp);
    }

    [Fact]
    public void Three_track_alignment_matches_shared_moments()
    {
        var rows = CueAligner.Align(A.Cues, B.Cues, C.Cues);

        var hello = rows.First(r => r.CueA is not null && r.CueA.Text.Contains("Hello there"));
        Assert.NotNull(hello.CueB);
        Assert.NotNull(hello.CueC);
        Assert.Equal(TimeSpan.FromSeconds(1), hello.Timestamp);

        // Walk exists in A and C, not B.
        var walk = rows.Single(r => r.CueA is not null && r.CueA.Text.Contains("walk"));
        Assert.Null(walk.CueB);
        Assert.NotNull(walk.CueC);
        Assert.Contains("take a walk", walk.CueC!.Text);

        // Unique B-only rows still exist.
        Assert.Contains(rows, r => r.CueB is not null && r.CueB.Text.Contains("Wait for me") && r.CueA is null && r.CueC is null);
        Assert.Contains(rows, r => r.CueB is not null && r.CueB.Text.Contains("corner") && r.CueA is null && r.CueC is null);

        // Identical timing "I would like that." triples up.
        var like = rows.Single(r => r.CueA is not null && r.CueA.Text == "I would like that.");
        Assert.Equal("I would like that.", like.CueB!.Text);
        Assert.Equal("I would like that.", like.CueC!.Text);
    }

    [Fact]
    public void Overlapping_times_pair_closest_start()
    {
        var a = new[]
        {
            Cue(1, 1.0, 3.0, "one"),
            Cue(2, 4.0, 6.0, "two"),
        };
        var b = new[]
        {
            Cue(1, 1.4, 2.8, "nearest"),
            Cue(2, 2.0, 3.5, "also-overlaps-first"),
            Cue(3, 10.0, 11.0, "far"),
        };

        var rows = CueAligner.Align(a, b);
        var first = rows.First(r => r.CueA?.Text == "one");
        Assert.Equal("nearest", first.CueB!.Text);

        Assert.Contains(rows, r => r.CueB?.Text == "also-overlaps-first" && r.CueA is null);
        Assert.Contains(rows, r => r.CueB?.Text == "far" && r.CueA is null);
        Assert.Contains(rows, r => r.CueA?.Text == "two" && r.CueB is null);
    }

    [Fact]
    public void Single_track_is_one_row_per_cue()
    {
        var rows = CueAligner.Align(A.Cues);
        Assert.Equal(8, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.NotNull(r.CueA);
            Assert.Null(r.CueB);
            Assert.Null(r.CueC);
        });
    }

    private static SubtitleCue Cue(int index, double startSec, double endSec, string text) =>
        new()
        {
            Index = index,
            Start = TimeSpan.FromSeconds(startSec),
            End = TimeSpan.FromSeconds(endSec),
            Text = text,
            RawText = text,
        };
}
