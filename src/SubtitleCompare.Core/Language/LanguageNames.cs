namespace SubtitleCompare.Core.Language;

/// <summary>
/// Maps ISO 639-1 / 639-2 codes (as used in Matroska / ffprobe tags) to English names.
/// </summary>
public static class LanguageNames
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "English", ["en"] = "English",
        ["spa"] = "Spanish", ["es"] = "Spanish", ["esp"] = "Spanish",
        ["fre"] = "French", ["fra"] = "French", ["fr"] = "French",
        ["ger"] = "German", ["deu"] = "German", ["de"] = "German",
        ["ita"] = "Italian", ["it"] = "Italian",
        ["por"] = "Portuguese", ["pt"] = "Portuguese",
        ["pob"] = "Brazilian Portuguese",
        ["jpn"] = "Japanese", ["ja"] = "Japanese",
        ["chi"] = "Chinese", ["zho"] = "Chinese", ["zh"] = "Chinese",
        ["cmn"] = "Mandarin", ["yue"] = "Cantonese",
        ["kor"] = "Korean", ["ko"] = "Korean",
        ["rus"] = "Russian", ["ru"] = "Russian",
        ["ara"] = "Arabic", ["ar"] = "Arabic",
        ["hin"] = "Hindi", ["hi"] = "Hindi",
        ["tha"] = "Thai", ["th"] = "Thai",
        ["vie"] = "Vietnamese", ["vi"] = "Vietnamese",
        ["pol"] = "Polish", ["pl"] = "Polish",
        ["nld"] = "Dutch", ["dut"] = "Dutch", ["nl"] = "Dutch",
        ["swe"] = "Swedish", ["sv"] = "Swedish",
        ["nor"] = "Norwegian", ["no"] = "Norwegian", ["nob"] = "Norwegian Bokmål",
        ["dan"] = "Danish", ["da"] = "Danish",
        ["fin"] = "Finnish", ["fi"] = "Finnish",
        ["hun"] = "Hungarian", ["hu"] = "Hungarian",
        ["ces"] = "Czech", ["cze"] = "Czech", ["cs"] = "Czech",
        ["slk"] = "Slovak", ["sk"] = "Slovak",
        ["ron"] = "Romanian", ["rum"] = "Romanian", ["ro"] = "Romanian",
        ["tur"] = "Turkish", ["tr"] = "Turkish",
        ["heb"] = "Hebrew", ["he"] = "Hebrew",
        ["ukr"] = "Ukrainian", ["uk"] = "Ukrainian",
        ["ell"] = "Greek", ["gre"] = "Greek", ["el"] = "Greek",
        ["ind"] = "Indonesian", ["id"] = "Indonesian",
        ["may"] = "Malay", ["msa"] = "Malay", ["ms"] = "Malay",
        ["tam"] = "Tamil", ["ta"] = "Tamil",
        ["tel"] = "Telugu", ["te"] = "Telugu",
        ["ben"] = "Bengali", ["bn"] = "Bengali",
        ["urd"] = "Urdu", ["ur"] = "Urdu",
        ["fas"] = "Persian", ["per"] = "Persian", ["fa"] = "Persian",
        ["cat"] = "Catalan", ["ca"] = "Catalan",
        ["hrv"] = "Croatian", ["hr"] = "Croatian",
        ["srp"] = "Serbian", ["sr"] = "Serbian",
        ["bos"] = "Bosnian", ["bs"] = "Bosnian",
        ["slv"] = "Slovenian", ["sl"] = "Slovenian",
        ["bul"] = "Bulgarian", ["bg"] = "Bulgarian",
        ["lit"] = "Lithuanian", ["lt"] = "Lithuanian",
        ["lav"] = "Latvian", ["lv"] = "Latvian",
        ["est"] = "Estonian", ["et"] = "Estonian",
        ["isl"] = "Icelandic", ["is"] = "Icelandic",
        ["gle"] = "Irish", ["ga"] = "Irish",
        ["wel"] = "Welsh", ["cym"] = "Welsh", ["cy"] = "Welsh",
        ["fil"] = "Filipino", ["tgl"] = "Tagalog", ["tl"] = "Tagalog",
        ["und"] = "Undetermined",
        ["zxx"] = "No linguistic content",
    };

    public static string DisplayName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "Unknown";
        var trimmed = code.Trim();
        // Matroska sometimes uses "eng-US" / "pt-BR"
        var primary = trimmed.Split('-', '_')[0];
        if (Map.TryGetValue(primary, out var name))
            return name;
        return trimmed;
    }
}
