using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.Tests;

public class PaneRefreshTests
{
    [Fact]
    public void First_load_of_a_track_loads()
    {
        Assert.Equal(
            PaneRefreshAction.Load,
            PaneRefresh.Decide(PaneSelection.ForTrack(0), settled: null, busyWithSameSelection: false));
    }

    [Fact]
    public void First_load_of_none_stays_empty()
    {
        Assert.Equal(
            PaneRefreshAction.Keep,
            PaneRefresh.Decide(PaneSelection.None, settled: null, busyWithSameSelection: false));
    }

    [Fact]
    public void Settled_same_track_is_not_reloaded()
    {
        var track = PaneSelection.ForTrack(2);
        Assert.Equal(
            PaneRefreshAction.Keep,
            PaneRefresh.Decide(track, settled: track, busyWithSameSelection: false));
    }

    [Fact]
    public void Failed_or_empty_result_for_the_same_track_is_not_retried()
    {
        var track = PaneSelection.ForTrack(1);
        Assert.Equal(
            PaneRefreshAction.Keep,
            PaneRefresh.Decide(track, settled: track, busyWithSameSelection: false));
    }

    [Fact]
    public void Stable_none_is_not_reloaded()
    {
        Assert.Equal(
            PaneRefreshAction.Keep,
            PaneRefresh.Decide(PaneSelection.None, settled: PaneSelection.None, busyWithSameSelection: false));
    }

    [Fact]
    public void Switching_to_none_clears_a_loaded_track()
    {
        Assert.Equal(
            PaneRefreshAction.Clear,
            PaneRefresh.Decide(PaneSelection.None, settled: PaneSelection.ForTrack(0), busyWithSameSelection: false));
    }

    [Fact]
    public void Switching_tracks_reloads()
    {
        Assert.Equal(
            PaneRefreshAction.Load,
            PaneRefresh.Decide(
                PaneSelection.ForTrack(4),
                settled: PaneSelection.ForTrack(1),
                busyWithSameSelection: false));
    }

    [Fact]
    public void None_to_a_track_loads()
    {
        Assert.Equal(
            PaneRefreshAction.Load,
            PaneRefresh.Decide(
                PaneSelection.ForTrack(0),
                settled: PaneSelection.None,
                busyWithSameSelection: false));
    }

    [Fact]
    public void Busy_same_selection_is_left_alone()
    {
        Assert.Equal(
            PaneRefreshAction.Keep,
            PaneRefresh.Decide(
                PaneSelection.ForTrack(3),
                settled: null,
                busyWithSameSelection: true));
    }

    [Fact]
    public void Changing_a_busy_pane_to_another_track_reloads()
    {
        Assert.Equal(
            PaneRefreshAction.Load,
            PaneRefresh.Decide(
                PaneSelection.ForTrack(5),
                settled: PaneSelection.ForTrack(3),
                busyWithSameSelection: false));
    }
}
