using SubtitleCompare.Core.Parsing;

namespace SubtitleCompare.Tests;

public class SrtParserTests
{
    [Fact]
    public void Parses_track_a_cue_count_and_times()
    {
        var parsed = new SrtParser().ParseFile(SampleFiles.A);

        Assert.Equal("srt", parsed.Format);
        Assert.Equal(8, parsed.Cues.Count);

        var first = parsed.Cues[0];
        Assert.Equal(1, first.Index);
        Assert.Equal(TimeSpan.FromSeconds(1), first.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(3500), first.End);
        Assert.Contains("Hello there.", first.Text);
        Assert.Contains("How are you?", first.Text);
        Assert.Contains('\n', first.Text);

        var last = parsed.Cues[^1];
        Assert.Equal(8, last.Index);
        Assert.Equal(TimeSpan.FromSeconds(22), last.Start);
        Assert.Equal(TimeSpan.FromSeconds(24), last.End);
        Assert.Equal("After you.", last.Text);
    }

    [Fact]
    public void Parses_track_b_extra_and_shifted_cues()
    {
        var parsed = new SrtParser().ParseFile(SampleFiles.B);

        Assert.Equal(9, parsed.Cues.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(7200), parsed.Cues[2].Start);
        Assert.Equal("Just around the corner.", parsed.Cues[4].Text);
        Assert.Equal(TimeSpan.FromSeconds(30), parsed.Cues[^1].Start);
        Assert.Equal("Wait for me!", parsed.Cues[^1].Text);
    }

    [Fact]
    public void Parses_track_c()
    {
        var parsed = new SrtParser().ParseFile(SampleFiles.C);
        Assert.Equal(8, parsed.Cues.Count);
        Assert.Equal("I am well.", parsed.Cues[1].Text);
        Assert.Equal(TimeSpan.FromSeconds(22), parsed.Cues[^1].Start);
    }

    [Fact]
    public void Accepts_period_milliseconds()
    {
        const string srt = """
            1
            00:00:01.250 --> 00:00:02.500
            Period millis.
            """;

        var parsed = new SrtParser().Parse(srt);
        Assert.Single(parsed.Cues);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), parsed.Cues[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(2500), parsed.Cues[0].End);
    }

    [Fact]
    public void Parses_multiline_cue()
    {
        var parsed = new SrtParser().ParseFile(SampleFiles.A);
        var lines = parsed.Cues[0].Text.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Hello there.", lines[0]);
        Assert.Equal("How are you?", lines[1]);
    }
}
