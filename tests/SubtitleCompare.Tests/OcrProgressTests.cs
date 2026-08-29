using SubtitleCompare.Core.Ocr;

namespace SubtitleCompare.Tests;

public class OcrProgressTests
{
    [Fact]
    public void Format_is_current_over_total()
    {
        Assert.Equal("OCR 7 / 412", OcrCueBuilder.Format(7, 412));
        Assert.Equal("OCR 107 / 412", OcrCueBuilder.Format(107, 412));
        Assert.Equal("OCR 412 / 412", OcrCueBuilder.Format(412, 412));
    }

    [Fact]
    public void Format_matches_the_status_bar_example()
    {
        Assert.Equal("OCR 12 / 400", OcrCueBuilder.Format(12, 400));
        Assert.Equal("OCR 120 / 800", OcrCueBuilder.Format(120, 800));
        Assert.Equal("OCR…", OcrCueBuilder.Format(0, 0));
    }

    [Fact]
    public void Throttle_emits_first_and_flushes_latest()
    {
        var sink = new Sink<int>();
        var t = new ThrottledProgress<int>(sink, TimeSpan.FromHours(1));

        t.Report(1);
        t.Report(2);
        t.Report(3);
        Assert.Equal(new[] { 1 }, sink.Items);

        t.Flush();
        Assert.Equal(new[] { 1, 3 }, sink.Items);
    }

    [Fact]
    public void Throttle_immediate_reports_bypass_the_interval()
    {
        var sink = new Sink<int>();
        var t = new ThrottledProgress<int>(sink, TimeSpan.FromHours(1), n => n < 0);

        t.Report(1);
        t.Report(-2);
        t.Report(3);
        t.Flush();

        Assert.Equal(new[] { 1, -2, 3 }, sink.Items);
    }

    [Fact]
    public void Throttle_flush_is_a_no_op_when_nothing_is_pending()
    {
        var sink = new Sink<int>();
        var t = new ThrottledProgress<int>(sink, TimeSpan.Zero);
        t.Report(1);
        t.Flush();
        Assert.Equal(new[] { 1 }, sink.Items);
    }

    private sealed class Sink<T> : IProgress<T>
    {
        public List<T> Items { get; } = new();
        public void Report(T value) => Items.Add(value);
    }
}
