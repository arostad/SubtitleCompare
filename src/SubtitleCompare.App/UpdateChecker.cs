using System.Diagnostics;
using System.IO;
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
    public static UpdateInfo Check()
    {
        try
        {
            var gh = ResolveGh();
            if (gh is null)
            {
                return new UpdateInfo
                {
                    RemoteVersion = "",
                    IsNewer = false,
                    Error = "GitHub CLI is not installed. Updates use the same gh login as the installer.",
                };
            }

            var remoteText = TryDownloadReleaseText(gh, "version.txt")?.Trim();
            if (string.IsNullOrWhiteSpace(remoteText))
            {
                return new UpdateInfo
                {
                    RemoteVersion = "",
                    IsNewer = false,
                    Error = "Could not read the latest version from GitHub. Is gh signed in?",
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
        var gh = ResolveGh() ?? throw new InvalidOperationException("GitHub CLI (gh) was not found.");
        var dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var exe = Environment.ProcessPath ?? Path.Combine(dir, "SubtitleCompare.exe");
        var pending = Path.Combine(dir, "SubtitleCompare.exe.new");

        var download = Run(gh, [
            "release", "download", "latest",
            "--repo", AppVersion.Repo,
            "--pattern", "SubtitleCompare.exe",
            "--clobber",
            "--output", pending,
        ], timeoutMs: 15 * 60 * 1000);

        if (download.ExitCode != 0 || !File.Exists(pending) || new FileInfo(pending).Length < 1_000_000)
        {
            var err = string.IsNullOrWhiteSpace(download.Stderr) ? download.Stdout : download.Stderr;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(err) ? "Download failed." : err.Trim());
        }

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

    private static string? TryDownloadReleaseText(string gh, string name)
    {
        var dest = Path.Combine(Path.GetTempPath(), $"sc-{name}");
        try { if (File.Exists(dest)) File.Delete(dest); } catch { /* ignore */ }
        var r = Run(gh, [
            "release", "download", "latest",
            "--repo", AppVersion.Repo,
            "--pattern", name,
            "--clobber",
            "--output", dest,
        ]);
        if (r.ExitCode != 0 || !File.Exists(dest))
            return null;
        return File.ReadAllText(dest);
    }

    private static string? ResolveGh()
    {
        var fromPath = Run("where", ["gh"]).Stdout.Trim().Split('\n', '\r')
            .Select(s => s.Trim())
            .FirstOrDefault(File.Exists);
        if (fromPath is not null)
            return fromPath;

        foreach (var candidate in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHub CLI", "gh.exe"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI", "gh.exe"),
                 })
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private readonly record struct ProcResult(int ExitCode, string Stdout, string Stderr);

    private static ProcResult Run(string file, IReadOnlyList<string> args, int timeoutMs = 60_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {file}.");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException($"{file} timed out.");
        }

        return new ProcResult(p.ExitCode, stdout, stderr);
    }
}
