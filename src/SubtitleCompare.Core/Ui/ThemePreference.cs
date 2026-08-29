namespace SubtitleCompare.Core.Ui;

/// <summary>
/// Optional Light/Dark override stored as a one-word file under
/// <c>%LOCALAPPDATA%\SubtitleCompare\theme.txt</c>. Missing or unreadable
/// means follow the OS theme.
/// </summary>
public static class ThemePreference
{
    public const string FileName = "theme.txt";

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SubtitleCompare");

    public static string DefaultFilePath => Path.Combine(DefaultDirectory, FileName);

    /// <summary>true = light, false = dark, null = follow the OS.</summary>
    public static bool? Load(string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;
        try
        {
            if (!File.Exists(path))
                return null;
            var text = File.ReadAllText(path).Trim();
            if (text.Equals("light", StringComparison.OrdinalIgnoreCase))
                return true;
            if (text.Equals("dark", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        catch
        {
            // follow the OS
        }

        return null;
    }

    public static void Save(bool light, string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, light ? "light" : "dark");
    }

    public static bool Resolve(bool? saved, bool osIsLight) => saved ?? osIsLight;

    public static bool Toggle(bool currentIsLight) => !currentIsLight;
}
