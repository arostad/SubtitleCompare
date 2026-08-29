using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SubtitleCompare.App.Ocr;
using SubtitleCompare.Core.Diagnostics;
using SubtitleCompare.Core.Ffmpeg;

namespace SubtitleCompare.App.Diagnostics;

internal static class DebugReport
{
    public const string PrivacyNote =
        "Saves a text file with app version, OS, and OCR/FFmpeg status. Usernames, machine name, file paths, media titles, and subtitle text are stripped.";

    public static string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine(PrivacyNote);
        sb.AppendLine();
        AppendApp(sb);
        AppendFfmpeg(sb);
        AppendVcRedist(sb);
        AppendTesseractNatives(sb);
        AppendTessdata(sb);
        AppendRecent(sb);
        return sb.ToString();
    }

    private static void AppendApp(StringBuilder sb)
    {
        sb.AppendLine("[app]");
        sb.AppendLine($"version: {AppVersion.Current}");
        sb.AppendLine($"64-bit process: {(Environment.Is64BitProcess ? "yes" : "no")}");
        sb.AppendLine($"os: {Anon.Text(RuntimeInformation.OSDescription)}");
        sb.AppendLine($"framework: {Anon.Text(RuntimeInformation.FrameworkDescription)}");
        sb.AppendLine();
    }

    private static void AppendFfmpeg(StringBuilder sb)
    {
        sb.AppendLine("[ffmpeg]");
        var available = false;
        try
        {
            available = FfmpegLocator.IsAvailable();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"available: no ({ex.GetType().Name})");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"available: {(available ? "yes" : "no")}");
        sb.AppendLine();
    }

    private static void AppendVcRedist(StringBuilder sb)
    {
        sb.AppendLine("[vcredist]");
        sb.AppendLine($"vcruntime140.dll: {(File.Exists(@"C:\Windows\System32\vcruntime140.dll") ? "yes" : "no")}");
        sb.AppendLine($"msvcp140.dll: {(File.Exists(@"C:\Windows\System32\msvcp140.dll") ? "yes" : "no")}");
        sb.AppendLine();
    }

    private static void AppendTesseractNatives(StringBuilder sb)
    {
        sb.AppendLine("[tesseract-natives]");
        ListFiles(sb, TesseractNative.NativeRoot, "*", "NativeRoot");
        sb.AppendLine($"embedded {TesseractNative.TesseractDll}: {(HasEmbedded(TesseractNative.TesseractDll) ? "yes" : "no")}");
        sb.AppendLine($"embedded {TesseractNative.LeptonicaDll}: {(HasEmbedded(TesseractNative.LeptonicaDll) ? "yes" : "no")}");
        sb.AppendLine();
    }

    private static void AppendTessdata(StringBuilder sb)
    {
        sb.AppendLine("[tessdata]");
        ListFiles(sb, TessdataStore.TessdataDirectory, "*.traineddata", "TessdataDirectory");
        var incoming = TesseractOcrEngine.DidOverrideTessdataPrefix
            ? TesseractOcrEngine.IncomingTessdataPrefix
            : Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        sb.AppendLine($"TESSDATA_PREFIX: {Anon.EnvVar(incoming)}");
        if (TesseractOcrEngine.DidOverrideTessdataPrefix)
            sb.AppendLine($"TESSDATA_PREFIX process: {Anon.EnvVar(Environment.GetEnvironmentVariable("TESSDATA_PREFIX"))}");
        sb.AppendLine($"engine init: {TesseractOcrEngine.LastSuccessfulInit ?? "not attempted"}");
        sb.AppendLine();
    }

    private static void AppendRecent(StringBuilder sb)
    {
        sb.AppendLine("[recent]");
        var events = DebugLog.Snapshot();
        if (events.Count == 0)
            sb.AppendLine("(none)");
        else
        {
            foreach (var line in events)
                sb.AppendLine(line);
        }
    }

    private static void ListFiles(StringBuilder sb, string directory, string pattern, string label)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                sb.AppendLine($"{label} exists: no");
                return;
            }

            sb.AppendLine($"{label} exists: yes");
            var index = 0;
            foreach (var path in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
            {
                index++;
                var size = new FileInfo(path).Length;
                sb.AppendLine($"file {index}: {size} bytes");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"{label} list failed: {ex.GetType().Name}");
        }
    }

    private static bool HasEmbedded(string fileName)
    {
        var names = typeof(TesseractNative).Assembly.GetManifestResourceNames();
        return names.Any(n =>
            n.Equals(fileName, StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase)
            || n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
    }
}
