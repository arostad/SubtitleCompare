using System.Text;
using System.Text.RegularExpressions;

namespace SubtitleCompare.Core.Parsing;

/// <summary>
/// Strips ASS/SSA override tags and drawing commands from cue text.
/// </summary>
public static class AssTagStripper
{
    private static readonly Regex OverrideTag = new(@"\{[^}]*\}", RegexOptions.Compiled);
    private static readonly Regex DrawingPayload = new(@"\{\s*\\p[1-9][^}]*\}.*?\{\s*\\p0[^}]*\}", RegexOptions.Compiled | RegexOptions.Singleline);

    public static string Strip(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var s = text.Replace("\\h", " ", StringComparison.Ordinal);
        s = DrawingPayload.Replace(s, "");
        s = OverrideTag.Replace(s, "");
        s = s.Replace("\\N", "\n", StringComparison.Ordinal)
             .Replace("\\n", "\n", StringComparison.Ordinal);
        // Collapse leftover escaped braces etc. but keep user text.
        return s;
    }

    /// <summary>Normalize whitespace for comparison without destroying newlines entirely.</summary>
    public static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        var sb = new StringBuilder(text.Length);
        var lastWs = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWs)
                {
                    sb.Append(' ');
                    lastWs = true;
                }
            }
            else
            {
                sb.Append(ch);
                lastWs = false;
            }
        }
        return sb.ToString().Trim();
    }
}
