namespace SubtitleCompare.Core.Ffmpeg;

public sealed class FfmpegNotFoundException : Exception
{
    public string ToolName { get; }

    public FfmpegNotFoundException(string toolName)
        : base(BuildMessage(toolName))
    {
        ToolName = toolName;
    }

    public FfmpegNotFoundException(string toolName, Exception inner)
        : base(BuildMessage(toolName), inner)
    {
        ToolName = toolName;
    }

    private static string BuildMessage(string toolName) =>
        $"{toolName} was not found on PATH. Subtitle Compare needs FFmpeg to read MKV subtitle tracks. " +
        "Install it with `winget install Gyan.FFmpeg` (or `winget install ffmpeg`), " +
        "or download a build from https://www.gyan.dev/ffmpeg/builds/ and add the bin folder to PATH, then restart the app.";
}
