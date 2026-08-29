using System.Collections.Concurrent;

namespace SubtitleCompare.Core.Diagnostics;

/// <summary>
/// In-memory ring buffer of already-anonymized diagnostic events.
/// Nothing is written to disk until the user saves a debug report.
/// </summary>
public static class DebugLog
{
    public const int Capacity = 80;

    private static readonly ConcurrentQueue<string> Events = new();

    public static void Info(string message) =>
        Enqueue("info", Anon.Text(message));

    public static void Error(string message, Exception? exception = null)
    {
        var text = Anon.Text(message);
        if (exception is not null)
            text += " | " + FormatException(exception);
        Enqueue("error", text);
    }

    public static IReadOnlyList<string> Snapshot() => Events.ToArray();

    internal static void Clear()
    {
        while (Events.TryDequeue(out _)) { }
    }

    private static void Enqueue(string level, string message)
    {
        Events.Enqueue($"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ} [{level}] {message}");
        while (Events.Count > Capacity && Events.TryDequeue(out _)) { }
    }

    private static string FormatException(Exception exception)
    {
        var parts = new List<string>();
        for (var ex = exception; ex is not null; ex = ex.InnerException)
            parts.Add(ex.GetType().Name);
        return string.Join(" | ", parts);
    }
}
