using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace SubtitleCompare.App;

/// <summary>
/// Follows Windows AppsUseLightTheme and keeps brushes + the DWM caption in sync.
/// </summary>
internal static class Theme
{
    public static bool IsAppsLight { get; private set; } = true;

    public static event EventHandler? Changed;

    public static void Initialize()
    {
        Apply(ReadAppsUseLightTheme());
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Shutdown()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
            return;

        var light = ReadAppsUseLightTheme();
        var app = Application.Current;
        if (app is null)
            return;

        app.Dispatcher.Invoke(() =>
        {
            if (light == IsAppsLight)
                return;
            Apply(light);
            Changed?.Invoke(null, EventArgs.Empty);
        });
    }

    public static bool ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                return i != 0;
        }
        catch
        {
            // fall through to light
        }

        return true;
    }

    public static void Apply(bool light)
    {
        IsAppsLight = light;
        var r = Application.Current.Resources;
        void Set(string key, string hex) => r[key] = Frozen(hex);

        if (light)
        {
            Set("AppBg", "#F4F5F7");
            Set("PanelBg", "#FFFFFF");
            Set("ChromeBg", "#ECEFF3");
            Set("StatusBg", "#EEF1F5");
            Set("BorderBrush", "#D0D4DC");
            Set("MutedFg", "#5C6570");
            Set("EqualFg", "#1A1A1A");
            Set("TimestampFg", "#5A6570");
            Set("RowBg", "#FFFFFF");
            Set("AltRowBg", "#F4F5F7");
            Set("GutterBg", "#E4E7EC");
            Set("EmptyBg", "#FAFBFC");
            Set("EmptyFg", "#2C3340");
            Set("EmptyDash", "#9AA3B2");
            Set("OverlayBg", "#F4F5F7");
            Set("UniqueBg", "#C8E6C9");
            Set("ChangedBg", "#FFF3B0");
            Set("MissingBg", "#F4EEEF");
            Set("MissingFg", "#6E4348");
            Set("MissingAccent", "#B56B73");
            Set("SelectedBg", "#BBDEFB");
            Set("SelectedBorder", "#64B5F6");
            Set("DiffAccent", "#C9A227");
            Set("ErrorFg", "#B71C1C");
            Set("BannerBg", "#FFF3CD");
            Set("BannerFg", "#5C4A00");
            Set("BannerBorder", "#E0C36A");
            Set("ButtonBg", "#FFFFFF");
            Set("ButtonBorder", "#C5CAD3");
            Set("ButtonFg", "#1A1A1A");
            Set("ComboBg", "#FFFFFF");
            Set("ComboFg", "#1A1A1A");
            Set("ButtonHover", "#E8EBF0");
            Set("ComboHover", "#E8EBF0");
            Set("ScrollTrack", "#E8EBF0");
            Set("ScrollThumb", "#B0B6C0");
            Set("ScrollThumbHover", "#8B93A0");
            OverrideSystem(r, light: true);
        }
        else
        {
            Set("AppBg", "#1B1D21");
            Set("PanelBg", "#25272C");
            Set("ChromeBg", "#22242A");
            Set("StatusBg", "#22242A");
            Set("BorderBrush", "#3C4048");
            Set("MutedFg", "#A8B0BA");
            Set("EqualFg", "#E8EAED");
            Set("TimestampFg", "#9AA3B0");
            Set("RowBg", "#25272C");
            Set("AltRowBg", "#1F2126");
            Set("GutterBg", "#181A1E");
            Set("EmptyBg", "#25272C");
            Set("EmptyFg", "#E8EAED");
            Set("EmptyDash", "#6B7280");
            Set("OverlayBg", "#1F2126");
            Set("UniqueBg", "#1F4A32");
            Set("ChangedBg", "#4A3F1A");
            Set("MissingBg", "#2A2224");
            Set("MissingFg", "#E4C4C8");
            Set("MissingAccent", "#C98A90");
            Set("SelectedBg", "#1E3A5F");
            Set("SelectedBorder", "#64B5F6");
            Set("DiffAccent", "#C9A227");
            Set("ErrorFg", "#F28B82");
            Set("BannerBg", "#3D3420");
            Set("BannerFg", "#F5E6A8");
            Set("BannerBorder", "#8A7340");
            Set("ButtonBg", "#2C2F36");
            Set("ButtonBorder", "#4A4E58");
            Set("ButtonFg", "#E8EAED");
            Set("ComboBg", "#2C2F36");
            Set("ComboFg", "#E8EAED");
            Set("ButtonHover", "#3A3E48");
            Set("ComboHover", "#3A3E48");
            Set("ScrollTrack", "#1A1C20");
            Set("ScrollThumb", "#5A616C");
            Set("ScrollThumbHover", "#7A8290");
            OverrideSystem(r, light: false);
        }
    }

    private static void OverrideSystem(ResourceDictionary r, bool light)
    {
        r[SystemColors.WindowBrushKey] = Frozen(light ? "#FFFFFF" : "#2C2F36");
        r[SystemColors.WindowTextBrushKey] = Frozen(light ? "#1A1A1A" : "#E8EAED");
        r[SystemColors.ControlBrushKey] = Frozen(light ? "#FFFFFF" : "#2C2F36");
        r[SystemColors.ControlTextBrushKey] = Frozen(light ? "#1A1A1A" : "#E8EAED");
        r[SystemColors.ControlLightBrushKey] = Frozen(light ? "#F4F5F7" : "#3A3D45");
        r[SystemColors.ControlDarkBrushKey] = Frozen(light ? "#C5CAD3" : "#181A1E");
        r[SystemColors.GrayTextBrushKey] = Frozen(light ? "#5C6570" : "#A8B0BA");
        r[SystemColors.HighlightBrushKey] = Frozen(light ? "#BBDEFB" : "#1E3A5F");
        r[SystemColors.HighlightTextBrushKey] = Frozen(light ? "#1A1A1A" : "#E8EAED");
        r[SystemColors.InactiveSelectionHighlightBrushKey] = Frozen(light ? "#E3F2FD" : "#243044");
        r[SystemColors.InactiveSelectionHighlightTextBrushKey] = Frozen(light ? "#1A1A1A" : "#E8EAED");
    }

    public static Brush Get(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;

    public static void ApplyCaption(Window window)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource src)
            return;

        var dark = IsAppsLight ? 0 : 1;
        DwmSetWindowAttribute(src.Handle, 20, ref dark, sizeof(int));
        DwmSetWindowAttribute(src.Handle, 19, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static SolidColorBrush Frozen(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex)!;
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
