using System.Runtime.InteropServices;
using ChromiumBrowser.Native;

namespace ChromiumBrowser.Ui;

/// <summary>
/// Closes an open menu when the page is clicked.
///
/// A drop-down closes itself on a click anywhere else by watching the mouse on
/// its own thread — and the page is not on that thread. The engine runs its
/// windows on a thread of its own, so a click into the page never reaches the
/// menu, and it stayed open until something in the window's own chrome was
/// clicked. While a menu is open, a low-level mouse hook looks at each press
/// instead: one that lands on a window of this process but of another thread
/// is a click into a page, and the menu is closed. The click itself still goes
/// through, as it would with any other menu.
/// </summary>
internal static class PageClicks
{
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;

    /// <summary>The menu that is open, and the hook watching for it. One at a time, as Windows Forms allows.</summary>
    private static ToolStripDropDown? s_open;
    private static IntPtr s_hook;

    /// <summary>Makes the menu close when a page is clicked while it is open.</summary>
    public static T ClosesOnPageClicks<T>(this T menu) where T : ToolStripDropDown
    {
        menu.Opened += (_, _) => Watch(menu);
        menu.Closed += (_, _) => Stop(menu);
        menu.Disposed += (_, _) => Stop(menu);
        return menu;
    }

    private static unsafe void Watch(ToolStripDropDown menu)
    {
        s_open = menu;
        if (s_hook == IntPtr.Zero)
        {
            s_hook = Win32.SetWindowsHookExW(Win32.WH_MOUSE_LL, &OnMouse, Win32.GetModuleHandleW(null), 0);
        }
    }

    private static void Stop(ToolStripDropDown menu)
    {
        if (!ReferenceEquals(s_open, menu))
        {
            return;
        }

        s_open = null;
        if (s_hook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(s_hook);
            s_hook = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Called on the thread that set the hook, for every mouse event on the
    /// desktop, so it decides quickly and leaves the closing for afterwards.
    /// </summary>
    [UnmanagedCallersOnly]
    private static IntPtr OnMouse(int code, IntPtr message, IntPtr info)
    {
        if (code >= 0 && s_open is { IsDisposed: false } menu
            && (int)message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN)
        {
            // The first thing in MSLLHOOKSTRUCT is where the pointer is, on the screen.
            Point at = new(Marshal.ReadInt32(info), Marshal.ReadInt32(info, 4));
            IntPtr window = Win32.WindowFromPoint(at);
            uint thread = Win32.GetWindowThreadProcessId(window, out uint process);

            if (window != IntPtr.Zero && process == (uint)Environment.ProcessId
                && thread != Win32.GetCurrentThreadId())
            {
                menu.BeginInvoke(() => menu.Close(ToolStripDropDownCloseReason.AppClicked));
            }
        }

        return Win32.CallNextHookEx(IntPtr.Zero, code, message, info);
    }
}
