using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace SubtitleCompare.Core.Ffmpeg;

internal sealed class FfmpegProcessResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public bool TimedOut { get; init; }
}

internal static class FfmpegProcess
{
    public static FfmpegProcessResult Run(
        string toolName,
        IEnumerable<string> arguments,
        TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = toolName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        Process process;
        try
        {
            process = Process.Start(psi)
                      ?? throw new InvalidOperationException($"Failed to start {toolName}.");
        }
        catch (Win32Exception ex) when (IsNotFound(ex))
        {
            throw new FfmpegNotFoundException(toolName, ex);
        }
        catch (System.IO.FileNotFoundException ex)
        {
            throw new FfmpegNotFoundException(toolName, ex);
        }

        using (process)
        {
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) stderr.AppendLine(e.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                process.WaitForExit(5000);
                return new FfmpegProcessResult
                {
                    ExitCode = -1,
                    StandardOutput = stdout.ToString(),
                    StandardError = stderr.ToString(),
                    TimedOut = true,
                };
            }

            // Flush async handlers after exit.
            process.WaitForExit();

            return new FfmpegProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString(),
            };
        }
    }

    private static bool IsNotFound(Win32Exception ex) =>
        ex.NativeErrorCode is 2 or 3 /* file / path not found */
        || (ex.Message.Contains("cannot find", StringComparison.OrdinalIgnoreCase)
            && ex.Message.Contains("specified file", StringComparison.OrdinalIgnoreCase));
}
