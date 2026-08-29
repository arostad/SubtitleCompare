using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace SubtitleCompare.App;

internal sealed class UpdateInfo
{
    public required string RemoteVersion { get; init; }
    public required bool IsNewer { get; init; }
    public string? Error { get; init; }
}

internal static class UpdateChecker
{
    private const string VersionUrl =
        "https://github.com/arostad/subtitle-compare/releases/download/latest/version.txt";
    private const string ExeUrl =
        "https://github.com/arostad/subtitle-compare/releases/download/latest/SubtitleCompare.exe";
    private const string ExeFileName = "SubtitleCompare.exe";
    private const long MinExeBytes = 1_000_000;

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromMinutes(15),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SubtitleCompare");
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
        };
        client.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
        return client;
    }

    private static string FreshUrl(string url)
    {
        var sep = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{sep}t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    internal static bool LooksLikeSingleFileExtractPath(string path)
    {
        var full = Path.GetFullPath(path);
        var temp = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(full, temp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return full.Contains($"{Path.DirectorySeparatorChar}.net{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    internal static string ResolveInstalledExe()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && !LooksLikeSingleFileExtractPath(processPath))
            return Path.GetFullPath(processPath);

        var installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SubtitleCompare",
            ExeFileName);
        if (File.Exists(installed))
            return Path.GetFullPath(installed);

        if (!string.IsNullOrWhiteSpace(processPath))
            return Path.GetFullPath(processPath);

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            ExeFileName));
    }

    public static UpdateInfo Check()
    {
        try
        {
            var remoteText = Http.GetStringAsync(FreshUrl(VersionUrl)).GetAwaiter().GetResult()?.Trim();
            if (string.IsNullOrWhiteSpace(remoteText))
            {
                return new UpdateInfo
                {
                    RemoteVersion = "",
                    IsNewer = false,
                    Error = "Could not read the latest version from GitHub.",
                };
            }

            var firstLine = remoteText.Split('\n', '\r')[0].Trim();
            if (!Version.TryParse(firstLine, out var remote))
            {
                return new UpdateInfo { RemoteVersion = firstLine, IsNewer = false, Error = "Latest version string was not readable." };
            }

            return new UpdateInfo
            {
                RemoteVersion = firstLine,
                IsNewer = remote > AppVersion.Parsed,
            };
        }
        catch (Exception ex)
        {
            return new UpdateInfo { RemoteVersion = "", IsNewer = false, Error = ex.Message };
        }
    }

    public static void DownloadAndRestart()
    {
        var exe = ResolveInstalledExe();
        var dir = Path.GetDirectoryName(exe)
            ?? throw new InvalidOperationException("Could not resolve the install directory.");
        Directory.CreateDirectory(dir);
        var pending = Path.Combine(dir, ExeFileName + ".new");

        using (var response = Http.GetAsync(FreshUrl(ExeUrl), HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
        {
            response.EnsureSuccessStatusCode();
            using var input = response.Content.ReadAsStream();
            using var output = File.Create(pending);
            input.CopyTo(output);
        }

        if (!File.Exists(pending) || new FileInfo(pending).Length < MinExeBytes)
            throw new InvalidOperationException("Download failed.");

        var pid = Environment.ProcessId;
        var errorFile = Path.Combine(dir, "update-error.txt");
        var bat = Path.Combine(Path.GetTempPath(), $"subtitle-compare-update-{pid}.cmd");
        var batText = $"""
            @echo off
            setlocal EnableDelayedExpansion
            :wait
            tasklist /FI "PID eq {pid}" | find "{pid}" >nul
            if not errorlevel 1 (
              timeout /t 1 /nobreak >nul
              goto wait
            )
            set TRY=0
            :retry
            set /a TRY+=1
            move /Y "{pending}" "{exe}"
            if errorlevel 1 (
              if !TRY! LSS 8 (
                timeout /t 1 /nobreak >nul
                goto retry
              )
              echo Update failed: could not replace "{exe}". > "{errorFile}"
              echo The downloaded file is still at "{pending}". >> "{errorFile}"
              exit /b 1
            )
            if exist "{exe}" (
              for %%A in ("{exe}") do if %%~zA GEQ 1000000 goto launch
            )
            echo Update failed: "{exe}" is missing or smaller than 1MB after replace. > "{errorFile}"
            exit /b 1
            :launch
            if exist "{errorFile}" del "{errorFile}"
            start "" "{exe}"
            del "%~f0"
            """;
        File.WriteAllText(bat, batText, Encoding.ASCII);
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{bat}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }
}
