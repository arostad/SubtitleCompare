using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.App;

/// <summary>
/// Follows Windows AppsUseLightTheme unless the user has saved a Light/Dark
/// override in <c>%LOCALAPPDATA%\SubtitleCompare\theme.txt</c>. Keeps brushes
/// and the DWM caption in sync.
/// </summary>
internal static class Theme
{
    public static bool IsAppsLight { get; private set; } = true;

    public static bool HasOverride { get; private set; }

    public static event EventHandler? Changed;

    public static void Initialize()
    {
        var saved = ThemePreference.Load();
        HasOverride = saved is not null;
        Apply(ThemePreference.Resolve(saved, ReadAppsUseLightTheme()));
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Shutdown()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    public static void Toggle()
    {
        var next = ThemePreference.Toggle(IsAppsLight);
        HasOverride = true;
        ThemePreference.Save(next);
        Apply(next);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (HasOverride)
            return;
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
            return;

        var light = ReadAppsUseLightTheme();
        var app = Application.Current;
        if (app is null)
            return;

        app.Dispatcher.Invoke(() =>
        {
            if (HasOverride || light == IsAppsLight)
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
        foreach (var (key, hex) in ThemePalette.For(light))
            r[key] = Frozen(hex);
        OverrideSystem(r, light);
        if (Application.Current is { } app)
        {
            foreach (Window window in app.Windows)
                ApplyCaption(window);
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
