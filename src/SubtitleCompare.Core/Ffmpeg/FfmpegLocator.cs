namespace SubtitleCompare.Core.Ffmpeg;

public static class FfmpegLocator
{
    public static bool IsAvailable()
    {
        try
        {
            var result = FfmpegProcess.Run(
                "ffprobe",
                ["-hide_banner", "-version"],
                TimeSpan.FromSeconds(8));
            return !result.TimedOut && result.ExitCode == 0;
        }
        catch (FfmpegNotFoundException)
        {
            return false;
        }
    }

    public static void RefreshSearchPath()
    {
        var machine = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "";
        var user = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        Environment.SetEnvironmentVariable("Path", string.Join(";", machine, user));
    }
}
