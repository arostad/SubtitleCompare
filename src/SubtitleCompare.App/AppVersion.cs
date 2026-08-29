using System.Reflection;

namespace SubtitleCompare.App;

internal static class AppVersion
{
    public const string Repo = "arostad/SubtitleCompare";

    public static string Current
    {
        get
        {
            var info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                return plus >= 0 ? info[..plus] : info;
            }

            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "1.1.25" : $"{v.Major}.{v.Minor}.{v.Build:00}";
        }
    }

    public static Version Parsed
    {
        get
        {
            var s = Current.Trim();
            return Version.TryParse(s, out var v) ? v : new Version(1, 0, 0);
        }
    }
}
