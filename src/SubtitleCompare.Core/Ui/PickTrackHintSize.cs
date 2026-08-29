namespace SubtitleCompare.Core.Ui;

/// <summary>
/// Column-relative size for the pick-track arrow so it scales with the pane
/// instead of staying locked at a fixed pixel width.
/// </summary>
public static class PickTrackHintSize
{
    public const double Fraction = 0.22;
    public const double MinWidth = 36;
    public const double MaxWidth = 96;

    public static double ArrowWidth(double columnWidth)
    {
        if (double.IsNaN(columnWidth) || double.IsInfinity(columnWidth) || columnWidth <= 0)
            return MinWidth;
        return Math.Clamp(columnWidth * Fraction, MinWidth, MaxWidth);
    }
}
