using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SubtitleCompare.Core.Diagnostics;
using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.App;

/// <summary>
/// 1.1.30: removes the ~6 px resize-border strip left above the title bar when a window is maximized
/// on a monitor whose DPI differs from the primary's (see <see cref="MaximizedFrame"/>).
///
/// While maximized: <c>WM_NCCALCSIZE</c> sets the client rect to the proposed window rect clipped to
/// the current monitor's work area (skipped when that monitor has an auto-hide taskbar), and DWM is
/// told to draw no border. Otherwise Windows' default frame and border are untouched. WPF's own DPI
/// handling runs as usual; after <c>WM_DPICHANGED</c> or a work-area/display change the frame is
/// recalculated. Does not touch the dark-mode caption attributes set by <see cref="Theme"/>.
/// </summary>
internal static class MaximizedFrameHook
{
    private const int WmSize = 0x0005;
    private const int WmSettingChange = 0x001A;
    private const int WmDisplayChange = 0x007E;
    private const int WmNcCalcSize = 0x0083;
    private const int WmDpiChanged = 0x02E0;

    private const int SizeRestored = 0;
    private const int SizeMaximized = 2;

    private const int GwlStyle = -16;
    private const uint WsMaximize = 0x01000000;
    private const uint WsMinimize = 0x20000000;

    private const uint MonitorDefaultToNearest = 2;

    private const uint AbmGetAutoHideBarEx = 0x0000000B;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint SwpRefreshFrame = SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpNoOwnerZOrder;

    public static void Attach(Window window)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource src)
            return;

        src.AddHook(Hook);
        var hwnd = src.Handle;
        SetBorderHidden(hwnd, IsMaximized(hwnd));
        if (IsMaximized(hwnd))
            RefreshFrame(hwnd);
    }

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        try
        {
            switch (msg)
            {
                case WmNcCalcSize when wParam != IntPtr.Zero && IsMaximized(hwnd):
                {
                    // rgrc[0] is the proposed window rect on entry and the client rect on return.
                    var proposed = Marshal.PtrToStructure<Rect>(lParam);
                    var result = DefWindowProc(hwnd, msg, wParam, lParam);
                    ApplyMaximizedClient(proposed, lParam);
                    handled = true;
                    return result;
                }
                case WmSize:
                {
                    var state = (int)(wParam.ToInt64() & 0xFFFF);
                    if (state is SizeMaximized or SizeRestored)
                        SetBorderHidden(hwnd, state == SizeMaximized);
                    break;
                }
                case WmDpiChanged:
                case WmDisplayChange:
                case WmSettingChange:
                    // Let WPF (and Windows) finish first, then recompute the frame for the monitor we are on.
                    if (IsMaximized(hwnd))
                        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                        {
                            if (IsMaximized(hwnd))
                                RefreshFrame(hwnd);
                        });
                    break;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            DebugLog.Error("maximized frame", ex);
        }

        return IntPtr.Zero;
    }

    private static void ApplyMaximizedClient(Rect proposed, IntPtr lParam)
    {
        var mon = MonitorFromRect(ref proposed, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (mon == IntPtr.Zero || !GetMonitorInfo(mon, ref info))
            return;

        var client = MaximizedFrame.ClientRect(
            ToPixel(proposed), ToPixel(info.Monitor), ToPixel(info.Work), HasAutoHideTaskbar(info.Monitor));
        if (client is not { } c)
            return;

        Marshal.StructureToPtr(new Rect { Left = c.Left, Top = c.Top, Right = c.Right, Bottom = c.Bottom }, lParam, fDeleteOld: false);
    }

    private static bool HasAutoHideTaskbar(Rect monitor)
    {
        for (uint edge = 0; edge < 4; edge++)
        {
            var data = new AppBarData
            {
                Size = (uint)Marshal.SizeOf<AppBarData>(),
                Edge = edge,
                Rc = monitor,
            };
            if (SHAppBarMessage(AbmGetAutoHideBarEx, ref data) != IntPtr.Zero)
                return true;
        }

        return false;
    }

    private static bool IsMaximized(IntPtr hwnd)
    {
        var style = (uint)GetWindowLong(hwnd, GwlStyle);
        return (style & WsMaximize) != 0 && (style & WsMinimize) == 0;
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

    private static void RefreshFrame(IntPtr hwnd) =>
        _ = SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpRefreshFrame);

    private static PixelRect ToPixel(Rect r) => new(r.Left, r.Top, r.Right, r.Bottom);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Size;
        public IntPtr Hwnd;
        public uint CallbackMessage;
        public uint Edge;
        public Rect Rc;
        public IntPtr LParam;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref Rect rect, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint message, ref AppBarData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);
}
