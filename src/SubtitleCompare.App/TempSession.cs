using System.IO;

namespace SubtitleCompare.App;

internal sealed class TempSession : IDisposable
{
    public TempSession()
    {
        Root = Path.Combine(Path.GetTempPath(), "SubtitleCompare", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup of temp extracts.
        }
    }

    public static void TryDeleteAllSessions()
    {
        try
        {
            var parent = Path.Combine(Path.GetTempPath(), "SubtitleCompare");
            if (Directory.Exists(parent))
                Directory.Delete(parent, recursive: true);
        }
        catch
        {
            // ignore
        }
    }
}
