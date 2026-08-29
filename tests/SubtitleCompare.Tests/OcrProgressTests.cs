using SubtitleCompare.Core.Ocr;

namespace SubtitleCompare.Tests;

public class OcrProgressTests
{
    [Fact]
    public void Format_pads_current_and_percent_to_a_stable_width()
    {
        var a = OcrCueBuilder.Format(7, 412);
        var b = OcrCueBuilder.Format(107, 412);
        var c = OcrCueBuilder.Format(412, 412);

        Assert.Equal("OCR   7 of 412 (  2%)", a);
        Assert.Equal("OCR 107 of 412 ( 26%)", b);
        Assert.Equal("OCR 412 of 412 (100%)", c);
        Assert.Equal(a.Length, b.Length);
        Assert.Equal(a.Length, c.Length);
    }

    [Fact]
    public void Format_matches_the_status_bar_example()
    {
        Assert.Equal("OCR  12 of 400 (  3%)", OcrCueBuilder.Format(12, 400));
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
