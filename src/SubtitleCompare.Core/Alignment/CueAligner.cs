using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Alignment;

/// <summary>
/// Aligns 1–3 cue lists into timestamp-keyed rows. Cues whose time ranges
/// overlap are paired; when several candidates overlap, the partner whose
/// start is closest to the seed cue wins. Unmatched cues keep their own row.
/// </summary>
public static class CueAligner
{
    public static IReadOnlyList<AlignedRow> Align(
        IReadOnlyList<SubtitleCue>? trackA,
        IReadOnlyList<SubtitleCue>? trackB = null,
        IReadOnlyList<SubtitleCue>? trackC = null)
    {
        var tracks = new[]
        {
            trackA ?? Array.Empty<SubtitleCue>(),
            trackB ?? Array.Empty<SubtitleCue>(),
            trackC ?? Array.Empty<SubtitleCue>(),
        };

        var used = new bool[3][];
        var order = new int[3][];
        var cursor = new int[3];
        var seeds = new List<(int Pane, int CueIndex, SubtitleCue Cue)>();
        for (var p = 0; p < 3; p++)
        {
            used[p] = new bool[tracks[p].Count];
            order[p] = SortedIndexes(tracks[p]);
            for (var i = 0; i < tracks[p].Count; i++)
                seeds.Add((p, i, tracks[p][i]));
        }

        seeds.Sort((x, y) =>
        {
            var c = x.Cue.Start.CompareTo(y.Cue.Start);
            if (c != 0) return c;
            c = x.Cue.End.CompareTo(y.Cue.End);
            if (c != 0) return c;
            return x.Pane.CompareTo(y.Pane);
        });

        var rows = new List<AlignedRow>(seeds.Count);
        foreach (var seed in seeds)
        {
            if (used[seed.Pane][seed.CueIndex])
                continue;

            var assigned = new SubtitleCue?[3];
            assigned[seed.Pane] = seed.Cue;
            used[seed.Pane][seed.CueIndex] = true;

            for (var other = 0; other < 3; other++)
            {
                if (other == seed.Pane || tracks[other].Count == 0)
                    continue;

                var bestIdx = FindBestOverlap(
                    seed.Cue,
                    tracks[other],
                    used[other],
                    order[other],
                    ref cursor[other]);
                if (bestIdx >= 0)
                {
                    assigned[other] = tracks[other][bestIdx];
                    used[other][bestIdx] = true;
                }
            }

            var timestamp = EarliestStart(assigned);
            rows.Add(new AlignedRow(timestamp, assigned[0], assigned[1], assigned[2]));
        }

        rows.Sort((x, y) =>
        {
            var c = x.Timestamp.CompareTo(y.Timestamp);
            return c != 0 ? c : x.AssignedCount.CompareTo(y.AssignedCount);
        });
        return rows;
    }

    /// <summary>
    /// Seeds are visited in start order, so a per-track cursor can skip
    /// cues that already ended. The remaining scan stops at the first
    /// cue that starts after the seed.
    /// </summary>
    private static int FindBestOverlap(
        SubtitleCue seed,
        IReadOnlyList<SubtitleCue> track,
        bool[] used,
        int[] order,
        ref int cursor)
    {
        while (cursor < order.Length)
        {
            var i = order[cursor];
            if (used[i] || track[i].End <= seed.Start)
                cursor++;
            else
                break;
        }

        var bestIdx = -1;
        var bestDist = TimeSpan.MaxValue;
        for (var k = cursor; k < order.Length; k++)
        {
            var i = order[k];
            var cand = track[i];
            if (cand.Start >= seed.End)
                break;
            if (used[i] || cand.End <= seed.Start)
                continue;

            var dist = (cand.Start - seed.Start).Duration();
            if (bestIdx < 0
                || dist < bestDist
                || (dist == bestDist && cand.Start < track[bestIdx].Start)
                || (dist == bestDist && cand.Start == track[bestIdx].Start && i < bestIdx))
            {
                bestIdx = i;
                bestDist = dist;
            }
        }

        return bestIdx;
    }

    private static int[] SortedIndexes(IReadOnlyList<SubtitleCue> track)
    {
        var idx = new int[track.Count];
        for (var i = 0; i < idx.Length; i++)
            idx[i] = i;
        Array.Sort(idx, (a, b) =>
        {
            var c = track[a].Start.CompareTo(track[b].Start);
            if (c != 0) return c;
            c = track[a].End.CompareTo(track[b].End);
            return c != 0 ? c : a.CompareTo(b);
        });
        return idx;
    }

    private static TimeSpan EarliestStart(SubtitleCue?[] assigned)
    {
        var ts = TimeSpan.MaxValue;
        foreach (var cue in assigned)
        {
            if (cue is not null && cue.Start < ts)
                ts = cue.Start;
        }
        return ts == TimeSpan.MaxValue ? TimeSpan.Zero : ts;
    }
}
