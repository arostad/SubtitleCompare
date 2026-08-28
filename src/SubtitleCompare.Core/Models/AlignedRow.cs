namespace SubtitleCompare.Core.Models;

/// <summary>
/// One timestamp-aligned compare row. Any pane may be null (no cue at this moment).
/// <see cref="Timestamp"/> is the earliest start among the member cues.
/// </summary>
public sealed class AlignedRow
{
    public AlignedRow(TimeSpan timestamp, SubtitleCue? cueA, SubtitleCue? cueB, SubtitleCue? cueC)
    {
        Timestamp = timestamp;
        CueA = cueA;
        CueB = cueB;
        CueC = cueC;
    }

    public TimeSpan Timestamp { get; }
    public SubtitleCue? CueA { get; }
    public SubtitleCue? CueB { get; }
    public SubtitleCue? CueC { get; }

    public SubtitleCue? this[int pane] => pane switch
    {
        0 => CueA,
        1 => CueB,
        2 => CueC,
        _ => throw new ArgumentOutOfRangeException(nameof(pane)),
    };

    public int AssignedCount
    {
        get
        {
            var n = 0;
            if (CueA is not null) n++;
            if (CueB is not null) n++;
            if (CueC is not null) n++;
            return n;
        }
    }

    public bool IsUnmatched => AssignedCount == 1;

    public TimeSpan? End
    {
        get
        {
            TimeSpan? end = null;
            if (CueA is not null) end = CueA.End;
            if (CueB is not null && (end is null || CueB.End > end)) end = CueB.End;
            if (CueC is not null && (end is null || CueC.End > end)) end = CueC.End;
            return end;
        }
    }
}
