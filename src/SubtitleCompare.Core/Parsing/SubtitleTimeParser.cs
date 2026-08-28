using System.Globalization;
using System.Text.RegularExpressions;

namespace SubtitleCompare.Core.Parsing;

internal static class SubtitleTimeParser
{
    // 00:00:01,000 or 00:00:01.000  (SRT / VTT with hours)
    private static readonly Regex Hours = new(
        @"^(?<h>\d{1,3}):(?<m>[0-5]?\d):(?<s>[0-5]?\d)[,\.](?<ms>\d{1,3})$",
        RegexOptions.Compiled);

    // 00:01.000  (VTT short)
    private static readonly Regex Minutes = new(
        @"^(?<m>\d{1,3}):(?<s>[0-5]?\d)[,\.](?<ms>\d{1,3})$",
        RegexOptions.Compiled);

    // ASS: H:MM:SS.cs  (centiseconds, typically 2 digits; also accept 1–3)
    private static readonly Regex Ass = new(
        @"^(?<h>\d+):(?<m>[0-5]\d):(?<s>[0-5]\d)\.(?<cs>\d{1,3})$",
        RegexOptions.Compiled);

    public static bool TryParseSrtOrVtt(string value, out TimeSpan time)
    {
        time = default;
        var s = value.Trim();
        var m = Hours.Match(s);
        if (m.Success)
        {
            time = Build(m.Groups["h"].Value, m.Groups["m"].Value, m.Groups["s"].Value, m.Groups["ms"].Value, msIsCentiseconds: false);
            return true;
        }

        m = Minutes.Match(s);
        if (m.Success)
        {
            time = Build("0", m.Groups["m"].Value, m.Groups["s"].Value, m.Groups["ms"].Value, msIsCentiseconds: false);
            return true;
        }

        return false;
    }

    public static bool TryParseAss(string value, out TimeSpan time)
    {
        time = default;
        var s = value.Trim();
        var m = Ass.Match(s);
        if (!m.Success)
        {
            // Fall back to SRT-style if someone used commas.
            return TryParseSrtOrVtt(s, out time);
        }

        time = Build(m.Groups["h"].Value, m.Groups["m"].Value, m.Groups["s"].Value, m.Groups["cs"].Value, msIsCentiseconds: true);
        return true;
    }

    private static TimeSpan Build(string h, string m, string s, string frac, bool msIsCentiseconds)
    {
        var hours = int.Parse(h, CultureInfo.InvariantCulture);
        var minutes = int.Parse(m, CultureInfo.InvariantCulture);
        var seconds = int.Parse(s, CultureInfo.InvariantCulture);
        int milliseconds;
        if (msIsCentiseconds)
        {
            // 2-digit centiseconds: "50" → 500ms. 1-digit: "5" → 500ms. 3-digit: treat as ms.
            milliseconds = frac.Length switch
            {
                1 => int.Parse(frac, CultureInfo.InvariantCulture) * 100,
                2 => int.Parse(frac, CultureInfo.InvariantCulture) * 10,
                _ => int.Parse(frac.PadRight(3, '0')[..3], CultureInfo.InvariantCulture),
            };
        }
        else
        {
            milliseconds = int.Parse(frac.PadRight(3, '0')[..3], CultureInfo.InvariantCulture);
        }

        return new TimeSpan(0, hours, minutes, seconds, milliseconds);
    }
}
