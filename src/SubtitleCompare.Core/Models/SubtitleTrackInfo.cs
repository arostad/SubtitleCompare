namespace SubtitleCompare.Core.Models;

/// <summary>
/// One subtitle stream from a container, as reported by ffprobe.
/// <see cref="Index"/> is the 0-based subtitle-only index used with <c>-map 0:s:N</c>.
/// <see cref="StreamIndex"/> is the overall stream index in the file.
/// </summary>
public sealed class SubtitleTrackInfo
{
    public int Index { get; init; }
    public int StreamIndex { get; init; }
    public string? Language { get; init; }
    public string? Title { get; init; }
    public string? CodecName { get; init; }
    public string? CodecLongName { get; init; }
    public bool IsForced { get; init; }
    public bool IsHearingImpaired { get; init; }
    public bool IsImageBased { get; init; }

    public bool IsTextBased => !IsImageBased;

    /// <summary>
    /// True for Blu-ray PGS streams that can be OCR'd from a raw <c>.sup</c> extract.
    /// </summary>
    public bool IsPgs
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CodecName))
                return false;
            if (CodecName.Contains("text", StringComparison.OrdinalIgnoreCase))
                return false;
            return CodecName.Contains("pgs", StringComparison.OrdinalIgnoreCase);
        }
    }
}
