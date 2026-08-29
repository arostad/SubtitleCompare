using System.Collections.Concurrent;
using System.Diagnostics;
using SubtitleCompare.Core.Ocr;
using SubtitleCompare.Core.Pgs;

namespace SubtitleCompare.Tests;

public class OcrCueBuilderTests
{
    [Fact]
    public void WorkerCount_stays_serial_on_short_tracks()
    {
        Assert.Equal(1, OcrCueBuilder.WorkerCount(1, 8));
        Assert.Equal(1, OcrCueBuilder.WorkerCount(7, 8));
        Assert.Equal(1, OcrCueBuilder.WorkerCount(80, 1));
        Assert.Equal(1, OcrCueBuilder.WorkerCount(0, 8));
    }

    [Fact]
    public void WorkerCount_caps_at_four_once_there_are_enough_cues()
    {
        Assert.Equal(4, OcrCueBuilder.WorkerCount(8, 8));
        Assert.Equal(4, OcrCueBuilder.WorkerCount(800, 16));
        Assert.Equal(2, OcrCueBuilder.WorkerCount(800, 2));
    }

    [Fact]
    public void Parallel_build_keeps_order_and_matches_serial()
    {
        var presentations = FakeCues(24);
        string Recognize(BinaryImage _) =>
            throw new InvalidOperationException("empty images skip recognize");

        var serial = OcrCueBuilder.Build(presentations, Recognize, maxDegreeOfParallelism: 1);
        var parallel = OcrCueBuilder.Build(presentations, Recognize, maxDegreeOfParallelism: 4);

        Assert.Equal(24, serial.Cues.Count);
        Assert.Equal(serial.Cues.Select(c => (c.Index, c.Start, c.End, c.Text)),
            parallel.Cues.Select(c => (c.Index, c.Start, c.End, c.Text)));
    }

    [Fact]
    public void Parallel_build_runs_recognize_on_more_than_one_thread()
    {
        var presentations = OpaqueCues(16);
        var threads = new ConcurrentDictionary<int, byte>();
        string Recognize(BinaryImage _)
        {
            threads.TryAdd(Environment.CurrentManagedThreadId, 0);
            Thread.Sleep(20);
            return "  hello   there ";
        }

        var parsed = OcrCueBuilder.Build(presentations, Recognize, maxDegreeOfParallelism: 4);
        Assert.Equal(16, parsed.Cues.Count);
        Assert.All(parsed.Cues, c => Assert.Equal("hello there", c.Text));
        Assert.Equal(Enumerable.Range(1, 16), parsed.Cues.Select(c => c.Index));
        Assert.True(threads.Count >= 2, $"expected parallel workers, saw {threads.Count} thread(s)");
    }

    [Fact]
    public void Parallel_ocr_finishes_sooner_than_serial_on_enough_cues()
    {
        var presentations = OpaqueCues(24);
        static string Recognize(BinaryImage _)
        {
            Thread.Sleep(15);
            return "ok";
        }

        var serialWatch = Stopwatch.StartNew();
        var serial = OcrCueBuilder.Build(presentations, Recognize, maxDegreeOfParallelism: 1);
        serialWatch.Stop();

        var parallelWatch = Stopwatch.StartNew();
        var parallel = OcrCueBuilder.Build(presentations, Recognize, maxDegreeOfParallelism: 4);
        parallelWatch.Stop();

        Assert.Equal(serial.Cues.Select(c => c.Text), parallel.Cues.Select(c => c.Text));
        Assert.True(
            parallelWatch.ElapsedMilliseconds < serialWatch.ElapsedMilliseconds * 0.7,
            $"parallel {parallelWatch.ElapsedMilliseconds}ms was not faster than serial {serialWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Progress_reaches_the_last_cue()
    {
        var last = default(OcrProgress);
        var sink = new InlineProgress<OcrProgress>(p => last = p);
        OcrCueBuilder.Build(OpaqueCues(5), _ => "x", sink, maxDegreeOfParallelism: 2);
        Assert.Equal(5, last.Current);
        Assert.Equal(5, last.Total);
        Assert.Equal("OCR 5 / 5", last.Message);
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _onNext;
        public InlineProgress(Action<T> onNext) => _onNext = onNext;
        public void Report(T value) => _onNext(value);
    }

    private static PgsPresentation[] FakeCues(int count)
    {
        var list = new PgsPresentation[count];
        for (var i = 0; i < count; i++)
        {
            list[i] = new PgsPresentation
            {
                Start = TimeSpan.FromSeconds(i),
                End = TimeSpan.FromSeconds(i + 1),
                Bitmap = new SubtitleBitmap(0, 0, []),
            };
        }

        return list;
    }

    private static PgsPresentation[] OpaqueCues(int count)
    {
        var list = new PgsPresentation[count];
        for (var i = 0; i < count; i++)
        {
            var rgba = new byte[4];
            rgba[0] = rgba[1] = rgba[2] = rgba[3] = 255;
            list[i] = new PgsPresentation
            {
                Start = TimeSpan.FromSeconds(i),
                End = TimeSpan.FromSeconds(i + 1),
                Bitmap = new SubtitleBitmap(1, 1, rgba),
            };
        }

        return list;
    }
}
