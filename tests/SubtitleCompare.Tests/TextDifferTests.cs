using SubtitleCompare.Core.Diff;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Tests;

public class TextDifferTests
{
    [Fact]
    public void Identical_strings_are_all_Equal()
    {
        var panes = TextDiffer.Compare("Hello there.", "Hello there.");

        Assert.Equal(2, panes.Count);
        Assert.All(panes[0], s => Assert.Equal(DiffKind.Equal, s.Kind));
        Assert.All(panes[1], s => Assert.Equal(DiffKind.Equal, s.Kind));
        Assert.Equal("Hello there.", string.Concat(panes[0].Select(s => s.Text)));
        Assert.False(TextDiffer.RowHasDifference(panes));
    }

    [Fact]
    public void One_word_changed_is_Changed()
    {
        var panes = TextDiffer.Compare("I am doing well.", "I am doing fine.");

        Assert.Contains(panes[0], s => s.Text.Contains("well") && s.Kind == DiffKind.Changed);
        Assert.Contains(panes[1], s => s.Text.Contains("fine") && s.Kind == DiffKind.Changed);
        Assert.Contains(panes[0], s => s.Text.Contains("doing") && s.Kind == DiffKind.Equal);
        Assert.True(TextDiffer.RowHasDifference(panes));
    }

    [Fact]
    public void Extra_word_is_Unique()
    {
        var panes = TextDiffer.Compare("How are you?", "How are you doing?");

        Assert.DoesNotContain(panes[0], s => s.Kind == DiffKind.Unique && s.Text.Contains("doing"));
        Assert.Contains(panes[1], s => s.Kind == DiffKind.Unique && s.Text.Contains("doing"));
        Assert.Contains(panes[0], s => s.Text.Contains("you") && s.Kind == DiffKind.Equal);
    }

    [Fact]
    public void Compare_is_case_insensitive_but_preserves_display()
    {
        var panes = TextDiffer.Compare("Hello World", "hello world");
        Assert.All(panes[0], s => Assert.Equal(DiffKind.Equal, s.Kind));
        Assert.All(panes[1], s => Assert.Equal(DiffKind.Equal, s.Kind));
        Assert.Equal("Hello World", string.Concat(panes[0].Select(s => s.Text)));
        Assert.Equal("hello world", string.Concat(panes[1].Select(s => s.Text)));
    }

    [Fact]
    public void Three_way_marks_changed_and_shared()
    {
        var panes = TextDiffer.Compare(
            "What a beautiful day.",
            "What a lovely day.",
            "Such a beautiful day.");

        Assert.Equal(3, panes.Count);
        Assert.Contains(panes[0], s => s.Text.Contains("beautiful") && s.Kind == DiffKind.Changed);
        Assert.Contains(panes[1], s => s.Text.Contains("lovely") && s.Kind == DiffKind.Changed);
        Assert.Contains(panes[2], s => s.Text.Contains("beautiful") && s.Kind == DiffKind.Changed);
        Assert.Contains(panes[0], s => s.Text.Contains("day") && s.Kind == DiffKind.Equal);
    }

    [Fact]
    public void Strips_ass_tags_before_compare()
    {
        var panes = TextDiffer.Compare(@"Hello {\i1}there{\i0}", "Hello there");
        Assert.All(panes[0], s => Assert.Equal(DiffKind.Equal, s.Kind));
        Assert.DoesNotContain(panes[0], s => s.Text.Contains(@"\i1"));
    }
}
