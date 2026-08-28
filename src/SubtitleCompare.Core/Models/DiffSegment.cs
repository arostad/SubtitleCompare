namespace SubtitleCompare.Core.Models;

public enum DiffKind
{
    Equal,
    Unique,
    Changed,
}

public readonly record struct DiffSegment(string Text, DiffKind Kind);
