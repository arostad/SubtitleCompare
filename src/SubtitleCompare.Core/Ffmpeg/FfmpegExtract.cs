using System.Collections.Concurrent;

namespace SubtitleCompare.Core.Ffmpeg;

/// <summary>
/// Extracts one text subtitle stream to a temporary .srt via ffmpeg <c>-map 0:s:N</c>.
/// Results are cached per (file identity, subtitle stream index).
/// </summary>
public sealed class FfmpegExtract
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    private readonly string _outputDirectory;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FfmpegExtract(string? outputDirectory = null)
    {
        _outputDirectory = outputDirectory
                           ?? Path.Combine(Path.GetTempPath(), "SubtitleCompare", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
    }

    public string OutputDirectory => _outputDirectory;

    public string Extract(string filePath, int subtitleStreamIndex, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));
        if (subtitleStreamIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(subtitleStreamIndex));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Media file not found.", filePath);

        var key = CacheKey(filePath, subtitleStreamIndex);
        if (_cache.TryGetValue(key, out var cached) && File.Exists(cached))
            return cached;

        var outPath = Path.Combine(_outputDirectory, $"s{subtitleStreamIndex}_{Guid.NewGuid():N}.srt");
        var args = new[]
        {
            "-y",
            "-i", filePath,
            "-map", $"0:s:{subtitleStreamIndex}",
            "-c:s", "srt",
            outPath,
        };

        var result = FfmpegProcess.Run("ffmpeg", args, timeout ?? DefaultTimeout);
        if (result.TimedOut)
        {
            TryDelete(outPath);
            throw new TimeoutException(
                $"ffmpeg timed out after {(timeout ?? DefaultTimeout).TotalSeconds:0}s extracting subtitle stream {subtitleStreamIndex}.");
        }

        if (result.ExitCode != 0 || !File.Exists(outPath) || new FileInfo(outPath).Length == 0)
        {
            TryDelete(outPath);
            var err = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"ffmpeg exited with code {result.ExitCode}."
                : result.StandardError.Trim();
            throw new InvalidOperationException(
                $"Failed to extract subtitle stream {subtitleStreamIndex} from '{Path.GetFileName(filePath)}':{Environment.NewLine}{err}");
        }

        _cache[key] = outPath;
        return outPath;
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    private static string CacheKey(string filePath, int subtitleStreamIndex)
    {
        var full = Path.GetFullPath(filePath);
        var stamp = File.GetLastWriteTimeUtc(full).Ticks;
        var size = new FileInfo(full).Length;
        return $"{full}|{stamp}|{size}|{subtitleStreamIndex}";
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
