namespace SubtitleCompare.Core.Ui;

/// <summary>
/// When a loaded file still has (none) on a pane, show the pick-track arrow
/// unless that pane already has an overlay (extracting, OCR, error, no cues).
/// </summary>
public static class PickTrackHintVisibility
{
    public static bool ShouldShow(bool fileLoaded, bool trackSelected, bool overlayVisible) =>
        fileLoaded && !trackSelected && !overlayVisible;
}
