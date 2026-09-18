using CefSharp;

namespace ChromiumBrowser.Browser;

/// <summary>The window commands a key press can stand for.</summary>
public enum BrowserCommand
{
    NewTab,
    CloseTab,
    NextTab,
    PreviousTab,
    FocusAddress,
    Reload,
    ZoomIn,
    ZoomOut,
    ZoomReset,
    Print,
    BookmarkPage,
    ShowHistory,
    ShowDownloads,
}

/// <summary>
/// Catches the window's own keyboard shortcuts before the page sees them.
///
/// This is needed because the page is a window of its own: once it has the
/// focus, Windows delivers keys straight to the engine, and the form's own
/// key handling never runs. Ctrl+T inside a text box on a web page would
/// otherwise do nothing at all, which is the sort of thing that makes a browser
/// feel broken without anyone being able to say why.
///
/// Only the combinations this window claims are swallowed. Everything else —
/// Ctrl+C, Ctrl+F, the page's own shortcuts — is passed through untouched.
/// </summary>
public sealed class ShortcutHandler : IKeyboardHandler
{
    private readonly Action<BrowserCommand> _invoke;

    public ShortcutHandler(Action<BrowserCommand> invoke) => _invoke = invoke;

    public bool OnPreKeyEvent(
        IWebBrowser browser,
        IBrowser cef,
        KeyType type,
        int windowsKeyCode,
        int nativeKeyCode,
        CefEventFlags modifiers,
        bool isSystemKey,
        ref bool isKeyboardShortcut)
    {
        if (type != KeyType.RawKeyDown)
        {
            return false;
        }

        bool control = modifiers.HasFlag(CefEventFlags.ControlDown);
        bool shift = modifiers.HasFlag(CefEventFlags.ShiftDown);

        BrowserCommand? shortcut = Match(windowsKeyCode, control, shift);
        if (shortcut is null)
        {
            return false;
        }

        _invoke(shortcut.Value);
        return true;
    }

    public bool OnKeyEvent(
        IWebBrowser browser,
        IBrowser cef,
        KeyType type,
        int windowsKeyCode,
        int nativeKeyCode,
        CefEventFlags modifiers,
        bool isSystemKey) => false;

    /// <summary>The one table both this handler and the window's own keys read from.</summary>
    public static BrowserCommand? Match(int keyCode, bool control, bool shift) => (Keys)keyCode switch
    {
        Keys.F5 when !control => BrowserCommand.Reload,
        Keys.Tab when control && shift => BrowserCommand.PreviousTab,
        Keys.Tab when control => BrowserCommand.NextTab,
        Keys.T when control => BrowserCommand.NewTab,
        Keys.W when control => BrowserCommand.CloseTab,
        Keys.L when control => BrowserCommand.FocusAddress,
        Keys.R when control => BrowserCommand.Reload,
        Keys.P when control => BrowserCommand.Print,
        Keys.D when control => BrowserCommand.BookmarkPage,
        Keys.H when control => BrowserCommand.ShowHistory,
        Keys.J when control => BrowserCommand.ShowDownloads,
        Keys.D0 or Keys.NumPad0 when control => BrowserCommand.ZoomReset,

        // The plus and minus keys arrive under several names depending on
        // whether the number pad or the row above the letters was used, and on
        // the keyboard layout.
        Keys.Add or Keys.Oemplus when control => BrowserCommand.ZoomIn,
        Keys.Subtract or Keys.OemMinus when control => BrowserCommand.ZoomOut,
        _ => null,
    };
}
