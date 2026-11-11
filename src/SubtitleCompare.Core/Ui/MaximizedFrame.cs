namespace SubtitleCompare.Core.Ui;

/// <summary>
/// DWM border color for the main window: none while maximized (Windows 11; ignored earlier), the
/// default otherwise.
///
/// 1.1.30 also handled <c>WM_NCCALCSIZE</c> to clip the client area to the work area (IXSmon 0.1.20's
/// approach). IXSmon draws its own title bar; SubtitleCompare uses standard chrome, so that made the
/// client area cover the caption and the title bar disappeared. 1.1.31 dropped it. With standard
/// chrome, never set the client rect from the window rect.
/// </summary>
public static class MaximizedFrame
{
    /// <summary><c>DWMWA_BORDER_COLOR</c> attribute id.</summary>
    public const int DwmwaBorderColor = 34;

    /// <summary><c>DWMWA_COLOR_NONE</c>: DWM draws no window border.</summary>
    public const uint BorderColorNone = 0xFFFFFFFE;

    /// <summary><c>DWMWA_COLOR_DEFAULT</c>: DWM draws its normal window border.</summary>
    public const uint BorderColorDefault = 0xFFFFFFFF;

    /// <summary>The <c>DWMWA_BORDER_COLOR</c> value for the window's state.</summary>
    public static uint BorderColor(bool maximized) => maximized ? BorderColorNone : BorderColorDefault;
}
