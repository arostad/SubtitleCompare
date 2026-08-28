namespace SubtitleCompare.Core.Models;

/// <summary>
/// A single timed caption. <see cref="Text"/> is display-ready;
/// <see cref="RawText"/> is the original payload before tag stripping.
/// </summary>
public sealed class SubtitleCue
{
    public int Index { get; init; }
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
    public string Text { get; init; } = "";
    public string RawText { get; init; } = "";

    public TimeSpan Duration => End < Start ? TimeSpan.Zero : End - Start;

    public bool Overlaps(SubtitleCue other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && other.Start < End;
    }
}
