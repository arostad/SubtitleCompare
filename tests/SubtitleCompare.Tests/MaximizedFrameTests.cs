using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.Tests;

public class MaximizedFrameTests
{
    // Field case: 1920x1080 at 100 % to the right of a 225 % primary, 48 px taskbar at the bottom.
    private static readonly PixelRect Monitor = new(3840, 0, 5760, 1080);
    private static readonly PixelRect Work = new(3840, 0, 5760, 1032);

    [Fact]
    public void Maximized_client_is_clipped_to_the_work_area()
    {
        // Windows proposes the work area grown by a frame; the top frame must not stay on-screen.
        var proposed = new PixelRect(3832, -8, 5768, 1040);
        Assert.Equal(Work, MaximizedFrame.ClientRect(proposed, Monitor, Work));
    }

    [Fact]
    public void Mismatched_frame_still_lands_on_the_work_area()
    {
        // Mixed DPI: frame Windows adds (8 px) differs from what default calc removes (~2 px).
        var proposed = new PixelRect(3832, -8, 5768, 1040);
        var client = MaximizedFrame.ClientRect(proposed, Monitor, Work);
        Assert.NotNull(client);
        Assert.Equal(0, client!.Value.Top);
        Assert.Equal(1032, client.Value.Bottom);
    }

    [Fact]
    public void Auto_hide_taskbar_is_left_alone()
    {
        var proposed = new PixelRect(3832, -8, 5768, 1088);
        Assert.Null(MaximizedFrame.ClientRect(proposed, Monitor, Work, autoHideTaskbar: true));
        Assert.Null(MaximizedFrame.ClientRect(proposed, Monitor, Monitor));
    }

    [Fact]
    public void Empty_work_area_or_no_overlap_is_left_alone()
    {
        Assert.Null(MaximizedFrame.ClientRect(new PixelRect(0, 0, 100, 100), Monitor, new PixelRect(3840, 0, 3840, 1032)));
        Assert.Null(MaximizedFrame.ClientRect(new PixelRect(0, 0, 100, 100), Monitor, Work));
    }

    [Fact]
    public void Border_color_is_none_only_while_maximized()
    {
        Assert.Equal(0xFFFFFFFEu, MaximizedFrame.BorderColor(true));
        Assert.Equal(0xFFFFFFFFu, MaximizedFrame.BorderColor(false));
    }
}
