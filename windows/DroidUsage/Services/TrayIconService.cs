using System.Runtime.InteropServices;
using DroidUsage.Helpers;
using Microsoft.UI.Xaml;

namespace DroidUsage.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint WM_APP_TRAYICON = 0x8000 + 42;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_CONTEXTMENU = 0x007B;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_NONOTIFY = 0x0080;
    private const uint TPM_RIGHTBUTTON = 0x0002;

    private const int IDI_APPLICATION = 32512;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const uint LR_DEFAULTSIZE = 0x00000040;

    private const int CMD_TOGGLE_VISIBLE = 1001;
    private const int CMD_TOGGLE_EXPAND = 1002;
    private const int CMD_REFRESH = 1003;
    private const int CMD_RESET_POSITION = 1004;
    private const int CMD_EXIT = 1005;

    private delegate nint SUBCLASSPROC(
        IntPtr hWnd,
        uint uMsg,
        nint wParam,
        nint lParam,
        nuint uIdSubclass,
        nuint dwRefData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", EntryPoint = "LoadIconW")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(
        IntPtr hInst,
        string lpszName,
        uint uType,
        int cxDesired,
        int cyDesired,
        uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        int nReserved,
        IntPtr hWnd,
        IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        nuint uIdSubclass,
        nuint dwRefData);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        nint wParam,
        nint lParam);

    private readonly IntPtr _hwnd;
    private readonly SUBCLASSPROC _subclassProc;
    private IntPtr _customIconHandle;
    private NOTIFYICONDATA _nid;
    private bool _isAdded;
    private bool _disposed;

    public event Action? ToggleVisibilityRequested;
    public event Action? ToggleExpandRequested;
    public event Action? RefreshRequested;
    public event Action? ResetPositionRequested;
    public event Action? ExitRequested;

    public TrayIconService(Window window)
    {
        _hwnd = WindowHelper.GetHwnd(window);
        _subclassProc = WindowSubclassProc;
        SetWindowSubclass(_hwnd, _subclassProc, 1, 0);

        var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "factory.ico");
        if (File.Exists(icoPath))
        {
            _customIconHandle = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
        }

        var hIcon = _customIconHandle != IntPtr.Zero
            ? _customIconHandle
            : LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);

        _nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_APP_TRAYICON,
            hIcon = hIcon,
            szTip = "Factory Droid Usage",
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref _nid);
    }

    public void UpdateTooltip(string tooltip)
    {
        if (!_isAdded || _disposed)
        {
            return;
        }

        _nid.szTip = tooltip.Length > 120 ? tooltip[..120] : tooltip;
        _nid.uFlags = NIF_TIP;
        Shell_NotifyIcon(NIM_MODIFY, ref _nid);
    }

    private nint WindowSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        nint wParam,
        nint lParam,
        nuint uIdSubclass,
        nuint dwRefData)
    {
        if (uMsg == WM_APP_TRAYICON)
        {
            uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
            if (mouseMsg == WM_LBUTTONUP || mouseMsg == WM_LBUTTONDBLCLK)
            {
                ToggleVisibilityRequested?.Invoke();
                return 0;
            }

            if (mouseMsg == WM_RBUTTONUP || mouseMsg == WM_CONTEXTMENU)
            {
                ShowContextMenu();
                return 0;
            }
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        WindowHelper.GetCursorPos(out var pt);
        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AppendMenu(hMenu, MF_STRING, CMD_TOGGLE_VISIBLE, "Show / Hide Widget");
            AppendMenu(hMenu, MF_STRING, CMD_TOGGLE_EXPAND, "Expand / Collapse");
            AppendMenu(hMenu, MF_SEPARATOR, 0, null);
            AppendMenu(hMenu, MF_STRING, CMD_REFRESH, "Refresh Now");
            AppendMenu(hMenu, MF_STRING, CMD_RESET_POSITION, "Reset Position");
            AppendMenu(hMenu, MF_SEPARATOR, 0, null);
            AppendMenu(hMenu, MF_STRING, CMD_EXIT, "Exit");

            WindowHelper.SetForegroundWindow(_hwnd);
            int cmd = TrackPopupMenu(
                hMenu,
                TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON,
                pt.X,
                pt.Y,
                0,
                _hwnd,
                IntPtr.Zero);

            switch (cmd)
            {
                case CMD_TOGGLE_VISIBLE:
                    ToggleVisibilityRequested?.Invoke();
                    break;
                case CMD_TOGGLE_EXPAND:
                    ToggleExpandRequested?.Invoke();
                    break;
                case CMD_REFRESH:
                    RefreshRequested?.Invoke();
                    break;
                case CMD_RESET_POSITION:
                    ResetPositionRequested?.Invoke();
                    break;
                case CMD_EXIT:
                    ExitRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_isAdded)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            _isAdded = false;
        }

        if (_customIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_customIconHandle);
            _customIconHandle = IntPtr.Zero;
        }

        RemoveWindowSubclass(_hwnd, _subclassProc, 1);
    }
}
