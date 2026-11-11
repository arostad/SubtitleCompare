using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SubtitleCompare.Core.Diagnostics;
using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.App;

/// <summary>
/// While maximized, tells DWM to draw no window border (<see cref="MaximizedFrame"/>); the default
/// border comes back when the window is restored. Does not touch the dark-mode caption attributes
/// set by <see cref="Theme"/>.
///
/// 1.1.31: no <c>WM_NCCALCSIZE</c> handling. MainWindow uses standard Windows chrome, so the title
/// bar is non-client; 1.1.30 set the client rect from the window rect and the caption vanished.
/// </summary>
internal static class MaximizedFrameHook
{
    private const int WmSize = 0x0005;
    private const int SizeRestored = 0;
    private const int SizeMaximized = 2;

    public static void Attach(Window window)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource src)
            return;

        src.AddHook(Hook);
        SetBorderHidden(src.Handle, window.WindowState == WindowState.Maximized);
    }

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSize)
        {
            var state = (int)(wParam.ToInt64() & 0xFFFF);
            if (state is SizeMaximized or SizeRestored)
                SetBorderHidden(hwnd, state == SizeMaximized);
        }

        return IntPtr.Zero;
    }

    private static void SetBorderHidden(IntPtr hwnd, bool hidden)
    {
        try
        {
            var color = MaximizedFrame.BorderColor(hidden);
            _ = DwmSetWindowAttribute(hwnd, MaximizedFrame.DwmwaBorderColor, ref color, sizeof(uint));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            DebugLog.Error("maximized frame border", ex);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);
}
