using System.Text;
using SubtitleCompare.Core.Models;

namespace SubtitleCompare.Core.Language;

public static class TrackLabelFormatter
{
    /// <summary>
    /// Formats a track like <c>3 — English (subrip) "Signs" [forced]</c>.
    /// The leading number is the 1-based subtitle-stream index.
    /// </summary>
    public static string Format(SubtitleTrackInfo track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var sb = new StringBuilder();
        sb.Append(track.Index + 1);
        sb.Append(" — ");
        sb.Append(LanguageNames.DisplayName(track.Language));

        var codec = string.IsNullOrWhiteSpace(track.CodecName) ? "unknown" : track.CodecName;
        sb.Append(" (");
        sb.Append(codec);
        sb.Append(')');

        if (!string.IsNullOrWhiteSpace(track.Title))
        {
            sb.Append(" \"");
            sb.Append(track.Title.Trim());
            sb.Append('"');
        }

        if (track.IsForced)
            sb.Append(" [forced]");
        if (track.IsHearingImpaired)
            sb.Append(" [SDH]");

        return sb.ToString();
    }
}
