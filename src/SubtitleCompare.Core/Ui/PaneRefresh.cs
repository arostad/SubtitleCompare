namespace SubtitleCompare.Core.Ui;

/// <summary>
/// Which subtitle stream a compare pane is pointed at.
/// <see cref="None"/> is the empty <c>(none)</c> slot.
/// </summary>
public readonly record struct PaneSelection(int? TrackIndex)
{
    public bool IsNone => TrackIndex is null;

    public static PaneSelection None { get; } = new(null);

    public static PaneSelection ForTrack(int trackIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(trackIndex);
        return new PaneSelection(trackIndex);
    }
}

public enum PaneRefreshAction
{
    /// <summary>Leave this pane alone — same track, already settled or still loading it.</summary>
    Keep,
    /// <summary>Selection became <c>(none)</c>; drop the old result.</summary>
    Clear,
    /// <summary>Selection changed (or this is the first load for that track).</summary>
    Load,
}

/// <summary>
/// Decides whether a dropdown change should touch a pane.
/// Unchanged settled panes (including a stable none) stay put.
/// </summary>
public static class PaneRefresh
{
    public static PaneRefreshAction Decide(
        PaneSelection selected,
        PaneSelection? settled,
        bool busyWithSameSelection)
    {
        if (busyWithSameSelection)
            return PaneRefreshAction.Keep;

        if (settled is { } current && current.Equals(selected))
            return PaneRefreshAction.Keep;

        if (selected.IsNone)
            return settled is { IsNone: false } ? PaneRefreshAction.Clear : PaneRefreshAction.Keep;

        return PaneRefreshAction.Load;
    }
}
