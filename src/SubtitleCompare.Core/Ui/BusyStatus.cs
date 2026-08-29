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
/// <paramref name="Step"/> is the casual overlay line when we have one.
/// </summary>
public readonly record struct BusyPane(int Index, BusyKind Kind, int Current = 0, int Total = 0, string? Step = null)
{
    public char Letter => (char)('A' + Index);

    public bool IsDeterminate => Total > 0;

    public string StepLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Step))
                return Step.Trim();
            return Kind switch
            {
                BusyKind.Extracting => "extracting",
                BusyKind.Parsing => "parsing",
                BusyKind.Ocr => "OCR",
                _ => "processing",
            };
        }
    }
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

        if (message.Contains("pulling", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Extract", StringComparison.OrdinalIgnoreCase))
            return BusyKind.Extracting;

        if (message.Contains("reading", StringComparison.OrdinalIgnoreCase)
            || message.Contains("pars", StringComparison.OrdinalIgnoreCase))
            return BusyKind.Parsing;

        if (message.Contains("OCR", StringComparison.OrdinalIgnoreCase)
            || message.Contains("download", StringComparison.OrdinalIgnoreCase))
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
    /// Casual status slot: <c>A pulling the track out of the MKV</c>,
    /// <c>B+C OCR 40 / 100 · 40%</c>. Letters stay A/B/C. Percent only
    /// when a total exists.
    /// </summary>
    public static string Format(IReadOnlyList<BusyPane> panes)
    {
        ArgumentNullException.ThrowIfNull(panes);
        if (panes.Count == 0)
            return "";

        var parts = new List<string>();
        var countedOcr = new List<BusyPane>(3);
        var rest = new List<BusyPane>(3);
        foreach (var pane in panes)
        {
            if (pane.Kind == BusyKind.Ocr && pane.IsDeterminate)
                countedOcr.Add(pane);
            else
                rest.Add(pane);
        }

        foreach (var kind in KindOrder)
        {
            List<BusyPane>? ofKind = null;
            foreach (var pane in rest)
            {
                if (pane.Kind != kind)
                    continue;
                ofKind ??= new List<BusyPane>(3);
                ofKind.Add(pane);
            }

            if (ofKind is null)
                continue;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pane in ofKind.OrderBy(p => p.Index))
            {
                var key = pane.StepLabel;
                if (!seen.Add(key))
                    continue;

                var group = ofKind.Where(p => p.StepLabel == key);
                parts.Add($"{Letters(group)} {key}");
            }
        }

        if (countedOcr.Count > 0)
        {
            long current = 0;
            long total = 0;
            foreach (var pane in countedOcr)
            {
                current += pane.Current;
                total += pane.Total;
            }

            parts.Add($"{Letters(countedOcr)} OCR {current} / {total}");
        }

        var label = string.Join(" · ", parts);
        return CombinedPercent(panes) is int pct ? $"{label} · {pct}%" : label;
    }

    private static string Letters(IEnumerable<BusyPane> panes)
    {
        char? a = null, b = null, c = null;
        foreach (var pane in panes)
        {
            switch (pane.Index)
            {
                case 0: a = 'A'; break;
                case 1: b = 'B'; break;
                case 2: c = 'C'; break;
            }
        }

        return string.Concat(
            a is not null ? "A" : "",
            b is not null ? (a is null ? "B" : "+B") : "",
            c is not null ? (a is null && b is null ? "C" : "+C") : "");
    }
}
