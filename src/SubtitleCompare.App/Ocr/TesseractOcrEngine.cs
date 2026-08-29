using System.IO;
using SubtitleCompare.Core.Diagnostics;
using SubtitleCompare.Core.Ocr;
using Tesseract;

namespace SubtitleCompare.App.Ocr;

internal sealed class TesseractOcrEngine : IDisposable
{
    private readonly TesseractEngine _engine;

    private TesseractOcrEngine(TesseractEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// <c>TESSDATA_PREFIX</c> before we overwrite it for this process.
    /// Meaningful after <see cref="DidOverrideTessdataPrefix"/> is true; null means it was unset.
    /// </summary>
    internal static string? IncomingTessdataPrefix { get; private set; }

    internal static bool DidOverrideTessdataPrefix { get; private set; }

    /// <summary>Label of the datapath + <see cref="EngineMode"/> that succeeded, or null.</summary>
    internal static string? LastSuccessfulInit { get; private set; }

    public static TesseractOcrEngine Create(string dataPrefix, string language)
    {
        TesseractNative.EnsureAvailable();
        LastSuccessfulInit = null;
        try
        {
            OverrideTessdataPrefix(dataPrefix);
            var engine = CreateEngine(dataPrefix, language);
            engine.DefaultPageSegMode = PageSegMode.SingleBlock;
            engine.SetVariable("user_defined_dpi", "300");
            engine.SetVariable("tessedit_do_invert", "0");
            return new TesseractOcrEngine(engine);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            DebugLog.Error("Tesseract OCR engine could not be loaded", ex);
            throw new InvalidOperationException(
                "Tesseract OCR engine could not be loaded. Language data may be missing or the native libraries failed to start.",
                ex);
        }
    }

    /// <summary>
    /// Native tesseract (and older charlesw builds) can ignore the datapath argument
    /// when <c>TESSDATA_PREFIX</c> is set. Always pin it to our tessdata parent so a
    /// leftover system/install value cannot win.
    /// </summary>
    private static void OverrideTessdataPrefix(string dataPrefix)
    {
        var previous = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        IncomingTessdataPrefix = previous;
        DidOverrideTessdataPrefix = true;
        DebugLog.Info($"TESSDATA_PREFIX incoming: {Anon.EnvVar(previous)}");
        Environment.SetEnvironmentVariable("TESSDATA_PREFIX", dataPrefix);
        DebugLog.Info($"TESSDATA_PREFIX process: {Anon.EnvVar(dataPrefix)}");
    }

    private static TesseractEngine CreateEngine(string dataPrefix, string language)
    {
        var tessdataDirectory = Path.Combine(dataPrefix, "tessdata");
        var attempts = new (string Datapath, EngineMode Mode, string Label)[]
        {
            (dataPrefix, EngineMode.Default, "DataPrefix + Default"),
            (dataPrefix, EngineMode.LstmOnly, "DataPrefix + LstmOnly"),
            (tessdataDirectory, EngineMode.Default, "TessdataDirectory + Default"),
        };

        Exception? last = null;
        foreach (var attempt in attempts)
        {
            try
            {
                DebugLog.Info($"TesseractEngine try {attempt.Label} datapath={attempt.Datapath} lang={language}");
                var engine = new TesseractEngine(attempt.Datapath, language, attempt.Mode);
                LastSuccessfulInit = attempt.Label;
                DebugLog.Info($"TesseractEngine succeeded: {attempt.Label}");
                return engine;
            }
            catch (Exception ex)
            {
                last = ex;
                DebugLog.Error($"TesseractEngine failed: {attempt.Label}", ex);
            }
        }

        throw last ?? new InvalidOperationException("TesseractEngine create attempts exhausted.");
    }

    public string Recognize(BinaryImage image)
    {
        if (image.IsEmpty)
            return "";

        var bmp = image.ToBmp();
        using var pix = Pix.LoadFromMemory(bmp);
        using var page = _engine.Process(pix, PageSegMode.SingleBlock);
        return page.GetText() ?? "";
    }

    public void Dispose() => _engine.Dispose();
}
