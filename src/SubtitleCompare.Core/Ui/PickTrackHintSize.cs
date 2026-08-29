namespace SubtitleCompare.Core.Ui;

/// <summary>
/// Column-relative size for the pick-track arrow. Kept small so the hint
/// is a quiet nudge rather than a wayfinding sign.
/// </summary>
public static class PickTrackHintSize
{
    public const double Fraction = 0.12;
    public const double MinWidth = 28;
    public const double MaxWidth = 52;

    public static double ArrowWidth(double columnWidth)
    {
        if (double.IsNaN(columnWidth) || double.IsInfinity(columnWidth) || columnWidth <= 0)
            return MinWidth;
        return Math.Clamp(columnWidth * Fraction, MinWidth, MaxWidth);
    }
}
