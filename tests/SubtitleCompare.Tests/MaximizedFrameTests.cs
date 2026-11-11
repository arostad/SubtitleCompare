using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.Tests;

public class MaximizedFrameTests
{
    [Fact]
    public void Border_color_is_none_only_while_maximized()
    {
        Assert.Equal(0xFFFFFFFEu, MaximizedFrame.BorderColor(true));
        Assert.Equal(0xFFFFFFFFu, MaximizedFrame.BorderColor(false));
    }

    [Fact]
    public void No_client_rect_override_with_standard_chrome()
    {
        // 1.1.30 regression: overriding WM_NCCALCSIZE's client rect hid the standard title bar.
        Assert.Null(typeof(MaximizedFrame).GetMethod("ClientRect"));
        Assert.Null(typeof(MaximizedFrame).Assembly.GetType("SubtitleCompare.Core.Ui.PixelRect"));
    }
}
