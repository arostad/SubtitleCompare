using SubtitleCompare.Core.Parsing;

namespace SubtitleCompare.Tests;

public class AssParserTests
{
    private const string Snippet = """
        [Script Info]
        Title: Test
        ScriptType: v4.00+

        [V4+ Styles]
        Format: Name, Fontname, Fontsize, PrimaryColour, Bold, Italic
        Style: Default,Arial,20,&H00FFFFFF,0,0

        [Events]
        Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
        Dialogue: 0,0:00:01.00,0:00:03.50,Default,,0,0,0,,Hello {\i1}there{\i0}\NHow are you?
        Dialogue: 0,0:00:04.00,0:00:06.00,Default,,0,0,0,,Second line
        """;

    [Fact]
    public void Parses_dialogue_strips_override_tags_and_converts_N()
    {
        var parsed = new AssParser().Parse(Snippet);

        Assert.Equal("ass", parsed.Format);
        Assert.Equal(2, parsed.Cues.Count);

        var first = parsed.Cues[0];
        Assert.Equal(TimeSpan.FromSeconds(1), first.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(3500), first.End);
        Assert.Equal("Hello there\nHow are you?", first.Text);
        Assert.Contains(@"{\i1}", first.RawText);
        Assert.DoesNotContain(@"{\i1}", first.Text);

        Assert.Equal("Second line", parsed.Cues[1].Text);
        Assert.Equal(TimeSpan.FromSeconds(4), parsed.Cues[1].Start);
    }

    [Fact]
    public void Auto_detect_routes_ass_content()
    {
        var parsed = SubtitleParser.Parse(Snippet);
        Assert.Equal("ass", parsed.Format);
        Assert.Equal(2, parsed.Cues.Count);
    }
}
