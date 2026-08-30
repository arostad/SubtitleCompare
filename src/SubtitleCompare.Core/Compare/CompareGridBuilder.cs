using SubtitleCompare.Core.Alignment;
using SubtitleCompare.Core.Diff;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Compare;

/// <summary>
/// One compare row after alignment and word-level diffs.
/// Visual construction stays in the app; this is the CPU work.
/// </summary>
public sealed class CompareRowModel
{
    public required AlignedRow Row { get; init; }
    public required bool IsDiff { get; init; }
    public required bool[] Present { get; init; }
    public required bool[] DiffFrameByPane { get; init; }
    public required IReadOnlyList<DiffSegment>?[] DiffByPane { get; init; }
}

/// <summary>
/// Aligns cues and diffs wording off the UI thread.
/// </summary>
public sealed class CompareGridModel
{
    public required bool[] Active { get; init; }
    public required IReadOnlyList<CompareRowModel> Rows { get; init; }

    public int DiffCount { get; init; }
}

public static class CompareGridBuilder
{
    public static CompareGridModel Build(ParsedSubtitles?[] parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        if (parsed.Length != 3)
            throw new ArgumentException("Compare expects three pane slots.", nameof(parsed));

        var active = new[]
        {
            parsed[0] is not null,
            parsed[1] is not null,
            parsed[2] is not null,
        };

        if (!active[0] && !active[1] && !active[2])
        {
            return new CompareGridModel
            {
                Active = active,
                Rows = Array.Empty<CompareRowModel>(),
                DiffCount = 0,
            };
        }

        var rows = CueAligner.Align(parsed[0]?.Cues, parsed[1]?.Cues, parsed[2]?.Cues);
        var models = new CompareRowModel[rows.Count];
        var activeCount = (active[0] ? 1 : 0) + (active[1] ? 1 : 0) + (active[2] ? 1 : 0);
        var diffsCount = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var present = new bool[3];
            var selected = new string[3];
            var selectedCount = 0;
            var presentCount = 0;

            for (var p = 0; p < 3; p++)
            {
                if (!active[p])
                    continue;
                var cue = row[p];
                present[p] = cue is not null;
                if (cue is null)
                    continue;
                presentCount++;
                selected[selectedCount++] = cue.Text ?? "";
            }

            IReadOnlyList<IReadOnlyList<DiffSegment>>? diffs = null;
            if (selectedCount >= 2)
            {
                diffs = selectedCount == 2
                    ? TextDiffer.Compare(selected[0], selected[1])
                    : TextDiffer.Compare(selected[0], selected[1], selected[2]);
            }

            var diffByPane = new IReadOnlyList<DiffSegment>?[3];
            if (diffs is not null)
            {
                var di = 0;
                for (var p = 0; p < 3; p++)
                {
                    if (active[p] && present[p])
                        diffByPane[p] = diffs[di++];
                }
            }

            var anyMissing = false;
            if (activeCount > 1)
            {
                for (var p = 0; p < 3; p++)
                {
                    if (active[p] && !present[p])
                    {
                        anyMissing = true;
                        break;
                    }
                }
            }

            var textDiffers = diffs is not null && TextDiffer.RowHasDifference(diffs);
            var isDiff = textDiffers || anyMissing || (activeCount > 1 && presentCount == 1);
            if (isDiff)
                diffsCount++;

            var diffFrameByPane = new bool[3];
            for (var p = 0; p < 3; p++)
                diffFrameByPane[p] = active[p] && isDiff;

            models[i] = new CompareRowModel
            {
                Row = row,
                IsDiff = isDiff,
                Present = present,
                DiffFrameByPane = diffFrameByPane,
                DiffByPane = diffByPane,
            };
        }

        return new CompareGridModel
        {
            Active = active,
            Rows = models,
            DiffCount = diffsCount,
        };
    }
}
