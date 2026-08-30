using SubtitleCompare.Core.Compare;
using SubtitleCompare.Core.Models;
using SubtitleCompare.Core.Parsing;

namespace SubtitleCompare.Tests;

public class CompareGridBuilderTests
{
    [Fact]
    public void Empty_slots_make_an_empty_grid()
    {
        var model = CompareGridBuilder.Build([null, null, null]);
        Assert.Empty(model.Rows);
        Assert.Equal(0, model.DiffCount);
        Assert.All(model.Active, on => Assert.False(on));
    }

    [Fact]
    public void Two_sample_tracks_align_and_mark_diffs()
    {
        var a = new SrtParser().ParseFile(SampleFiles.A);
        var b = new SrtParser().ParseFile(SampleFiles.B);
        var model = CompareGridBuilder.Build([a, b, null]);

        Assert.True(model.Active[0]);
        Assert.True(model.Active[1]);
        Assert.False(model.Active[2]);
        Assert.True(model.Rows.Count >= 10);
        Assert.True(model.DiffCount > 0);
        Assert.Equal(model.DiffCount, model.Rows.Count(r => r.IsDiff));

        var hello = model.Rows.First(r => r.Row.CueA?.Text.Contains("Hello there") == true);
        Assert.NotNull(hello.Row.CueB);
        Assert.True(hello.Present[0]);
        Assert.True(hello.Present[1]);
        Assert.False(hello.Present[2]);
    }

    [Fact]
    public void Inactive_third_pane_never_gets_a_diff_frame()
    {
        var a = Track("Original line");
        var b = Track("Changed line");

        var model = CompareGridBuilder.Build([a, b, null]);

        var row = Assert.Single(model.Rows);
        Assert.True(row.IsDiff);
        Assert.True(row.DiffFrameByPane[0]);
        Assert.True(row.DiffFrameByPane[1]);
        Assert.False(row.DiffFrameByPane[2]);
        Assert.Null(row.DiffByPane[2]);
    }

    [Fact]
    public void One_active_pane_has_no_diffs()
    {
        var a = new ParsedSubtitles
        {
            Format = "srt",
            Cues =
            [
                new SubtitleCue
                {
                    Index = 1,
                    Start = TimeSpan.FromSeconds(1),
                    End = TimeSpan.FromSeconds(2),
                    Text = "Hello",
                    RawText = "Hello",
                },
            ],
        };

        var model = CompareGridBuilder.Build([a, null, null]);
        Assert.Single(model.Rows);
        Assert.Equal(0, model.DiffCount);
        Assert.False(model.Rows[0].IsDiff);
    }

    [Fact]
    public void Missing_counterpart_is_a_diff()
    {
        var a = new ParsedSubtitles
        {
            Format = "srt",
            Cues =
            [
                new SubtitleCue
                {
                    Index = 1,
                    Start = TimeSpan.FromSeconds(1),
                    End = TimeSpan.FromSeconds(2),
                    Text = "Only A",
                    RawText = "Only A",
                },
            ],
        };
        var b = new ParsedSubtitles { Format = "srt", Cues = Array.Empty<SubtitleCue>() };

        var model = CompareGridBuilder.Build([a, b, null]);
        Assert.Single(model.Rows);
        Assert.True(model.Rows[0].IsDiff);
        Assert.Equal(1, model.DiffCount);
    }

    private static ParsedSubtitles Track(string text) => new()
    {
        Format = "srt",
        Cues =
        [
            new SubtitleCue
            {
                Index = 1,
                Start = TimeSpan.FromSeconds(1),
                End = TimeSpan.FromSeconds(2),
                Text = text,
                RawText = text,
            },
        ],
    };
}
