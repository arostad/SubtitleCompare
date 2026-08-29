namespace SubtitleCompare.Core.Ocr;

/// <summary>
/// Maps Matroska / ffprobe language tags onto Tesseract <c>tessdata</c> names.
/// Unknown tags fall back to English.
/// </summary>
public static class TessLanguage
{
    public const string Default = "eng";

    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "eng", ["en"] = "eng",
        ["spa"] = "spa", ["es"] = "spa", ["esp"] = "spa",
        ["fre"] = "fra", ["fra"] = "fra", ["fr"] = "fra",
        ["ger"] = "deu", ["deu"] = "deu", ["de"] = "deu",
        ["ita"] = "ita", ["it"] = "ita",
        ["por"] = "por", ["pt"] = "por", ["pob"] = "por",
        ["jpn"] = "jpn", ["ja"] = "jpn",
        ["chi"] = "chi_sim", ["zho"] = "chi_sim", ["zh"] = "chi_sim",
        ["cmn"] = "chi_sim", ["cht"] = "chi_tra", ["yue"] = "chi_tra",
        ["kor"] = "kor", ["ko"] = "kor",
        ["rus"] = "rus", ["ru"] = "rus",
        ["ara"] = "ara", ["ar"] = "ara",
        ["hin"] = "hin", ["hi"] = "hin",
        ["tha"] = "tha", ["th"] = "tha",
        ["vie"] = "vie", ["vi"] = "vie",
        ["pol"] = "pol", ["pl"] = "pol",
        ["nld"] = "nld", ["dut"] = "nld", ["nl"] = "nld",
        ["swe"] = "swe", ["sv"] = "swe",
        ["nor"] = "nor", ["no"] = "nor", ["nob"] = "nor",
        ["dan"] = "dan", ["da"] = "dan",
        ["fin"] = "fin", ["fi"] = "fin",
        ["hun"] = "hun", ["hu"] = "hun",
        ["ces"] = "ces", ["cze"] = "ces", ["cs"] = "ces",
        ["slk"] = "slk", ["sk"] = "slk",
        ["ron"] = "ron", ["rum"] = "ron", ["ro"] = "ron",
        ["tur"] = "tur", ["tr"] = "tur",
        ["heb"] = "heb", ["he"] = "heb",
        ["ukr"] = "ukr", ["uk"] = "ukr",
        ["ell"] = "ell", ["gre"] = "ell", ["el"] = "ell",
        ["ind"] = "ind", ["id"] = "ind",
        ["may"] = "msa", ["msa"] = "msa", ["ms"] = "msa",
        ["tam"] = "tam", ["ta"] = "tam",
        ["tel"] = "tel", ["te"] = "tel",
        ["ben"] = "ben", ["bn"] = "ben",
        ["urd"] = "urd", ["ur"] = "urd",
        ["fas"] = "fas", ["per"] = "fas", ["fa"] = "fas",
        ["cat"] = "cat", ["ca"] = "cat",
        ["hrv"] = "hrv", ["hr"] = "hrv",
        ["srp"] = "srp", ["sr"] = "srp",
        ["bos"] = "bos", ["bs"] = "bos",
        ["slv"] = "slv", ["sl"] = "slv",
        ["bul"] = "bul", ["bg"] = "bul",
        ["lit"] = "lit", ["lt"] = "lit",
        ["lav"] = "lav", ["lv"] = "lav",
        ["est"] = "est", ["et"] = "est",
        ["isl"] = "isl", ["is"] = "isl",
        ["gle"] = "gle", ["ga"] = "gle",
        ["wel"] = "cym", ["cym"] = "cym", ["cy"] = "cym",
        ["fil"] = "tgl", ["tgl"] = "tgl", ["tl"] = "tgl",
    };

    private static readonly Dictionary<string, string> ScriptHint = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hant"] = "chi_tra",
        ["trad"] = "chi_tra",
        ["tw"] = "chi_tra",
        ["hk"] = "chi_tra",
        ["hans"] = "chi_sim",
        ["cn"] = "chi_sim",
        ["sg"] = "chi_sim",
    };

    public static string FromTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return Default;

        var trimmed = tag.Trim();
        var parts = trimmed.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return Default;

        if (parts.Length > 1)
        {
            foreach (var part in parts.Skip(1))
            {
                if (ScriptHint.TryGetValue(part, out var hinted))
                    return hinted;
            }
        }

        if (Map.TryGetValue(parts[0], out var mapped))
            return mapped;

        var lower = parts[0].ToLowerInvariant();
        if (lower is "und" or "zxx")
            return Default;

        // Already a tessdata name (chi_sim) or a plausible 3-letter code.
        if (lower is "chi_sim" or "chi_tra")
            return lower;
        if (lower.Length is >= 3 and <= 8 && lower.All(c => char.IsAsciiLetter(c) || c == '_'))
            return lower;

        return Default;
    }

    public static string DisplayName(string tessLang)
    {
        if (string.IsNullOrWhiteSpace(tessLang))
            return "English";
        return tessLang.Trim().ToLowerInvariant() switch
        {
            "eng" => "English",
            "spa" => "Spanish",
            "fra" => "French",
            "deu" => "German",
            "ita" => "Italian",
            "por" => "Portuguese",
            "jpn" => "Japanese",
            "chi_sim" => "Chinese (Simplified)",
            "chi_tra" => "Chinese (Traditional)",
            "kor" => "Korean",
            "rus" => "Russian",
            _ => tessLang.Trim(),
        };
    }
}
