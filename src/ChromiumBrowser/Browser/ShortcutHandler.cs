using CefSharp;

namespace ChromiumBrowser.Browser;

/// <summary>The window commands a key press can stand for.</summary>
public enum BrowserCommand
{
    NewTab,
    NewWindow,
    NewPrivateWindow,
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
    ToggleBookmarksBar,
    ShowSettings,
    FindInPage,
    FindNext,
    FindPrevious,
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
/// Ctrl+C, Ctrl+A, the page's own shortcuts — is passed through untouched.
/// Ctrl+F is claimed: a browser's own find bar is expected to answer it, and a
/// page that wanted the key for its own search is the exception rather than the
/// rule.
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
        Keys.F3 when shift => BrowserCommand.FindPrevious,
        Keys.F3 => BrowserCommand.FindNext,
        Keys.F when control => BrowserCommand.FindInPage,
        Keys.Tab when control && shift => BrowserCommand.PreviousTab,
        Keys.Tab when control => BrowserCommand.NextTab,
        Keys.T when control => BrowserCommand.NewTab,
        Keys.N when control && shift => BrowserCommand.NewPrivateWindow,
        Keys.N when control => BrowserCommand.NewWindow,
        Keys.W when control => BrowserCommand.CloseTab,
        Keys.L when control => BrowserCommand.FocusAddress,
        Keys.R when control => BrowserCommand.Reload,
        Keys.P when control => BrowserCommand.Print,
        Keys.D when control => BrowserCommand.BookmarkPage,
        Keys.H when control => BrowserCommand.ShowHistory,
        Keys.J when control => BrowserCommand.ShowDownloads,
        Keys.B when control && shift => BrowserCommand.ToggleBookmarksBar,
        Keys.D0 or Keys.NumPad0 when control => BrowserCommand.ZoomReset,

        // The plus and minus keys arrive under several names depending on
        // whether the number pad or the row above the letters was used, and on
        // the keyboard layout.
        Keys.Add or Keys.Oemplus when control => BrowserCommand.ZoomIn,
        Keys.Subtract or Keys.OemMinus when control => BrowserCommand.ZoomOut,
        _ => null,
    };
}
