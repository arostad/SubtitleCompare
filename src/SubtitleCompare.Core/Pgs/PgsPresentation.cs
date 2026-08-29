using SubtitleCompare.Core.Ocr;

namespace SubtitleCompare.Core.Pgs;

/// <summary>
/// One composed PGS cue: timestamps plus the RGBA bitmap that will be OCR'd.
/// </summary>
public sealed class PgsPresentation
{
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
    public required SubtitleBitmap Bitmap { get; init; }
}
