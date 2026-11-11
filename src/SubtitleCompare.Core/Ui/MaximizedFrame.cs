namespace SubtitleCompare.Core.Ui;

/// <summary>A window-system rectangle in physical pixels (left/top inclusive, right/bottom exclusive).</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}

/// <summary>
/// 1.1.30: no thin strip above the title bar when the main window is maximized on a monitor whose
/// scale differs from the primary's.
///
/// Windows makes a maximized window larger than its monitor's work area by the resize frame, so the
/// frame falls off-screen. With mixed per-monitor DPI (field case: primary at 225 %, ASUS MB16AC
/// 1920x1080 at 100 %) the frame Windows trims from the client area is not the frame it added, and a
/// ~6 px piece of the top resize border stays on-screen: light gray unfocused, accent color focused.
///
/// The App layer (<c>MaximizedFrameHook</c>) handles <c>WM_NCCALCSIZE</c>: while maximized, the client
/// area becomes the part of the proposed window rectangle inside the monitor's work area, so no frame
/// pixel is left on-screen. While maximized it also sets <c>DWMWA_BORDER_COLOR</c> to
/// <see cref="BorderColorNone"/> (Windows 11; ignored earlier) and restores the default otherwise.
/// Same approach as IXSmon 0.1.20.
/// </summary>
public static class MaximizedFrame
{
    /// <summary><c>DWMWA_BORDER_COLOR</c> attribute id.</summary>
    public const int DwmwaBorderColor = 34;

    /// <summary><c>DWMWA_COLOR_NONE</c>: DWM draws no window border.</summary>
    public const uint BorderColorNone = 0xFFFFFFFE;

    /// <summary><c>DWMWA_COLOR_DEFAULT</c>: DWM draws its normal window border.</summary>
    public const uint BorderColorDefault = 0xFFFFFFFF;

    /// <summary>
    /// The client rectangle a maximized window should get, or <c>null</c> to leave Windows' result
    /// alone. <paramref name="window"/> is the proposed window rectangle (<c>WM_NCCALCSIZE</c>
    /// <c>rgrc[0]</c> on entry); <paramref name="monitor"/> and <paramref name="work"/> are that
    /// monitor's <c>rcMonitor</c> / <c>rcWork</c>.
    ///
    /// Leaves it alone when the monitor has an auto-hide taskbar, or the work area is the whole
    /// monitor: a client area covering the full monitor reads as full-screen and an auto-hide taskbar
    /// would no longer pop up.
    /// </summary>
    public static PixelRect? ClientRect(PixelRect window, PixelRect monitor, PixelRect work, bool autoHideTaskbar = false)
    {
        if (autoHideTaskbar || work == monitor || work.Width <= 0 || work.Height <= 0)
            return null;

        var client = new PixelRect(
            Math.Max(window.Left, work.Left),
            Math.Max(window.Top, work.Top),
            Math.Min(window.Right, work.Right),
            Math.Min(window.Bottom, work.Bottom));
        return client.Width > 0 && client.Height > 0 ? client : null;
    }

    /// <summary>The <c>DWMWA_BORDER_COLOR</c> value for the window's state.</summary>
    public static uint BorderColor(bool maximized) => maximized ? BorderColorNone : BorderColorDefault;
}
