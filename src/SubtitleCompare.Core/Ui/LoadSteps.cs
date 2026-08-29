namespace SubtitleCompare.Core.Ui;

/// <summary>
/// Casual per-step wording for pane overlays and the status bar.
/// No filenames, paths, or subtitle text.
/// </summary>
public static class LoadSteps
{
    public const string PullingTrack = "pulling the track out of the MKV";
    public const string ReadingSrt = "reading the SRT";
    public const string ParsingPgs = "parsing PGS";
    public const string StartingOcr = "starting OCR";

    public static string DownloadingOcrData(string? languageDisplayName)
    {
        var name = string.IsNullOrWhiteSpace(languageDisplayName)
            ? "English"
            : languageDisplayName.Trim();
        return $"downloading {name} OCR data";
    }
}
