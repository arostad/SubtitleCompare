using System.Text.RegularExpressions;

namespace SubtitleCompare.Core.Diagnostics;

/// <summary>
/// Strips usernames, machine names, filesystem paths, media titles, and emails
/// from diagnostic text so a debug report can be shared.
/// </summary>
public static class Anon
{
    private static readonly Regex WindowsUserPath = new(
        @"[A-Za-z]:\\Users\\[^\\/:\s""']+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MacUserPath = new(
        @"/Users/[^/\s""']+",
        RegexOptions.Compiled);

    private static readonly Regex LinuxUserPath = new(
        @"/home/[^/\s""']+",
        RegexOptions.Compiled);

    private static readonly Regex MediaFileName = new(
        @"[^\s\\/:*?""<>|]+\.(?:mkv|mka|mks|mp4|m4v|avi|mov|wmv|webm|ts|m2ts|srt|ass|ssa|vtt|sub|idx|sup|pgs)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Email = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Reports only whether an environment variable is set, never its value.
    /// </summary>
    public static string EnvVar(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "not set" : "set";

    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? "";

        var s = value;

        // Most-specific known folders first so LocalAppData/Temp are not left as
        // %USERPROFILE%\AppData\Local\...
        s = ReplacePath(s, Path.GetTempPath(), "%TEMP%");
        s = ReplacePath(s, Environment.GetEnvironmentVariable("TEMP"), "%TEMP%");
        s = ReplacePath(s, Environment.GetEnvironmentVariable("TMP"), "%TEMP%");
        s = ReplacePath(s, Folder(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%");
        s = ReplacePath(s, Environment.GetEnvironmentVariable("LOCALAPPDATA"), "%LOCALAPPDATA%");
        s = ReplacePath(s, Folder(Environment.SpecialFolder.ApplicationData), "%APPDATA%");
        s = ReplacePath(s, Environment.GetEnvironmentVariable("APPDATA"), "%APPDATA%");
        s = ReplacePath(s, Folder(Environment.SpecialFolder.DesktopDirectory), "%USERPROFILE%\\Desktop");
        s = ReplacePath(s, Folder(Environment.SpecialFolder.MyDocuments), "%USERPROFILE%\\Documents");
        s = ReplacePath(s, Folder(Environment.SpecialFolder.UserProfile), "%USERPROFILE%");
        s = ReplacePath(s, Environment.GetEnvironmentVariable("USERPROFILE"), "%USERPROFILE%");
        s = ReplacePath(s, Environment.GetEnvironmentVariable("HOME"), "%USERPROFILE%");

        s = WindowsUserPath.Replace(s, @"C:\Users\<user>");
        s = MacUserPath.Replace(s, "/Users/<user>");
        s = LinuxUserPath.Replace(s, "/home/<user>");

        s = ReplaceIgnoreCase(s, @"C:\Users\<user>\AppData\Local", "%LOCALAPPDATA%");
        s = ReplaceIgnoreCase(s, @"C:\Users\<user>\AppData\Roaming", "%APPDATA%");
        s = ReplaceIgnoreCase(s, @"C:/Users/<user>/AppData/Local", "%LOCALAPPDATA%");
        s = ReplaceIgnoreCase(s, @"C:/Users/<user>/AppData/Roaming", "%APPDATA%");

        s = ReplaceWord(s, Environment.UserName, "<user>");
        s = ReplaceWord(s, Environment.GetEnvironmentVariable("USERNAME"), "<user>");
        s = ReplaceWord(s, Environment.UserDomainName, "<domain>");
        s = ReplaceWord(s, Environment.MachineName, "<machine>");
        s = ReplaceWord(s, Environment.GetEnvironmentVariable("COMPUTERNAME"), "<machine>");
        s = ReplaceWord(s, Environment.GetEnvironmentVariable("USERDOMAIN"), "<domain>");

        s = MediaFileName.Replace(s, "<media>");
        s = Email.Replace(s, "<email>");
        return s;
    }

    private static string? Folder(Environment.SpecialFolder folder)
    {
        try
        {
            return Environment.GetFolderPath(folder);
        }
        catch
        {
            return null;
        }
    }

    private static string ReplacePath(string text, string? path, string token)
    {
        if (string.IsNullOrWhiteSpace(path))
            return text;

        var trimmed = path.TrimEnd('\\', '/');
        if (trimmed.Length < 4)
            return text;

        text = ReplaceIgnoreCase(text, trimmed, token);
        var flipped = trimmed.Contains('\\')
            ? trimmed.Replace('\\', '/')
            : trimmed.Replace('/', '\\');
        if (!string.Equals(flipped, trimmed, StringComparison.Ordinal))
            text = ReplaceIgnoreCase(text, flipped, token);
        return text;
    }

    private static string ReplaceWord(string text, string? word, string token)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 2)
            return text;
        return Regex.Replace(text, $@"\b{Regex.Escape(word)}\b", token, RegexOptions.IgnoreCase);
    }

    private static string ReplaceIgnoreCase(string text, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(oldValue))
            return text;
        return Regex.Replace(text, Regex.Escape(oldValue), newValue, RegexOptions.IgnoreCase);
    }
}
