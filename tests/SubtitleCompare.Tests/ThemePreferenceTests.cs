using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.Tests;

public class ThemePreferenceTests
{
    [Fact]
    public void Missing_file_means_follow_the_os()
    {
        var path = Path.Combine(Path.GetTempPath(), "SubtitleCompare-theme-tests", Guid.NewGuid().ToString("N"), "theme.txt");
        Assert.Null(ThemePreference.Load(path));
    }

    [Theory]
    [InlineData("light", true)]
    [InlineData("LIGHT", true)]
    [InlineData(" light \n", true)]
    [InlineData("dark", false)]
    [InlineData("Dark", false)]
    public void Reads_light_or_dark(string contents, bool expectedLight)
    {
        var path = WriteTemp(contents);
        Assert.Equal(expectedLight, ThemePreference.Load(path));
    }

    [Fact]
    public void Garbage_file_means_follow_the_os()
    {
        var path = WriteTemp("purple");
        Assert.Null(ThemePreference.Load(path));
    }

    [Theory]
    [InlineData(true, "light")]
    [InlineData(false, "dark")]
    public void Save_writes_a_one_word_file(bool light, string expected)
    {
        var path = Path.Combine(Path.GetTempPath(), "SubtitleCompare-theme-tests", Guid.NewGuid().ToString("N"), "theme.txt");
        ThemePreference.Save(light, path);
        Assert.Equal(expected, File.ReadAllText(path).Trim());
        Assert.Equal(light, ThemePreference.Load(path));
    }

    [Fact]
    public void First_run_uses_the_os_theme()
    {
        Assert.False(ThemePreference.Resolve(saved: null, osIsLight: false));
        Assert.True(ThemePreference.Resolve(saved: null, osIsLight: true));
    }

    [Fact]
    public void Saved_override_wins_over_the_os()
    {
        Assert.True(ThemePreference.Resolve(saved: true, osIsLight: false));
        Assert.False(ThemePreference.Resolve(saved: false, osIsLight: true));
    }

    [Fact]
    public void Click_flips_to_the_opposite_theme()
    {
        Assert.False(ThemePreference.Toggle(currentIsLight: true));
        Assert.True(ThemePreference.Toggle(currentIsLight: false));
    }

    [Fact]
    public void Toggle_from_light_restyles_chrome_to_the_dark_palette()
    {
        var next = ThemePreference.Toggle(currentIsLight: true);
        var colors = ThemePalette.For(next);
        Assert.Equal("#1B1D21", colors["AppBg"]);
        Assert.Equal("#22242A", colors["ChromeBg"]);
        Assert.Equal("#A8B0BA", colors["MutedFg"]);
        Assert.Equal("#E8EAED", colors["EqualFg"]);
        Assert.Equal("#2C2F36", colors["ButtonBg"]);
        Assert.Equal("#1A1C20", colors["ScrollTrack"]);
        Assert.Equal("#22242A", colors["StatusBg"]);
    }

    [Fact]
    public void Toggle_from_dark_restyles_chrome_to_the_light_palette()
    {
        var next = ThemePreference.Toggle(currentIsLight: false);
        var colors = ThemePalette.For(next);
        Assert.Equal("#F4F5F7", colors["AppBg"]);
        Assert.Equal("#ECEFF3", colors["ChromeBg"]);
        Assert.Equal("#5C6570", colors["MutedFg"]);
        Assert.Equal("#1A1A1A", colors["EqualFg"]);
        Assert.Equal("#FFFFFF", colors["ButtonBg"]);
        Assert.Equal("#E8EBF0", colors["ScrollTrack"]);
        Assert.Equal("#EEF1F5", colors["StatusBg"]);
    }

    [Fact]
    public void Light_and_dark_palettes_cover_the_same_keys()
    {
        Assert.Equal(ThemePalette.Light.Keys.OrderBy(k => k), ThemePalette.Dark.Keys.OrderBy(k => k));
    }

    [Fact]
    public void Compare_view_keys_exist_in_both_palettes()
    {
        string[] keys =
        [
            "EqualFg", "RowBg",
            "ChangedBg", "UniqueBg",
            "MissingBg", "MissingFg", "MissingAccent",
        ];
        foreach (var key in keys)
        {
            Assert.Contains(key, ThemePalette.Light.Keys);
            Assert.Contains(key, ThemePalette.Dark.Keys);
        }
    }

    private static string WriteTemp(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), "SubtitleCompare-theme-tests", Guid.NewGuid().ToString("N"), "theme.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }
}
