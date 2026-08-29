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

    public static TesseractOcrEngine Create(string dataPrefix, string language)
    {
        TesseractNative.EnsureAvailable();
        try
        {
            var engine = new TesseractEngine(dataPrefix, language, EngineMode.LstmOnly);
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
