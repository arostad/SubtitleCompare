namespace SubtitleCompare.Core.Ui;

/// <summary>
/// Hex colors for the themed resource keys. <c>Theme.Apply</c> writes these
/// into the WPF resource dictionary so a toggle restyles the whole chrome.
/// </summary>
public static class ThemePalette
{
    public static IReadOnlyDictionary<string, string> For(bool light) => light ? Light : Dark;

    public static readonly IReadOnlyDictionary<string, string> Light = new Dictionary<string, string>
    {
        ["AppBg"] = "#F4F5F7",
        ["PanelBg"] = "#FFFFFF",
        ["ChromeBg"] = "#ECEFF3",
        ["StatusBg"] = "#EEF1F5",
        ["BorderBrush"] = "#D0D4DC",
        ["MutedFg"] = "#5C6570",
        ["EqualFg"] = "#1A1A1A",
        ["TimestampFg"] = "#5A6570",
        ["RowBg"] = "#FFFFFF",
        ["AltRowBg"] = "#F4F5F7",
        ["GutterBg"] = "#E4E7EC",
        ["EmptyBg"] = "#FAFBFC",
        ["EmptyFg"] = "#2C3340",
        ["EmptyDash"] = "#9AA3B2",
        ["OverlayBg"] = "#F4F5F7",
        ["UniqueBg"] = "#C8E6C9",
        ["ChangedBg"] = "#FFF3B0",
        ["MissingBg"] = "#F4EEEF",
        ["MissingFg"] = "#6E4348",
        ["MissingAccent"] = "#B56B73",
        ["SelectedBg"] = "#BBDEFB",
        ["SelectedBorder"] = "#64B5F6",
        ["DiffAccent"] = "#C9A227",
        ["ErrorFg"] = "#B71C1C",
        ["BannerBg"] = "#FFF3CD",
        ["BannerFg"] = "#5C4A00",
        ["BannerBorder"] = "#E0C36A",
        ["ButtonBg"] = "#FFFFFF",
        ["ButtonBorder"] = "#C5CAD3",
        ["ButtonFg"] = "#1A1A1A",
        ["ComboBg"] = "#FFFFFF",
        ["ComboFg"] = "#1A1A1A",
        ["ButtonHover"] = "#E8EBF0",
        ["ComboHover"] = "#E8EBF0",
        ["ScrollTrack"] = "#E8EBF0",
        ["ScrollThumb"] = "#B0B6C0",
        ["ScrollThumbHover"] = "#8B93A0",
    };

    public static readonly IReadOnlyDictionary<string, string> Dark = new Dictionary<string, string>
    {
        ["AppBg"] = "#1B1D21",
        ["PanelBg"] = "#25272C",
        ["ChromeBg"] = "#22242A",
        ["StatusBg"] = "#22242A",
        ["BorderBrush"] = "#3C4048",
        ["MutedFg"] = "#A8B0BA",
        ["EqualFg"] = "#E8EAED",
        ["TimestampFg"] = "#9AA3B0",
        ["RowBg"] = "#25272C",
        ["AltRowBg"] = "#1F2126",
        ["GutterBg"] = "#181A1E",
        ["EmptyBg"] = "#25272C",
        ["EmptyFg"] = "#E8EAED",
        ["EmptyDash"] = "#6B7280",
        ["OverlayBg"] = "#1F2126",
        ["UniqueBg"] = "#1F4A32",
        ["ChangedBg"] = "#4A3F1A",
        ["MissingBg"] = "#2A2224",
        ["MissingFg"] = "#E4C4C8",
        ["MissingAccent"] = "#C98A90",
        ["SelectedBg"] = "#1E3A5F",
        ["SelectedBorder"] = "#64B5F6",
        ["DiffAccent"] = "#C9A227",
        ["ErrorFg"] = "#F28B82",
        ["BannerBg"] = "#3D3420",
        ["BannerFg"] = "#F5E6A8",
        ["BannerBorder"] = "#8A7340",
        ["ButtonBg"] = "#2C2F36",
        ["ButtonBorder"] = "#4A4E58",
        ["ButtonFg"] = "#E8EAED",
        ["ComboBg"] = "#2C2F36",
        ["ComboFg"] = "#E8EAED",
        ["ButtonHover"] = "#3A3E48",
        ["ComboHover"] = "#3A3E48",
        ["ScrollTrack"] = "#1A1C20",
        ["ScrollThumb"] = "#5A616C",
        ["ScrollThumbHover"] = "#7A8290",
    };
}
