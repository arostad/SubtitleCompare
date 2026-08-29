using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.Tests;

public class BusyStatusTests
{
    [Fact]
    public void Format_names_a_single_extracting_pane()
    {
        Assert.Equal("Extracting A", BusyStatus.Format([new BusyPane(0, BusyKind.Extracting)]));
    }

    [Fact]
    public void Format_joins_same_kind_with_plus()
    {
        Assert.Equal("OCR A+B", BusyStatus.Format(
        [
            new BusyPane(0, BusyKind.Ocr),
            new BusyPane(1, BusyKind.Ocr),
        ]));
        Assert.Equal("OCR A+C", BusyStatus.Format(
        [
            new BusyPane(0, BusyKind.Ocr),
            new BusyPane(2, BusyKind.Ocr),
        ]));
        Assert.Equal("OCR B+C · 40%", BusyStatus.Format(
        [
            new BusyPane(1, BusyKind.Ocr, 20, 50),
            new BusyPane(2, BusyKind.Ocr, 20, 50),
        ]));
        Assert.Equal("OCR A+B+C · 67%", BusyStatus.Format(
        [
            new BusyPane(0, BusyKind.Ocr, 67, 100),
            new BusyPane(1, BusyKind.Ocr, 67, 100),
            new BusyPane(2, BusyKind.Ocr, 67, 100),
        ]));
    }

    [Fact]
    public void Format_keeps_letters_in_abc_order()
    {
        Assert.Equal("Extracting A+C", BusyStatus.Format(
        [
            new BusyPane(2, BusyKind.Extracting),
            new BusyPane(0, BusyKind.Extracting),
        ]));
    }

    [Fact]
    public void Format_mixes_extract_and_ocr()
    {
        Assert.Equal(
            "Extracting A · OCR C · 18%",
            BusyStatus.Format(
            [
                new BusyPane(0, BusyKind.Extracting),
                new BusyPane(2, BusyKind.Ocr, 18, 100),
            ]));
    }

    [Fact]
    public void Format_omits_percent_when_every_pane_is_indeterminate()
    {
        var text = BusyStatus.Format(
        [
            new BusyPane(0, BusyKind.Extracting),
            new BusyPane(1, BusyKind.Ocr),
        ]);
        Assert.Equal("Extracting A · OCR B", text);
        Assert.DoesNotContain("%", text);
    }

    [Fact]
    public void CombinedPercent_is_sum_current_over_sum_total()
    {
        var panes = new[]
        {
            new BusyPane(0, BusyKind.Ocr, 10, 100),
            new BusyPane(1, BusyKind.Ocr, 30, 100),
            new BusyPane(2, BusyKind.Extracting),
        };

        Assert.Equal(20, BusyStatus.CombinedPercent(panes));
        Assert.Equal(0.2, BusyStatus.CombinedFraction(panes));
    }

    [Fact]
    public void CombinedPercent_ignores_indeterminate_panes()
    {
        var panes = new[]
        {
            new BusyPane(0, BusyKind.Extracting),
            new BusyPane(1, BusyKind.Ocr, 40, 200),
        };

        Assert.Equal(20, BusyStatus.CombinedPercent(panes));
    }

    [Fact]
    public void CombinedPercent_is_null_when_no_determinate_total()
    {
        Assert.Null(BusyStatus.CombinedPercent(
        [
            new BusyPane(0, BusyKind.Extracting),
            new BusyPane(2, BusyKind.Ocr),
        ]));
        Assert.Null(BusyStatus.CombinedFraction(Array.Empty<BusyPane>()));
    }

    [Theory]
    [InlineData(0, 0, "Extracting…", BusyKind.Extracting)]
    [InlineData(0, 0, "Extracting image subtitles…", BusyKind.Extracting)]
    [InlineData(0, 0, "Parsing PGS…", BusyKind.Parsing)]
    [InlineData(0, 0, "Starting OCR…", BusyKind.Ocr)]
    [InlineData(12, 400, "OCR  12 of 400 (  3%)", BusyKind.Ocr)]
    [InlineData(0, 0, "Downloading English OCR data…", BusyKind.Ocr)]
    [InlineData(0, 0, "Working on something else", BusyKind.Processing)]
    public void Classify_uses_real_loader_messages(int current, int total, string message, BusyKind expected)
    {
        Assert.Equal(expected, BusyStatus.Classify(current, total, message));
    }
}
