using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using AntigravityQuota.Core;
using Point = System.Windows.Point;

namespace AntigravityQuota.App.Services;

internal static class Win32Interop
{
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOREDRAW = 0x0008;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TRANSPARENT = 0x00000020;

    public const int VK_MENU = 0x12;

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int MA_NOACTIVATE = 3;

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public const int DWMWCP_ROUND = 2;
    public const int DWMSBT_MAINWINDOW = 2;     // Mica
    public const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

    public static nint GetWindowLongPtr(nint hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    public static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, (int)dwNewLong);
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public static void ApplyHudWindowStyles(nint hwnd)
    {
        nint exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);
    }

    public static void SetClickThrough(nint hwnd, bool clickThrough)
    {
        if (hwnd == nint.Zero) return;
        nint exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        if (clickThrough)
        {
            exStyle |= WS_EX_TRANSPARENT;
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
        }
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);
    }

    public static void ApplyHudWindowAttributes(nint hwnd)
    {
        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        int corner = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int backdrop = DWMSBT_TRANSIENTWINDOW; // Acrylic / Mica
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }
}

public static class TaskbarPositioner
{
    private const double MarginDip = 12.0;

    /// <summary>
    /// Calculates the screen position (in DIPs) for the FlyoutWindow,
    /// aligned strictly above the taskbar in the vicinity of the system tray.
    /// Handles multi-monitor, multi-DPI, and taskbar on any screen edge.
    /// </summary>
    public static Point CalculatePosition(Window window, bool centerOverCursor = true)
    {
        // 1. Get physical cursor position to determine the target monitor
        Win32Interop.GetCursorPos(out var cursorPt);
        var hMonitor = Win32Interop.MonitorFromPoint(cursorPt, Win32Interop.MONITOR_DEFAULTTONEAREST);

        var mi = new Win32Interop.MONITORINFO
        {
            cbSize = (uint)Marshal.SizeOf<Win32Interop.MONITORINFO>()
        };
        Win32Interop.GetMonitorInfo(hMonitor, ref mi);

        // 2. Get DPI scale for this window
        var dpi = VisualTreeHelper.GetDpi(window);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        // 3. Measure desired window size if not yet rendered
        double width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        double height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        if (double.IsNaN(width) || width <= 0) width = 380;
        if (double.IsNaN(height) || height <= 0) height = 540;

        // 4. Delegate to pure TaskbarGeometry math
        var monitorRect = new ScreenRect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
        var workRect = new ScreenRect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right, mi.rcWork.Bottom);

        var pt = TaskbarGeometry.CalculateFlyoutPosition(
            monitorPhysical: monitorRect,
            workPhysical: workRect,
            windowWidthDip: width,
            windowHeightDip: height,
            dpiScaleX: dpiX,
            dpiScaleY: dpiY,
            marginDip: MarginDip,
            cursorPhysicalX: cursorPt.X,
            centerOverCursor: centerOverCursor
        );

        return new Point(pt.X, pt.Y);
    }
}
