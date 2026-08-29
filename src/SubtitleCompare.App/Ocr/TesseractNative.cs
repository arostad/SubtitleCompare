using System.IO;
using Tesseract;

namespace SubtitleCompare.App.Ocr;

/// <summary>
/// Drops win-x64 Tesseract/Leptonica DLLs into LocalAppData so charlesw/tesseract
/// can find them after a PublishSingleFile extract.
/// </summary>
internal static class TesseractNative
{
    internal const string TesseractDll = "tesseract50.dll";
    internal const string LeptonicaDll = "leptonica-1.82.0.dll";

    public static string NativeRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SubtitleCompare",
        "tesseract-native");

    public static void EnsureAvailable()
    {
        var x64 = Path.Combine(NativeRoot, "x64");
        Directory.CreateDirectory(x64);

        try
        {
            ExtractIfMissing(x64, TesseractDll);
            ExtractIfMissing(x64, LeptonicaDll);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Tesseract OCR engine could not be loaded. The native OCR libraries are missing or could not be extracted.",
                ex);
        }

        TesseractEnviornment.CustomSearchPath = NativeRoot;

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (!path.Contains(x64, StringComparison.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("PATH", x64 + Path.PathSeparator + path);
    }

    private static void ExtractIfMissing(string dir, string name)
    {
        var dest = Path.Combine(dir, name);
        if (File.Exists(dest) && new FileInfo(dest).Length > 10_000)
            return;

        using var stream = OpenLibrary(name)
            ?? throw new FileNotFoundException($"Embedded OCR library '{name}' was not found in this build.");

        var tmp = dest + ".tmp";
        using (var output = File.Create(tmp))
            stream.CopyTo(output);
        File.Move(tmp, dest, overwrite: true);
    }

    private static Stream? OpenLibrary(string name)
    {
        var asm = typeof(TesseractNative).Assembly;
        var embedded = asm.GetManifestResourceStream(name);
        if (embedded is not null)
            return embedded;

        foreach (var resource in asm.GetManifestResourceNames())
        {
            if (resource.EndsWith(name, StringComparison.OrdinalIgnoreCase))
                return asm.GetManifestResourceStream(resource);
        }

        var beside = Path.Combine(AppContext.BaseDirectory, "x64", name);
        if (File.Exists(beside))
            return File.OpenRead(beside);

        var nugetHint = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(nugetHint) ? File.OpenRead(nugetHint) : null;
    }
}
