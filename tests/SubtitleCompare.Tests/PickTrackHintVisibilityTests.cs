using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.Tests;

public class PickTrackHintVisibilityTests
{
    [Fact]
    public void Shows_when_file_is_loaded_and_dropdown_is_none()
    {
        Assert.True(PickTrackHintVisibility.ShouldShow(
            fileLoaded: true, trackSelected: false, overlayVisible: false));
    }

    [Fact]
    public void Hides_when_no_file_is_loaded()
    {
        Assert.False(PickTrackHintVisibility.ShouldShow(
            fileLoaded: false, trackSelected: false, overlayVisible: false));
    }

    [Fact]
    public void Hides_when_that_column_has_a_track()
    {
        Assert.False(PickTrackHintVisibility.ShouldShow(
            fileLoaded: true, trackSelected: true, overlayVisible: false));
    }

    [Fact]
    public void Hides_when_that_pane_has_an_overlay()
    {
        Assert.False(PickTrackHintVisibility.ShouldShow(
            fileLoaded: true, trackSelected: false, overlayVisible: true));
    }

    [Fact]
    public void Other_columns_stay_independent()
    {
        Assert.True(PickTrackHintVisibility.ShouldShow(
            fileLoaded: true, trackSelected: false, overlayVisible: false));
        Assert.False(PickTrackHintVisibility.ShouldShow(
            fileLoaded: true, trackSelected: true, overlayVisible: false));
    }
}
