using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.Tests;

public class BusyStatusTests
{
    [Fact]
    public void Format_names_a_single_pane_and_its_step()
    {
        Assert.Equal(
            "A pulling the track out of the MKV",
            BusyStatus.Format([new BusyPane(0, BusyKind.Extracting, Step: LoadSteps.PullingTrack)]));
        Assert.Equal("A extracting", BusyStatus.Format([new BusyPane(0, BusyKind.Extracting)]));
    }

    [Fact]
    public void Format_joins_same_step_with_plus()
    {
        Assert.Equal("A+B OCR", BusyStatus.Format(
        [
            new BusyPane(0, BusyKind.Ocr),
            new BusyPane(1, BusyKind.Ocr),
        ]));
        Assert.Equal("A+C OCR", BusyStatus.Format(
        [
            new BusyPane(0, BusyKind.Ocr),
            new BusyPane(2, BusyKind.Ocr),
        ]));
        Assert.Equal("B+C OCR 40 / 100 · 40%", BusyStatus.Format(
        [
            new BusyPane(1, BusyKind.Ocr, 20, 50),
            new BusyPane(2, BusyKind.Ocr, 20, 50),
        ]));
        Assert.Equal("A+B+C OCR 201 / 300 · 67%", BusyStatus.Format(
        [
            new BusyPane(0, BusyKind.Ocr, 67, 100),
            new BusyPane(1, BusyKind.Ocr, 67, 100),
            new BusyPane(2, BusyKind.Ocr, 67, 100),
        ]));
    }

    [Fact]
    public void Format_keeps_letters_in_abc_order()
    {
        Assert.Equal(
            "A+C pulling the track out of the MKV",
            BusyStatus.Format(
            [
                new BusyPane(2, BusyKind.Extracting, Step: LoadSteps.PullingTrack),
                new BusyPane(0, BusyKind.Extracting, Step: LoadSteps.PullingTrack),
            ]));
    }

    [Fact]
    public void Format_mixes_extract_step_and_ocr_counts()
    {
        Assert.Equal(
            "A pulling the track out of the MKV · C OCR 18 / 100 · 18%",
            BusyStatus.Format(
            [
                new BusyPane(0, BusyKind.Extracting, Step: LoadSteps.PullingTrack),
                new BusyPane(2, BusyKind.Ocr, 18, 100),
            ]));
    }

    [Fact]
    public void Format_keeps_parse_and_download_steps()
    {
        Assert.Equal(
            "A reading the SRT · B parsing PGS · C downloading English OCR data",
            BusyStatus.Format(
            [
                new BusyPane(0, BusyKind.Parsing, Step: LoadSteps.ReadingSrt),
                new BusyPane(1, BusyKind.Parsing, Step: LoadSteps.ParsingPgs),
                new BusyPane(2, BusyKind.Ocr, Step: LoadSteps.DownloadingOcrData("English")),
            ]));
    }

    [Fact]
    public void Format_omits_percent_when_every_pane_is_indeterminate()
    {
        var text = BusyStatus.Format(
        [
            new BusyPane(0, BusyKind.Extracting, Step: LoadSteps.PullingTrack),
            new BusyPane(1, BusyKind.Ocr, Step: LoadSteps.StartingOcr),
        ]);
        Assert.Equal("A pulling the track out of the MKV · B starting OCR", text);
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
    [InlineData(0, 0, "pulling the track out of the MKV", BusyKind.Extracting)]
    [InlineData(0, 0, "Extracting…", BusyKind.Extracting)]
    [InlineData(0, 0, "Extracting image subtitles…", BusyKind.Extracting)]
    [InlineData(0, 0, "reading the SRT", BusyKind.Parsing)]
    [InlineData(0, 0, "parsing PGS", BusyKind.Parsing)]
    [InlineData(0, 0, "Parsing PGS…", BusyKind.Parsing)]
    [InlineData(0, 0, "starting OCR", BusyKind.Ocr)]
    [InlineData(0, 0, "Starting OCR…", BusyKind.Ocr)]
    [InlineData(12, 400, "OCR 12 / 400", BusyKind.Ocr)]
    [InlineData(0, 0, "downloading English OCR data", BusyKind.Ocr)]
    [InlineData(0, 0, "Working on something else", BusyKind.Processing)]
    public void Classify_uses_real_loader_messages(int current, int total, string message, BusyKind expected)
    {
        Assert.Equal(expected, BusyStatus.Classify(current, total, message));
    }
}
