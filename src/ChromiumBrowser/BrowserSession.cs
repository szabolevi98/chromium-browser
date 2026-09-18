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
    private readonly SessionStore _session;
    private readonly List<BrowserWindow> _windows = [];

    /// <summary>
    /// Writes the session a moment after the last change rather than on every
    /// one. A page loading fires several address changes in a row, and a window
    /// being dragged fires a great many: without this the file would be
    /// rewritten dozens of times for one action.
    /// </summary>
    private readonly System.Windows.Forms.Timer _saveSoon = new() { Interval = 1000 };

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
        SettingsStore settings,
        SessionStore session)
    {
        _session = session;
        _profile = profile;
        _bookmarks = bookmarks;
        _history = history;
        _downloads = downloads;
        _settings = settings;

        _saveSoon.Tick += (_, _) =>
        {
            _saveSoon.Stop();
            SaveSession();
        };
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

    public BrowserWindow Open(string? url, bool isPrivate, SavedWindow? restore = null)
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
            _internalPages!,
            restore);

        _windows.Add(window);

        // A private window is not part of what comes back, so it does not ask
        // for the session to be written either.
        if (!window.IsPrivate)
        {
            window.ContentsChanged += (_, _) => SaveLater();
            window.ResizeEnd += (_, _) => SaveLater();
        }

        window.FormClosed += (_, _) =>
        {
            _windows.Remove(window);

            // Written while the window is still in hand rather than after it has
            // gone: the last window closing is exactly the moment worth keeping,
            // and by the time the program is ending there is nothing left to ask.
            if (!window.IsPrivate)
            {
                SaveSession(closing: window);
            }

            if (_windows.Count == 0)
            {
                ExitThread();
            }
        };

        window.Show();
        SaveLater();
        return window;
    }

    /// <summary>
    /// Opens what a previous run left, and says whether there was anything. The
    /// windows come back in the order they were opened, so the one that was
    /// first is first again.
    /// </summary>
    public bool Restore()
    {
        if (!_settings.Current.RestoreSession || !_session.HasSomething)
        {
            return false;
        }

        foreach (SavedWindow saved in _session.Windows.Where(window => window.Tabs.Count > 0))
        {
            Open(null, isPrivate: false, saved);
        }

        return _windows.Count > 0;
    }

    private void SaveLater()
    {
        _saveSoon.Stop();
        _saveSoon.Start();
    }

    /// <summary>
    /// Writes what is open. A window that is closing is asked for its tabs
    /// before it has let go of them, but is not itself part of what comes back.
    /// </summary>
    private void SaveSession(BrowserWindow? closing = null)
    {
        List<SavedWindow> open = [.. _windows
            .Where(window => !window.IsPrivate && !window.IsDisposed)
            .Select(window => window.Snapshot())];

        // Closing the last window is what leaves the browser with something to
        // come back to; closing one of several leaves the others.
        if (closing is not null && open.Count == 0)
        {
            open.Add(closing.Snapshot());
        }

        _session.Save(open);
    }
}
