using System.Collections.Concurrent;
using SubtitleCompare.Core.Ocr;

namespace SubtitleCompare.App.Ocr;

/// <summary>
/// One Tesseract engine is not thread-safe. A short pool lets OCR workers
/// run in parallel without sharing an instance.
/// </summary>
internal sealed class TesseractOcrPool : IDisposable
{
    private readonly ConcurrentBag<TesseractOcrEngine> _idle;
    private readonly TesseractOcrEngine[] _all;
    private bool _disposed;

    private TesseractOcrPool(TesseractOcrEngine[] engines)
    {
        _all = engines;
        _idle = new ConcurrentBag<TesseractOcrEngine>(engines);
    }

    public int Count => _all.Length;

    public static TesseractOcrPool Create(string dataPrefix, string language, int workers)
    {
        if (workers < 1)
            throw new ArgumentOutOfRangeException(nameof(workers));

        var engines = new TesseractOcrEngine[workers];
        var created = 0;
        try
        {
            for (var i = 0; i < workers; i++)
            {
                engines[i] = TesseractOcrEngine.Create(dataPrefix, language);
                created++;
            }

            return new TesseractOcrPool(engines);
        }
        catch
        {
            for (var i = 0; i < created; i++)
                engines[i].Dispose();
            throw;
        }
    }

    public string Recognize(BinaryImage image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        TesseractOcrEngine? engine = null;
        var spins = 0;
        while (!_idle.TryTake(out engine))
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TesseractOcrPool));
            if (++spins > 10_000)
                Thread.Sleep(1);
            else
                Thread.SpinWait(20);
        }

        try
        {
            return engine.Recognize(image);
        }
        finally
        {
            _idle.Add(engine);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var engine in _all)
            engine.Dispose();
    }
}
