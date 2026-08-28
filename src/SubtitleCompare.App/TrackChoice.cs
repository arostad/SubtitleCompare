using SubtitleCompare.Core.Language;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.App;

internal sealed class TrackChoice
{
    public static TrackChoice None { get; } = new(null);

    public TrackChoice(SubtitleTrackInfo? track)
    {
        Track = track;
        if (track is null)
        {
            Label = "(none)";
            return;
        }

        Label = TrackLabelFormatter.Format(track);
    }

    public SubtitleTrackInfo? Track { get; }
    public string Label { get; }
    public bool IsNone => Track is null;
    public bool IsImage => Track?.IsImageBased == true;
    public bool IsText => Track is { IsImageBased: false };

    public override string ToString() => Label;
}
