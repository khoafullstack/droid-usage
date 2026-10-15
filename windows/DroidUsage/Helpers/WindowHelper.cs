using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace DroidUsage.Helpers;

public static class WindowHelper
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const nint WS_CAPTION = 0x00C00000;
    private const nint WS_THICKFRAME = 0x00040000;
    private const nint WS_SYSMENU = 0x00080000;
    private const nint WS_EX_TOOLWINDOW = 0x00000080;

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

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

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attr,
        ref int attrValue,
        int attrSize);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    public static IntPtr GetHwnd(Window window) => WindowNative.GetWindowHandle(window);

    public static AppWindow GetAppWindow(Window window)
    {
        var hwnd = GetHwnd(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    public static void ConfigureFloatingWidgetWindow(Window window)
    {
        var hwnd = GetHwnd(window);
        var appWindow = GetAppWindow(window);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            try
            {
                presenter.SetBorderAndTitleBar(true, false);
            }
            catch
            {
            }

            presenter.IsResizable = false;
        }

        var style = GetWindowLongPtr(hwnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU);
        SetWindowLongPtr(hwnd, GWL_STYLE, style);

        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);

        SetWindowPos(
            hwnd,
            HWND_TOPMOST,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        int cornerPreference = DWMWCP_ROUND;
        DwmSetWindowAttribute(
            hwnd,
            DWMWA_WINDOW_CORNER_PREFERENCE,
            ref cornerPreference,
            sizeof(int));
    }

    public static double GetDpiScale(Window window)
    {
        var hwnd = GetHwnd(window);
        var dpi = GetDpiForWindow(hwnd);
        return dpi > 0 ? dpi / 96.0 : 1.0;
    }

    public static PointInt32 ResizeAndPositionEdgeAwareDip(
        Window window,
        int newWidthDip,
        int newHeightDip)
    {
        var hwnd = GetHwnd(window);
        var appWindow = GetAppWindow(window);
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var scale = GetDpiScale(window);
        int newWidthPx = (int)Math.Round(newWidthDip * scale);
        int newHeightPx = (int)Math.Round(newHeightDip * scale);

        GetWindowRect(hwnd, out var currentRect);

        int currentCenterX = currentRect.Left + (currentRect.Width / 2);
        int workAreaMidX = workArea.X + (workArea.Width / 2);

        bool anchorRight = currentCenterX >= workAreaMidX ||
                           (currentRect.Left + newWidthPx > workArea.X + workArea.Width);

        int newX = anchorRight
            ? currentRect.Right - newWidthPx
            : currentRect.Left;

        int bottomThreshold = workArea.Y + workArea.Height - (int)Math.Round(80 * scale);
        bool anchorBottom = currentRect.Bottom >= bottomThreshold ||
                            (currentRect.Top + newHeightPx > workArea.Y + workArea.Height);

        int newY = anchorBottom
            ? currentRect.Bottom - newHeightPx
            : currentRect.Top;

        return SetPositionAndSizeClampedPx(hwnd, workArea, newX, newY, newWidthPx, newHeightPx);
    }

    public static PointInt32 MoveAndClampDip(
        Window window,
        int targetX,
        int targetY,
        int widthDip,
        int heightDip)
    {
        var hwnd = GetHwnd(window);
        var appWindow = GetAppWindow(window);
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var scale = GetDpiScale(window);
        int widthPx = (int)Math.Round(widthDip * scale);
        int heightPx = (int)Math.Round(heightDip * scale);

        return SetPositionAndSizeClampedPx(hwnd, workArea, targetX, targetY, widthPx, heightPx);
    }

    private static PointInt32 SetPositionAndSizeClampedPx(
        IntPtr hwnd,
        RectInt32 workArea,
        int targetX,
        int targetY,
        int widthPx,
        int heightPx)
    {
        int minX = workArea.X + 4;
        int maxX = Math.Max(minX, workArea.X + workArea.Width - widthPx - 4);
        int minY = workArea.Y + 4;
        int maxY = Math.Max(minY, workArea.Y + workArea.Height - heightPx - 4);

        int clampedX = Math.Clamp(targetX, minX, maxX);
        int clampedY = Math.Clamp(targetY, minY, maxY);

        SetWindowPos(
            hwnd,
            HWND_TOPMOST,
            clampedX,
            clampedY,
            widthPx,
            heightPx,
            SWP_NOACTIVATE);

        return new PointInt32(clampedX, clampedY);
    }

    public static PointInt32 GetDefaultTopRightPosition(Window window, int widthDip, int heightDip)
    {
        var appWindow = GetAppWindow(window);
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var scale = GetDpiScale(window);
        var widthPx = (int)Math.Round(widthDip * scale);
        var marginPx = (int)Math.Round(16 * scale);

        var x = workArea.X + workArea.Width - widthPx - marginPx;
        var y = workArea.Y + marginPx;
        return new PointInt32(Math.Max(workArea.X + 4, x), Math.Max(workArea.Y + 4, y));
    }
}
