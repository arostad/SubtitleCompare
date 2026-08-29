using System.Diagnostics;
using System.IO;
using System.Net.Http;
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

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromMinutes(15),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SubtitleCompare");
        return client;
    }

    public static UpdateInfo Check()
    {
        try
        {
            var remoteText = Http.GetStringAsync(VersionUrl).GetAwaiter().GetResult()?.Trim();
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
        var dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var exe = Environment.ProcessPath ?? Path.Combine(dir, "SubtitleCompare.exe");
        var pending = Path.Combine(dir, "SubtitleCompare.exe.new");

        using (var response = Http.GetAsync(ExeUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
        {
            response.EnsureSuccessStatusCode();
            using var input = response.Content.ReadAsStream();
            using var output = File.Create(pending);
            input.CopyTo(output);
        }

        if (!File.Exists(pending) || new FileInfo(pending).Length < 1_000_000)
            throw new InvalidOperationException("Download failed.");

        var pid = Environment.ProcessId;
        var bat = Path.Combine(Path.GetTempPath(), $"subtitle-compare-update-{pid}.cmd");
        var batText = $"""
            @echo off
            :wait
            tasklist /FI "PID eq {pid}" | find "{pid}" >nul
            if not errorlevel 1 (
              timeout /t 1 /nobreak >nul
              goto wait
            )
            move /Y "{pending}" "{exe}" >nul
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
