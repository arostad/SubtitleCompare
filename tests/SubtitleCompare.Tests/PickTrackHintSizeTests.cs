using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.Tests;

public class PickTrackHintSizeTests
{
    [Fact]
    public void Uses_22_percent_of_the_column_when_in_range()
    {
        Assert.Equal(44, PickTrackHintSize.ArrowWidth(200));
    }

    [Fact]
    public void Floors_at_36_when_the_column_is_narrow()
    {
        Assert.Equal(36, PickTrackHintSize.ArrowWidth(100));
    }

    [Fact]
    public void Caps_at_96_when_the_column_is_wide()
    {
        Assert.Equal(96, PickTrackHintSize.ArrowWidth(500));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Falls_back_to_min_when_width_is_unusable(double columnWidth)
    {
        Assert.Equal(36, PickTrackHintSize.ArrowWidth(columnWidth));
    }
}
