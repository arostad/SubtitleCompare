namespace SubtitleCompare.Tests;

internal static class SampleFiles
{
    public static string Dir
    {
        get
        {
            var fromOutput = Path.Combine(AppContext.BaseDirectory, "samples");
            if (Directory.Exists(fromOutput))
                return fromOutput;

            var walk = new DirectoryInfo(AppContext.BaseDirectory);
            while (walk is not null)
            {
                var candidate = Path.Combine(walk.FullName, "samples");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "track-a.srt")))
                    return candidate;
                walk = walk.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate samples/ next to the test output or repo root.");
        }
    }

    public static string A => Path.Combine(Dir, "track-a.srt");
    public static string B => Path.Combine(Dir, "track-b.srt");
    public static string C => Path.Combine(Dir, "track-c.srt");
}
