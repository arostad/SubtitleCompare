using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
        "https://github.com/arostad/SubtitleCompare/releases/download/latest/version.txt";
    private const string ExeUrl =
        "https://github.com/arostad/SubtitleCompare/releases/download/latest/SubtitleCompare.exe";
    private const string ChecksumUrl =
        "https://github.com/arostad/SubtitleCompare/releases/download/latest/SubtitleCompare.exe.sha256";
    private const string ExeFileName = "SubtitleCompare.exe";
    private const long MinExeBytes = 1_000_000;
    private const long MaxExeBytes = 1_000_000_000;
    private const int MaxRedirects = 5;

    private static readonly HashSet<string> TrustedDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    };

    private static readonly HttpClient Http = CreateClient();
    private static readonly Regex ChecksumPattern = new(
        @"\A(?<hash>[0-9a-fA-F]{64})(?:\s+[* ]?SubtitleCompare\.exe)?\s*\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
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

    private static HttpResponseMessage GetTrusted(string url)
    {
        var current = new Uri(FreshUrl(url), UriKind.Absolute);
        for (var redirect = 0; ; redirect++)
        {
            EnsureTrustedUri(current);
            var response = Http.GetAsync(current, HttpCompletionOption.ResponseHeadersRead)
                .GetAwaiter().GetResult();
            if (!IsRedirect(response.StatusCode))
                return response;

            if (redirect >= MaxRedirects)
            {
                response.Dispose();
                throw new HttpRequestException("The download used too many redirects.");
            }

            var location = response.Headers.Location;
            response.Dispose();
            if (location is null)
                throw new HttpRequestException("The download redirect had no destination.");
            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }
    }

    private static void EnsureTrustedUri(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !TrustedDownloadHosts.Contains(uri.IdnHost)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new HttpRequestException("The download was redirected to an untrusted location.");
        }
    }

    private static bool IsRedirect(System.Net.HttpStatusCode status) =>
        status is System.Net.HttpStatusCode.Moved
            or System.Net.HttpStatusCode.Redirect
            or System.Net.HttpStatusCode.RedirectMethod
            or System.Net.HttpStatusCode.TemporaryRedirect
            or System.Net.HttpStatusCode.PermanentRedirect;

    private static string ReadChecksum()
    {
        using var response = GetTrusted(ChecksumUrl);
        response.EnsureSuccessStatusCode();
        using var input = response.Content.ReadAsStream();
        using var reader = new StreamReader(input, Encoding.ASCII, detectEncodingFromByteOrderMarks: false);
        var buffer = new char[1025];
        var count = reader.ReadBlock(buffer, 0, buffer.Length);
        if (count > 1024)
            throw new InvalidOperationException("The update checksum was not readable.");

        var match = ChecksumPattern.Match(new string(buffer, 0, count));
        if (!match.Success)
            throw new InvalidOperationException("The update checksum was not readable.");
        return match.Groups["hash"].Value.ToUpperInvariant();
    }

    private static void DownloadExe(string destination)
    {
        using var response = GetTrusted(ExeUrl);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxExeBytes)
            throw new InvalidOperationException("The update download was unexpectedly large.");

        using var input = response.Content.ReadAsStream();
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaxExeBytes)
                throw new InvalidOperationException("The update download was unexpectedly large.");
            output.Write(buffer, 0, read);
        }
    }

    private static string Sha256(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private static (string Path, string Hash) DownloadVerifiedExe(string directory)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var expectedHash = ReadChecksum();
            var pending = Path.Combine(directory, $"{ExeFileName}.{Guid.NewGuid():N}.new");
            try
            {
                DownloadExe(pending);
                if (new FileInfo(pending).Length < MinExeBytes)
                    throw new InvalidOperationException("Download failed.");
                if (string.Equals(Sha256(pending), expectedHash, StringComparison.OrdinalIgnoreCase))
                    return (pending, expectedHash);
            }
            catch
            {
                try { File.Delete(pending); } catch { /* best effort */ }
                throw;
            }

            try { File.Delete(pending); } catch { /* best effort */ }
        }

        throw new InvalidOperationException("The downloaded update failed its integrity check.");
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
        if (!string.IsNullOrWhiteSpace(processPath) && LooksLikeSingleFileExtractPath(processPath))
            return Path.GetFullPath(installed);

        if (File.Exists(installed))
            return Path.GetFullPath(installed);

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            ExeFileName));
    }

    public static UpdateInfo Check()
    {
        try
        {
            using var response = GetTrusted(VersionUrl);
            response.EnsureSuccessStatusCode();
            var remoteText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult()?.Trim();
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
        var (pending, expectedHash) = DownloadVerifiedExe(dir);

        var pid = Environment.ProcessId;
        var errorFile = Path.Combine(dir, "update-error.txt");
        var script = Path.Combine(dir, $".SubtitleCompare-update-{Guid.NewGuid():N}.ps1");
        const string scriptText = """
            $ErrorActionPreference = "Stop"
            $pending = $env:SUBTITLECOMPARE_UPDATE_PENDING
            $exe = $env:SUBTITLECOMPARE_UPDATE_EXE
            $expectedHash = $env:SUBTITLECOMPARE_UPDATE_SHA256
            $errorFile = $env:SUBTITLECOMPARE_UPDATE_ERROR
            try {
                $process = Get-Process -Id ([int]$env:SUBTITLECOMPARE_UPDATE_PID) -ErrorAction SilentlyContinue
                if ($process) { $process.WaitForExit() }
                if ((Get-FileHash -LiteralPath $pending -Algorithm SHA256).Hash -ne $expectedHash) {
                    throw "The downloaded update failed its integrity check."
                }
                for ($attempt = 0; $attempt -lt 8; $attempt++) {
                    try {
                        Move-Item -LiteralPath $pending -Destination $exe -Force
                        break
                    } catch {
                        if ($attempt -eq 7) { throw }
                        Start-Sleep -Seconds 1
                    }
                }
                if (-not (Test-Path -LiteralPath $exe) -or (Get-Item -LiteralPath $exe).Length -lt 1MB) {
                    throw "The updated executable is missing or too small."
                }
                Remove-Item -LiteralPath $errorFile -Force -ErrorAction SilentlyContinue
                Start-Process -FilePath $exe
            } catch {
                "Update failed: $($_.Exception.Message)" | Set-Content -LiteralPath $errorFile
                if (Test-Path -LiteralPath $exe) {
                    Start-Process -FilePath $exe
                }
            } finally {
                Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
            }
            """;
        using (var stream = new FileStream(script, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            writer.Write(scriptText);

        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.Environment["SUBTITLECOMPARE_UPDATE_PENDING"] = pending;
        start.Environment["SUBTITLECOMPARE_UPDATE_EXE"] = exe;
        start.Environment["SUBTITLECOMPARE_UPDATE_SHA256"] = expectedHash;
        start.Environment["SUBTITLECOMPARE_UPDATE_ERROR"] = errorFile;
        start.Environment["SUBTITLECOMPARE_UPDATE_PID"] = pid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            if (Process.Start(start) is null)
                throw new InvalidOperationException("Could not start the update helper.");
        }
        catch
        {
            try { File.Delete(script); } catch { /* best effort */ }
            try { File.Delete(pending); } catch { /* best effort */ }
            throw;
        }
    }
}
