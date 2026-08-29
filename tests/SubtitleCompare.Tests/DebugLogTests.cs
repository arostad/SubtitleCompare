using SubtitleCompare.Core.Diagnostics;

namespace SubtitleCompare.Tests;

public class DebugLogTests : IDisposable
{
    public DebugLogTests() => DebugLog.Clear();

    public void Dispose() => DebugLog.Clear();

    [Fact]
    public void Error_records_inner_exception_without_paths_or_media_titles()
    {
        var inner = new DllNotFoundException(
            @"Unable to load DLL 'tesseract50' from C:\Users\andy\Videos\Show.Name.mkv");
        DebugLog.Error("Tesseract OCR engine could not be loaded", inner);

        var line = Assert.Single(DebugLog.Snapshot());
        Assert.Contains("[error]", line);
        Assert.Contains("DllNotFoundException", line);
        Assert.Contains("Tesseract OCR engine could not be loaded", line);
        Assert.DoesNotContain("andy", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Show.Name", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".mkv", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", line);
    }

    [Fact]
    public void Error_walks_inner_exception_chain()
    {
        var inner = new FileNotFoundException("eng.traineddata was not found");
        var outer = new InvalidOperationException("Tesseract OCR engine could not be loaded.", inner);
        DebugLog.Error("create failed", outer);

        var line = Assert.Single(DebugLog.Snapshot());
        Assert.Contains("InvalidOperationException", line);
        Assert.Contains("FileNotFoundException", line);
        Assert.Contains("eng.traineddata", line);
    }

    [Fact]
    public void Info_is_anonymized_before_enqueue()
    {
        DebugLog.Info(@"extracted tesseract50.dll from C:\Users\andy\AppData\Local\Temp\foo");
        var line = Assert.Single(DebugLog.Snapshot());
        Assert.Contains("[info]", line);
        Assert.Contains("tesseract50.dll", line);
        Assert.DoesNotContain("andy", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%LOCALAPPDATA%", line);
    }

    [Fact]
    public void Ring_buffer_keeps_the_last_capacity_events()
    {
        for (var i = 0; i < DebugLog.Capacity + 7; i++)
            DebugLog.Info($"event-{i}");

        var snap = DebugLog.Snapshot();
        Assert.Equal(DebugLog.Capacity, snap.Count);
        Assert.Contains("event-7", snap[0]);
        Assert.Contains($"event-{DebugLog.Capacity + 6}", snap[^1]);
    }
}
