using System.Text.Json;
using System.Text.Json.Serialization;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Ffmpeg;

/// <summary>
/// Lists subtitle streams in a media file via ffprobe.
/// </summary>
public sealed class FfmpegProbe
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private static readonly HashSet<string> ImageCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "hdmv_pgs_subtitle",
        "pgssub",
        "dvd_subtitle",
        "dvdsub",
        "dvb_subtitle",
        "dvbsub",
        "xsub",
        "hdmv_text_subtitle", // text-in-PGS presentation; treat as image-like / not comparable
        "arib_caption",
    };

    private static readonly HashSet<string> TextCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "subrip", "srt", "ass", "ssa", "webvtt", "mov_text",
        "text", "eia_608", "timed_id3", "ttml", "srt_subtitle",
    };

    public IReadOnlyList<SubtitleTrackInfo> Probe(string filePath, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Media file not found.", filePath);

        // Explicit entries: -show_entries alone can strip codec fields if only tags
        // are listed. Request stream + disposition + the tags the caller asked for.
        var args = new[]
        {
            "-v", "error",
            "-select_streams", "s",
            "-show_streams",
            "-show_entries", "stream=index,codec_name,codec_long_name,codec_type:stream_disposition=forced,hearing_impaired:stream_tags=language,title,BPS,DURATION",
            "-print_format", "json",
            filePath,
        };

        var result = FfmpegProcess.Run("ffprobe", args, timeout ?? DefaultTimeout);
        if (result.TimedOut)
            throw new TimeoutException($"ffprobe timed out after {(timeout ?? DefaultTimeout).TotalSeconds:0}s while probing '{filePath}'.");
        if (result.ExitCode != 0)
        {
            var err = string.IsNullOrWhiteSpace(result.StandardError) ? $"exit {result.ExitCode}" : result.StandardError.Trim();
            throw new InvalidOperationException($"ffprobe failed for '{filePath}': {err}");
        }

        return ParseJson(result.StandardOutput);
    }

    public static IReadOnlyList<SubtitleTrackInfo> ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<SubtitleTrackInfo>();

        ProbeDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<ProbeDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("ffprobe returned JSON that could not be parsed.", ex);
        }

        var streams = doc?.Streams;
        if (streams is null || streams.Count == 0)
            return Array.Empty<SubtitleTrackInfo>();

        var tracks = new List<SubtitleTrackInfo>(streams.Count);
        var subtitleIndex = 0;
        foreach (var s in streams)
        {
            if (!string.IsNullOrEmpty(s.CodecType)
                && !string.Equals(s.CodecType, "subtitle", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? language = null;
            string? title = null;
            var tags = s.Tags;
            tags?.TryGetValue("language", out language);
            tags?.TryGetValue("title", out title);

            var codec = s.CodecName;
            tracks.Add(new SubtitleTrackInfo
            {
                Index = subtitleIndex,
                StreamIndex = s.Index,
                Language = string.IsNullOrWhiteSpace(language) ? null : language,
                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                CodecName = codec,
                CodecLongName = s.CodecLongName,
                IsForced = s.Disposition?.Forced is > 0,
                IsHearingImpaired = s.Disposition?.HearingImpaired is > 0,
                IsImageBased = IsImageCodec(codec),
            });
            subtitleIndex++;
        }

        return tracks;
    }

    internal static bool IsImageCodec(string? codecName)
    {
        if (string.IsNullOrWhiteSpace(codecName))
            return false;
        if (ImageCodecs.Contains(codecName))
            return true;
        if (TextCodecs.Contains(codecName))
            return false;
        // Unknown: assume image if the name hints at bitmap / pgs / vobsub.
        return codecName.Contains("pgs", StringComparison.OrdinalIgnoreCase)
               || codecName.Contains("dvd", StringComparison.OrdinalIgnoreCase)
               || codecName.Contains("vob", StringComparison.OrdinalIgnoreCase)
               || codecName.Contains("dvb", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private sealed class ProbeDocument
    {
        [JsonPropertyName("streams")]
        public List<ProbeStream>? Streams { get; set; }
    }

    private sealed class ProbeStream
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("codec_name")]
        public string? CodecName { get; set; }

        [JsonPropertyName("codec_long_name")]
        public string? CodecLongName { get; set; }

        [JsonPropertyName("codec_type")]
        public string? CodecType { get; set; }

        [JsonPropertyName("disposition")]
        public ProbeDisposition? Disposition { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }

    private sealed class ProbeDisposition
    {
        [JsonPropertyName("forced")]
        public int Forced { get; set; }

        [JsonPropertyName("hearing_impaired")]
        public int HearingImpaired { get; set; }
    }
}
