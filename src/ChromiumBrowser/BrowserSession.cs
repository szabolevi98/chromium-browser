using CefSharp;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Profile;

namespace ChromiumBrowser;

/// <summary>
/// Every window the program has open, and what they share.
///
/// A browser cannot be one window: the second one has to be able to outlive the
/// first, and the program has to end when the last of them closes rather than
/// when whichever happened to be opened first does. That is what an application
/// context is for, and it is why <see cref="Application.Run(ApplicationContext)"/>
/// is given one of these instead of a form.
///
/// The bookmarks, the history, the downloads and the settings belong here rather
/// than to a window, so two windows share one list instead of each holding a
/// copy and the last one to close winning.
/// </summary>
internal sealed class BrowserSession : ApplicationContext
{
    private readonly ProfileLocation _profile;
    private readonly BookmarkStore _bookmarks;
    private readonly HistoryStore _history;
    private readonly DownloadStore _downloads;
    private readonly SettingsStore _settings;
    private readonly List<BrowserWindow> _windows = [];

    /// <summary>
    /// Answers <c>browser://</c> addresses. Held because a private window
    /// browses in a context of its own, which has to be told about the scheme
    /// separately.
    /// </summary>
    private ISchemeHandlerFactory? _internalPages;

    public BrowserSession(
        ProfileLocation profile,
        BookmarkStore bookmarks,
        HistoryStore history,
        DownloadStore downloads,
        SettingsStore settings)
    {
        _profile = profile;
        _bookmarks = bookmarks;
        _history = history;
        _downloads = downloads;
        _settings = settings;
    }

    /// <summary>Set once, before the first window, by the code that registers the scheme.</summary>
    public ISchemeHandlerFactory? InternalPages
    {
        get => _internalPages;
        set => _internalPages = value;
    }

    /// <summary>
    /// Any window that is up, for work that has to happen on the thread the
    /// windows live on. The engine asks for the browser's own pages from a
    /// thread of its own and needs somewhere to hand that work to; which window
    /// it is does not matter, only that it is alive.
    /// </summary>
    public Control? AnyWindow =>
        _windows.FirstOrDefault(window => !window.IsDisposed && window.IsHandleCreated);

    /// <summary>The window opened most recently, which is where a handed-over address goes.</summary>
    public BrowserWindow? Newest => _windows.Count > 0 ? _windows[^1] : null;

    public BrowserWindow Open(string? url, bool isPrivate)
    {
        BrowserWindow window = new(
            _profile,
            _bookmarks,
            _history,
            _downloads,
            _settings,
            url,
            isPrivate,
            (address, secret) => Open(address, secret),
            _internalPages!);

        _windows.Add(window);

        window.FormClosed += (_, _) =>
        {
            _windows.Remove(window);

            if (_windows.Count == 0)
            {
                ExitThread();
            }
        };

        window.Show();
        return window;
    }
}
