namespace SubtitleCompare.Core.Ui;

/// <summary>
/// What a compare pane is doing right now. Only the real load phases —
/// extract, parse, leftover setup, and OCR.
/// </summary>
public enum BusyKind
{
    Extracting,
    Parsing,
    Processing,
    Ocr,
}

/// <summary>
/// One busy column. <paramref name="Index"/> is 0/1/2 for panes A/B/C.
/// <paramref name="Total"/> is 0 while the work is still indeterminate.
/// </summary>
public readonly record struct BusyPane(int Index, BusyKind Kind, int Current = 0, int Total = 0)
{
    public char Letter => (char)('A' + Index);

    public bool IsDeterminate => Total > 0;
}

/// <summary>
/// Status-bar wording and combined OCR/process percent for busy panes.
/// </summary>
public static class BusyStatus
{
    private static readonly BusyKind[] KindOrder =
    [
        BusyKind.Extracting,
        BusyKind.Parsing,
        BusyKind.Processing,
        BusyKind.Ocr,
    ];

    public static BusyKind Classify(int current, int total, string? message)
    {
        if (total > 0)
            return BusyKind.Ocr;

        if (string.IsNullOrWhiteSpace(message))
            return BusyKind.Processing;

        if (message.StartsWith("Extract", StringComparison.OrdinalIgnoreCase))
            return BusyKind.Extracting;
        if (message.StartsWith("Pars", StringComparison.OrdinalIgnoreCase))
            return BusyKind.Parsing;
        if (message.Contains("OCR", StringComparison.OrdinalIgnoreCase))
            return BusyKind.Ocr;
        return BusyKind.Processing;
    }

    /// <summary>
    /// <c>sum(current) / sum(total)</c> over determinate panes only.
    /// Null when nobody has a real total yet — not a fake 0%.
    /// </summary>
    public static int? CombinedPercent(IReadOnlyList<BusyPane> panes)
    {
        var fraction = CombinedFraction(panes);
        if (fraction is null)
            return null;
        return (int)Math.Round(100.0 * fraction.Value);
    }

    public static double? CombinedFraction(IReadOnlyList<BusyPane> panes)
    {
        ArgumentNullException.ThrowIfNull(panes);
        long current = 0;
        long total = 0;
        foreach (var pane in panes)
        {
            if (pane.Total <= 0)
                continue;
            current += pane.Current;
            total += pane.Total;
        }

        if (total <= 0)
            return null;
        return (double)current / total;
    }

    /// <summary>
    /// Casual status slot: <c>Extracting A</c>, <c>OCR B+C · 40%</c>,
    /// <c>Extracting A · OCR C · 18%</c>. Percent only when a total exists.
    /// </summary>
    public static string Format(IReadOnlyList<BusyPane> panes)
    {
        ArgumentNullException.ThrowIfNull(panes);
        if (panes.Count == 0)
            return "";

        var parts = new List<string>();
        foreach (var kind in KindOrder)
        {
            char? a = null, b = null, c = null;
            foreach (var pane in panes)
            {
                if (pane.Kind != kind)
                    continue;
                switch (pane.Index)
                {
                    case 0: a = 'A'; break;
                    case 1: b = 'B'; break;
                    case 2: c = 'C'; break;
                }
            }

            if (a is null && b is null && c is null)
                continue;

            var letters = string.Concat(
                a is char aa ? aa.ToString() : "",
                b is char bb ? (a is null ? "B" : "+B") : "",
                c is char cc ? (a is null && b is null ? "C" : "+C") : "");
            parts.Add($"{Label(kind)} {letters}");
        }

        var label = string.Join(" · ", parts);
        return CombinedPercent(panes) is int pct ? $"{label} · {pct}%" : label;
    }

    private static string Label(BusyKind kind) => kind switch
    {
        BusyKind.Extracting => "Extracting",
        BusyKind.Parsing => "Parsing",
        BusyKind.Processing => "Processing",
        BusyKind.Ocr => "OCR",
        _ => "Processing",
    };
}
