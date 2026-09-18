using System.Runtime.InteropServices;

namespace ChromiumBrowser.Native;

/// <summary>
/// The few pieces of Windows that a window drawing its own title bar cannot do
/// without.
///
/// The approach here is the one that keeps everything Windows gives a normal
/// window — snapping, the shadow, the animations, resizing from any edge, the
/// rounded corners of Windows 11 — and removes only the caption Windows would
/// have drawn. That is a matter of answering two messages correctly rather than
/// of building a border out of panels, which is why so little is needed.
/// </summary>
internal static partial class Win32
{
    internal const int WM_NCCALCSIZE = 0x0083;
    internal const int WM_NCHITTEST = 0x0084;
    internal const int WM_NCLBUTTONDOWN = 0x00A1;
    internal const int WM_SETTINGCHANGE = 0x001A;
    internal const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
    internal const int WM_GETMINMAXINFO = 0x0024;
    internal const int WM_SYSCOMMAND = 0x0112;

    // Hit-test results. The resize edges are what make a borderless window still
    // feel like a window.
    internal const int HTCLIENT = 1;
    internal const int HTCAPTION = 2;
    internal const int HTLEFT = 10;
    internal const int HTRIGHT = 11;
    internal const int HTTOP = 12;
    internal const int HTTOPLEFT = 13;
    internal const int HTTOPRIGHT = 14;
    internal const int HTBOTTOM = 15;
    internal const int HTBOTTOMLEFT = 16;
    internal const int HTBOTTOMRIGHT = 17;

    internal const int SC_MOUSEMOVE = 0xF012;

    internal const int SM_CXSIZEFRAME = 32;
    internal const int SM_CYSIZEFRAME = 33;
    internal const int SM_CXPADDEDBORDER = 92;

    /// <summary>Ask for rounded corners; ignored before Windows 11, which is fine.</summary>
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWCP_ROUND = 2;

    /// <summary>Tell the shell the window is dark, so the resize border matches.</summary>
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NcCalcSizeParams
    {
        public Rect Proposed;
        public Rect Before;
        public Rect Client;
        public IntPtr WindowPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr SendMessageW(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReleaseCapture();

    /// <summary>
    /// Hands the drag over to Windows, as though the pointer had gone down on a
    /// title bar. Doing it this way rather than by moving the window from mouse
    /// events is what keeps snapping, the half-screen preview and the shake
    /// gesture working.
    /// </summary>
    internal static void BeginWindowDrag(IntPtr window)
    {
        ReleaseCapture();
        SendMessageW(window, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    [LibraryImport("user32.dll")]
    internal static partial IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    /// <summary>How thick the invisible resize border is, in this window's own pixels.</summary>
    internal static int ResizeBorder(float scale) =>
        GetSystemMetrics(SM_CXSIZEFRAME) + GetSystemMetrics(SM_CXPADDEDBORDER) + (int)(2 * scale);
}
