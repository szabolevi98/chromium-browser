using System.Runtime.InteropServices;
using CefSharp;
using CefSharp.Handler;
using CefSharp.WinForms;
using ChromiumBrowser.Browser;
using ChromiumBrowser.Controls;
using ChromiumBrowser.Core;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Profile;
using ChromiumBrowser.Core.Ui;
using ChromiumBrowser.Core.Web;
using ChromiumBrowser.Native;
using ChromiumBrowser.Ui;
using ChromiumBrowser.Core.Localisation;

namespace ChromiumBrowser;

/// <summary>
/// The window, which draws its own title bar.
///
/// Windows is still doing the work — the frame, the shadow, snapping, the
/// animations, the rounded corners — because the window keeps a real frame and
/// only takes the caption band back for itself, in
/// <see cref="OnNonClientCalcSize"/>. Building a border out of panels instead is
/// what makes home-made title bars feel wrong: they lose the snap layouts, they
/// resize a frame behind the pointer, and they never quite match the desktop.
/// </summary>
public sealed class BrowserWindow : Form
{
    /// <summary>The band the tabs live in, in device-independent pixels.</summary>
    private const int CaptionHeight = 40;

    private const int ToolbarHeight = 44;

    private const int BookmarksBarHeight = 34;

    private const int FindBarHeight = 38;

    /// <summary>
    /// A sliver of window above the tabs. It exists so the top edge can still be
    /// grabbed to resize: every pixel covered by a child control belongs to that
    /// control, and a window whose top edge cannot be dragged is maddening.
    /// </summary>
    private const int TopResizeStrip = 6;

    private readonly ProfileLocation _profile;
    private readonly TabStripControl _tabStrip = new();
    private readonly CaptionButtons _captionButtons = new();
    private readonly ToolbarControl _toolbar = new();
    private readonly BookmarksBarControl _bookmarksBar = new();
    private readonly FindBarControl _findBar = new();

    /// <summary>
    /// The one menu this window opens, whichever button opened it. Kept rather
    /// than built per click, because there is no safe moment to throw a menu
    /// away while the click that closed it is still being dealt with.
    /// </summary>
    private readonly ContextMenuStrip _menu = DarkMenu.Create(new Font("Segoe UI", 9f));
    private readonly Panel _pages = new() { Dock = DockStyle.None };
    private readonly List<ChromiumWebBrowser> _browsers = [];
    private readonly Dictionary<ChromiumWebBrowser, AudioWatcher> _audio = [];
    private readonly List<(string Url, string Title, int Index, bool Muted)> _closedTabs = [];
    private ChromiumWebBrowser? _shownBrowser;
    private bool _fullscreen;
    private Rectangle _beforeFullscreen;
    private FormWindowState _beforeFullscreenState;
    private readonly Button _exitFullscreen = new() { AutoSize = true, Visible = false, TabStop = true };

    /// <summary>The zoom each page is at, which the engine does not hand back synchronously.</summary>
    private readonly Dictionary<ChromiumWebBrowser, double> _zoom = [];

    private readonly BookmarkStore _bookmarks;

    /// <summary>Icons for the bookmarks bar, guessed from each site's root.</summary>
    private readonly Dictionary<string, Image?> _bookmarkIcons = new(StringComparer.OrdinalIgnoreCase);

    private readonly HistoryStore _history;
    private readonly DownloadStore _downloads;
    private readonly SettingsStore _settings;

    /// <summary>Whether this window is one that remembers nothing.</summary>
    private readonly bool _isPrivate;

    /// <summary>
    /// The cookies and cache a private window browses with, which live in memory
    /// and go when it closes. A normal window has none of its own: it shares the
    /// engine's, which is what keeps you logged in between sessions.
    /// </summary>
    private readonly IRequestContext? _context;
    private readonly ISchemeHandlerFactory _internalPages;

    /// <summary>Opens another window: the address, and whether it is private.</summary>
    private readonly Action<string?, bool> _openWindow;

    public BrowserWindow(
        ProfileLocation profile,
        BookmarkStore bookmarks,
        HistoryStore history,
        DownloadStore downloads,
        SettingsStore settings,
        string? startUrl,
        bool isPrivate,
        Action<string?, bool> openWindow,
        ISchemeHandlerFactory internalPages,
        SavedWindow? restore = null)
    {
        _settings = settings;
        _profile = profile;
        _bookmarks = bookmarks;
        _history = history;
        _isPrivate = isPrivate;
        _openWindow = openWindow;

        // What a private window downloads is not written down. The file lands
        // wherever it was asked to land — that is the point of downloading it —
        // but the list of what was fetched goes when the window does.
        _downloads = isPrivate ? new DownloadStore(string.Empty) : downloads;

        ISchemeHandlerFactory pages = internalPages is InternalSchemeFactory factory
            ? factory.WithDownloads(_downloads) : internalPages;
        _internalPages = pages;
        _context = isPrivate ? PrivateContext(pages) : null;

        Text = Branding.Name;
        Icon = AppIcon();
        MinimumSize = new Size(560, 380);
        Size = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        BackColor = Theme.Current.Chrome;

        _tabStrip.SelectedIndexChanged += (_, _) =>
        {
            ShowSelectedPage();
            ContentsChanged?.Invoke(this, EventArgs.Empty);
        };
        _tabStrip.TabCloseRequested += (_, index) => CloseTab(index);
        _tabStrip.NewTabRequested += (_, _) => OpenTab(NewTabPage);
        _tabStrip.TabMoved += (_, move) => MoveBrowser(move.From, move.To);
        _tabStrip.TabMenuRequested += (_, request) => ShowTabMenu(request.Index, request.At);
        _tabStrip.MuteRequested += (_, index) => ToggleMute(index);

        _captionButtons.MinimiseClicked += (_, _) => WindowState = FormWindowState.Minimized;
        _captionButtons.MaximiseClicked += (_, _) => ToggleMaximise();
        _captionButtons.CloseClicked += (_, _) => Close();

        _toolbar.BackRequested += (_, _) => Selected?.Back();
        _toolbar.ForwardRequested += (_, _) => Selected?.Forward();
        _toolbar.ReloadRequested += (_, _) => Selected?.Reload();
        _toolbar.StopRequested += (_, _) => Selected?.Stop();
        _toolbar.HomeRequested += (_, _) => Selected?.LoadUrl(HomePage);
        _toolbar.Navigated += (_, typed) =>
        {
            string url = AddressParser.Parse(typed, _settings.Current.SearchTemplate, InternalPages.Scheme);
            if (url.Length > 0)
            {
                _toolbar.ShowAddress(url, force: true);
                Selected?.LoadUrl(url);
                FocusPage();
            }
        };
        _toolbar.AddressFocused += (_, _) => _toolbar.SetSuggestions(_bookmarks.All.Select(b => b.Url)
            .Concat(_isPrivate ? [] : _history.All.Select(h => h.Url)));
        _toolbar.MenuRequested += (_, at) => ShowMenu(at);

        _bookmarksBar.IconFor = BookmarkIcon;
        _bookmarksBar.Requested += (_, asked) => DoWithBookmark(asked.Bookmark, asked.Action);
        _bookmarksBar.OverflowRequested += (_, overflow) => ShowOverflow(overflow.At, overflow.Hidden);
        _bookmarksBar.Show(_bookmarks.All);

        _findBar.SearchChanged += (_, text) => Find(text, forward: true, again: false);
        _findBar.StepRequested += (_, forward) => Find(_findBar.SearchText, forward, again: true);
        _findBar.CloseRequested += (_, _) => CloseFind();

        Controls.Add(_pages);
        Controls.Add(_findBar);
        Controls.Add(_bookmarksBar);
        Controls.Add(_toolbar);
        Controls.Add(_tabStrip);
        Controls.Add(_captionButtons);
        Controls.Add(_exitFullscreen);
        _exitFullscreen.Click += (_, _) => SetFullscreen(false);

        Theme.Changed += OnThemeChanged;
        _settings.Changed += OnSettingsChanged;
        _bookmarks.Changed += OnBookmarksChanged;

        _toolbar.IsPrivate = isPrivate;

        if (restore is { Tabs.Count: > 0 })
        {
            RestoreFrom(restore);
        }
        else
        {
            OpenTab(startUrl ?? NewTabPage);
        }
    }

    /// <summary>
    /// A context of its own for a private window, with its cookies and cache in
    /// memory.
    ///
    /// The browser's own pages have to be registered on it as well. A scheme
    /// handler belongs to the context that was browsing when it was registered,
    /// not to the program, so a private window that was not told about
    /// <c>browser://</c> answers its own settings page with "unknown scheme".
    /// </summary>
    private static IRequestContext PrivateContext(ISchemeHandlerFactory internalPages)
    {
        RequestContextHandler handler = new();
        handler.OnInitialize(context =>
        {
            bool registered = context.RegisterSchemeHandlerFactory(InternalPages.Scheme, string.Empty, internalPages);
            System.Diagnostics.Debug.WriteLine($"Private scheme registered: {registered}");
        });

        return new RequestContext(
            new RequestContextSettings
            {
                CachePath = string.Empty,
                PersistSessionCookies = false,
            },
            handler);
    }

    /// <summary>
    /// The icon the taskbar and Alt+Tab show. The window draws its own caption
    /// and has no room for it up there, but everywhere else Windows asks.
    /// </summary>
    internal static Icon? AppIcon()
    {
        using Stream? stream = typeof(BrowserWindow).Assembly
            .GetManifestResourceStream("ChromiumBrowser.app.ico");

        return stream is null ? null : new Icon(stream);
    }

    private string HomePage => _settings.Current.HomePage;

    /// <summary>
    /// Where a new tab starts. A private window opens on the page explaining
    /// what it keeps and what it does not, rather than on a home page that may
    /// well be a site that knows who you are.
    /// </summary>
    private string NewTabPage => _isPrivate ? $"{InternalPages.Scheme}://private" : HomePage;

    private ChromiumWebBrowser? Selected =>
        _tabStrip.SelectedIndex >= 0 && _tabStrip.SelectedIndex < _browsers.Count
            ? _browsers[_tabStrip.SelectedIndex]
            : null;

    private float UiScale => DeviceDpi / 96f;

    private int Dip(int value) => (int)(value * UiScale);

    // ------------------------------------------------------------------- tabs

    private void OpenTab(string url, string? knownTitle = null, bool muted = false)
    {
        ChromiumWebBrowser browser = new(url, _context) { Dock = DockStyle.Fill };
        browser.RequestHandler = new InternalRequestHandler(_internalPages);

        browser.TitleChanged += (_, e) => OnUiThread(() =>
        {
            int at = _browsers.IndexOf(browser);
            if (at < 0)
            {
                return;
            }

            _tabStrip.Tabs[at].Title = string.IsNullOrWhiteSpace(e.Title) ? Strings.Of("tab.new") : e.Title;
            _tabStrip.Refresh(at);
            if (at == _tabStrip.SelectedIndex)
            {
                Text = WindowTitle(_tabStrip.Tabs[at].Title);
            }
        });

        browser.LoadingStateChanged += (_, e) => OnUiThread(() =>
        {
            int at = _browsers.IndexOf(browser);
            if (at < 0)
            {
                return;
            }

            _tabStrip.Tabs[at].IsLoading = e.IsLoading;
            _tabStrip.Refresh(at);

            if (at != _tabStrip.SelectedIndex)
            {
                return;
            }

            _toolbar.CanGoBack = e.CanGoBack;
            _toolbar.CanGoForward = e.CanGoForward;
            _toolbar.IsLoading = e.IsLoading;
            _toolbar.Invalidate();
        });

        browser.LoadingStateChanged += (_, e) => OnUiThread(() =>
        {
            // Recorded when the page settles rather than when it is asked for,
            // so a redirect leaves the address it ended at and the title it
            // ended up with, instead of a list of places passed through.
            if (!e.IsLoading && !_isPrivate)
            {
                _history.Record(browser.Address ?? string.Empty, TitleOf(browser), DateTimeOffset.Now);
            }
        });

        browser.DownloadHandler = new DownloadRouter(_downloads, OnUiThread, () => { });

        browser.AddressChanged += (_, e) => OnUiThread(() =>
        {
            if (_browsers.IndexOf(browser) == _tabStrip.SelectedIndex)
            {
                _toolbar.ShowAddress(e.Address);
            }

            ContentsChanged?.Invoke(this, EventArgs.Empty);
        });

        browser.KeyboardHandler = new ShortcutHandler(shortcut => OnUiThread(() => Run(shortcut)),
            () => _fullscreen || _findBar.Visible);
        AudioWatcher audio = new(audible => OnUiThread(() =>
        {
            int at = _browsers.IndexOf(browser);
            if (at < 0) return;
            _tabStrip.Tabs[at].IsAudible = audible;
            _tabStrip.Refresh(at);
        }));
        _audio[browser] = audio;
        browser.IsBrowserInitializedChanged += (_, _) =>
        {
            if (browser.IsBrowserInitialized)
            {
                browser.GetBrowser().GetHost().SetAudioMuted(muted);
                audio.Attach(browser.GetBrowser());
            }
        };

        browser.FindHandler = new FindWatcher(result => OnUiThread(() =>
        {
            // A result from a page that is no longer the one on screen would
            // relabel the bar with another tab's count.
            if (ReferenceEquals(browser, Selected))
            {
                _findBar.ShowCounter(FindCounter.Text(_findBar.SearchText, result.Count, result.Active));
            }
        }));

        browser.DisplayHandler = new FaviconWatcher(async url =>
        {
            Image? icon = await FaviconCache.GetAsync(url).ConfigureAwait(false);
            if (icon is null)
            {
                return;
            }

            OnUiThread(() =>
            {
                int at = _browsers.IndexOf(browser);
                if (at < 0)
                {
                    return;
                }

                _tabStrip.Tabs[at].Icon = icon;
                _tabStrip.Refresh(at);
            });
        });

        _browsers.Add(browser);
        _pages.Controls.Add(browser);
        browser.CreateControl();
        _tabStrip.AddTab(new TabItem
        {
            Title = string.IsNullOrWhiteSpace(knownTitle) ? Strings.Of("tab.new") : knownTitle,
            IsLoading = true,
            IsMuted = muted,
        });

        ShowSelectedPage();
        ContentsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CloseTab(int index)
    {
        if (index < 0 || index >= _browsers.Count)
        {
            return;
        }

        if (_browsers.Count == 1)
        {
            Close(); // closing the last tab closes the window, as everywhere else
            return;
        }

        ChromiumWebBrowser browser = _browsers[index];
        _closedTabs.Add((browser.Address ?? NewTabPage, _tabStrip.Tabs[index].Title, index, _tabStrip.Tabs[index].IsMuted));
        if (_closedTabs.Count > 50) _closedTabs.RemoveAt(0);
        _browsers.RemoveAt(index);
        _zoom.Remove(browser);
        _pages.Controls.Remove(browser);
        if (_audio.Remove(browser, out AudioWatcher? audio)) audio.Dispose();
        (browser.DownloadHandler as IDisposable)?.Dispose();
        browser.Dispose();

        _tabStrip.RemoveTab(index);
        ShowSelectedPage();
        ContentsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MoveBrowser(int from, int to)
    {
        if (from == to || from < 0 || from >= _browsers.Count)
        {
            return;
        }

        ChromiumWebBrowser browser = _browsers[from];
        _browsers.RemoveAt(from);
        _browsers.Insert(Math.Clamp(to, 0, _browsers.Count), browser);
        ContentsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowSelectedPage()
    {
        // A search belongs to the page it was typed against, so moving to
        // another tab ends it rather than carrying a count across.
        CloseFind();
        _shownBrowser = Selected;

        ChromiumWebBrowser? selected = Selected;
        foreach (ChromiumWebBrowser browser in _browsers)
        {
            browser.Visible = ReferenceEquals(browser, selected);
        }

        if (selected is null)
        {
            return;
        }

        selected.BringToFront();
        _toolbar.ShowAddress(selected.Address ?? string.Empty, force: true);
        _toolbar.CanGoBack = selected.CanGoBack;
        _toolbar.CanGoForward = selected.CanGoForward;
        _toolbar.IsLoading = _tabStrip.Tabs[_tabStrip.SelectedIndex].IsLoading;
        _toolbar.Invalidate();

        int at = _tabStrip.SelectedIndex;
        if (at >= 0 && at < _tabStrip.Tabs.Count)
        {
            Text = WindowTitle(_tabStrip.Tabs[at].Title);
        }
    }

    // --------------------------------------------------------------- commands

    /// <summary>
    /// The window's own keys, for when the chrome has the focus. The page has
    /// its own path through <see cref="ShortcutHandler"/>, and both read the
    /// same table so the two can never drift apart.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        BrowserCommand? shortcut = ShortcutHandler.Match(
            (int)(keyData & Keys.KeyCode),
            keyData.HasFlag(Keys.Control),
            keyData.HasFlag(Keys.Shift), keyData.HasFlag(Keys.Alt));

        if ((keyData & Keys.KeyCode) == Keys.Escape && _findBar.ContainsFocus)
        {
            CloseFind();
            return true;
        }

        if (shortcut is null)
        {
            return base.ProcessCmdKey(ref message, keyData);
        }

        Run(shortcut.Value);
        return true;
    }

    private void Run(BrowserCommand shortcut)
    {
        if (shortcut >= BrowserCommand.Tab1 && shortcut <= BrowserCommand.Tab8)
        {
            _tabStrip.SelectedIndex = shortcut - BrowserCommand.Tab1;
            return;
        }
        switch (shortcut)
        {
            case BrowserCommand.ReopenTab: ReopenTab(); break;
            case BrowserCommand.LastTab: _tabStrip.SelectedIndex = _browsers.Count - 1; break;
            case BrowserCommand.Back: Selected?.Back(); break;
            case BrowserCommand.Forward: Selected?.Forward(); break;
            case BrowserCommand.HardReload: Selected?.Reload(ignoreCache: true); break;
            case BrowserCommand.Fullscreen: SetFullscreen(!_fullscreen); break;
            case BrowserCommand.Escape:
                if (_fullscreen) SetFullscreen(false);
                else if (_toolbar.AddressHasFocus) { _toolbar.CancelAddressEdit(); FocusPage(); }
                else if (_findBar.Visible) CloseFind();
                else Selected?.Stop();
                break;
            case BrowserCommand.NewTab: OpenTab(NewTabPage); break;
            case BrowserCommand.NewWindow: _openWindow(null, false); break;
            case BrowserCommand.NewPrivateWindow: _openWindow(null, true); break;
            case BrowserCommand.CloseTab: CloseTab(_tabStrip.SelectedIndex); break;
            case BrowserCommand.NextTab: StepTab(1); break;
            case BrowserCommand.PreviousTab: StepTab(-1); break;
            case BrowserCommand.FocusAddress: PageKeyboard(mine: false); _toolbar.FocusAddress(); break;
            case BrowserCommand.Reload: Selected?.Reload(); break;
            case BrowserCommand.Print: Selected?.Print(); break;
            case BrowserCommand.ZoomIn: Zoom(0.5); break;
            case BrowserCommand.ZoomOut: Zoom(-0.5); break;
            case BrowserCommand.ZoomReset: Zoom(null); break;
            case BrowserCommand.BookmarkPage: ToggleBookmark(); break;
            case BrowserCommand.ToggleBookmarksBar:
                _settings.Save(_settings.Current with
                {
                    ShowBookmarksBar = !_settings.Current.ShowBookmarksBar,
                });
                break;

            case BrowserCommand.FindInPage: OpenFind(); break;
            case BrowserCommand.FindNext: Find(_findBar.SearchText, forward: true, again: true); break;
            case BrowserCommand.FindPrevious: Find(_findBar.SearchText, forward: false, again: true); break;

            case BrowserCommand.ShowSettings: OpenTab($"{InternalPages.Scheme}://settings"); break;
            case BrowserCommand.ShowHistory: OpenTab($"{InternalPages.Scheme}://history"); break;
            case BrowserCommand.ShowDownloads: OpenTab($"{InternalPages.Scheme}://downloads"); break;
        }
    }

    private void FocusPage()
    {
        Selected?.Focus();
        PageKeyboard(mine: true);
    }

    private void ReopenTab()
    {
        if (_closedTabs.Count == 0) return;
        var closed = _closedTabs[^1];
        _closedTabs.RemoveAt(_closedTabs.Count - 1);
        OpenTab(closed.Url, closed.Title, closed.Muted);
        int from = _browsers.Count - 1;
        int to = Math.Clamp(closed.Index, 0, from);
        _tabStrip.MoveTab(from, to);
    }

    private void ToggleMute(int index)
    {
        if (index < 0 || index >= _browsers.Count || !_browsers[index].IsBrowserInitialized) return;
        TabItem tab = _tabStrip.Tabs[index];
        tab.IsMuted = !tab.IsMuted;
        _browsers[index].GetBrowser().GetHost().SetAudioMuted(tab.IsMuted);
        _tabStrip.Refresh(index);
    }

    private void ShowTabMenu(int index, Point at)
    {
        ContextMenuStrip menu = _menu.Reset();
        menu.Add(Strings.Of("menu.newTab"), "Ctrl+T", () => OpenTab(NewTabPage));
        menu.Add(Strings.Of("tab.reopen"), "Ctrl+Shift+T", ReopenTab);
        menu.Items[^1].Enabled = _closedTabs.Count > 0;
        if (index >= 0 && index < _browsers.Count)
        {
            ChromiumWebBrowser target = _browsers[index];
            menu.Separator();
            menu.Add(Strings.Of("tab.duplicate"), string.Empty, () =>
            {
                int current = _browsers.IndexOf(target);
                if (current >= 0) OpenTab(target.Address ?? NewTabPage, _tabStrip.Tabs[current].Title);
            });
            menu.Add(Strings.Of(_tabStrip.Tabs[index].IsMuted ? "tab.unmute" : "tab.mute"), string.Empty,
                () => ToggleMute(_browsers.IndexOf(target)));
            menu.Add(Strings.Of("menu.closeTab"), "Ctrl+W", () => CloseTab(_browsers.IndexOf(target)));
            menu.Add(Strings.Of("tab.closeOthers"), string.Empty, () => CloseRelativeTabs(target, rightOnly: false));
            menu.Add(Strings.Of("tab.closeRight"), string.Empty, () => CloseRelativeTabs(target, rightOnly: true));
        }
        menu.ShowAt(_tabStrip, at);
    }

    private void CloseRelativeTabs(ChromiumWebBrowser kept, bool rightOnly)
    {
        int keep = _browsers.IndexOf(kept);
        if (keep < 0) return;
        for (int i = _browsers.Count - 1; i >= 0; i--)
            if (i != keep && (!rightOnly || i > keep)) CloseTab(i);
    }

    private void SetFullscreen(bool enabled)
    {
        if (_fullscreen == enabled) return;
        CloseFind();
        SuspendLayout();
        if (enabled)
        {
            _beforeFullscreenState = WindowState;
            _beforeFullscreen = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            Rectangle screen = Screen.FromControl(this).Bounds;
            _fullscreen = true;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = screen;
        }
        else
        {
            _fullscreen = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = _beforeFullscreen;
            WindowState = _beforeFullscreenState;
        }
        _exitFullscreen.Text = Strings.Of("window.exitFullscreen") + " (F11 / Esc)";
        _exitFullscreen.Visible = enabled;
        ResumeLayout(performLayout: true);
        _exitFullscreen.BringToFront();
        ContentsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// What the taskbar and the window list say. A private window says so there
    /// too, because that is where you look when several are open at once.
    /// </summary>
    private string WindowTitle(string tab) =>
        _isPrivate
            ? $"{tab} - {Branding.Name} ({Strings.Of("private.badge")})"
            : $"{tab} - {Branding.Name}";

    /// <summary>
    /// Raised whenever what this window holds has changed in a way worth
    /// writing down: a tab opened, closed, moved or gone somewhere else.
    /// </summary>
    public event EventHandler? ContentsChanged;

    /// <summary>Whether this window is one that keeps nothing.</summary>
    public bool IsPrivate => _isPrivate;

    /// <summary>
    /// What this window would need to be brought back: its tabs, which one was
    /// in front, and where it sat. A maximised window reports the size it would
    /// go back to, so restoring it maximised does not leave it filling the
    /// screen at the size of the screen once un-maximised.
    /// </summary>
    public SavedWindow Snapshot()
    {
        Rectangle bounds = _fullscreen ? _beforeFullscreen : WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;

        List<SavedTab> tabs = [];
        for (int index = 0; index < _browsers.Count; index++)
        {
            string url = _browsers[index].Address ?? string.Empty;

            // A tab that never got anywhere is not worth bringing back, and an
            // address the engine has not settled on yet would come back as
            // about:blank.
            if (url.Length == 0 || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            tabs.Add(new SavedTab
            {
                Url = url,
                Title = index < _tabStrip.Tabs.Count ? _tabStrip.Tabs[index].Title : string.Empty,
            });
        }

        return new SavedWindow
        {
            Tabs = tabs,
            Selected = Math.Clamp(_tabStrip.SelectedIndex, 0, Math.Max(0, tabs.Count - 1)),
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximised = (_fullscreen ? _beforeFullscreenState : WindowState) == FormWindowState.Maximized,
        };
    }

    /// <summary>
    /// Opens the tabs a previous run left, and puts the window back where it
    /// was — but only if that place is still on a screen, because a window
    /// restored onto a monitor that has since been unplugged is a window
    /// nobody can reach.
    /// </summary>
    private void RestoreFrom(SavedWindow saved)
    {
        if (saved.Width > 200 && saved.Height > 150)
        {
            Rectangle wanted = new(saved.X, saved.Y, saved.Width, saved.Height);
            Rectangle work = Screen.FromRectangle(wanted).WorkingArea;
            wanted.Size = new Size(Math.Min(Math.Max(MinimumSize.Width, wanted.Width), work.Width),
                Math.Min(Math.Max(MinimumSize.Height, wanted.Height), work.Height));
            wanted.X = Math.Clamp(wanted.X, work.Left, Math.Max(work.Left, work.Right - wanted.Width));
            wanted.Y = Math.Clamp(wanted.Y, work.Top, Math.Max(work.Top, work.Bottom - wanted.Height));
            StartPosition = FormStartPosition.Manual;
            Bounds = wanted;
        }

        foreach (SavedTab tab in saved.Tabs)
        {
            OpenTab(tab.Url, tab.Title);
        }

        if (saved.Selected >= 0 && saved.Selected < _tabStrip.Tabs.Count)
        {
            _tabStrip.SelectedIndex = saved.Selected;
        }

        if (saved.Maximised)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    /// <summary>
    /// Takes an address from a second launch: a tab if there is one, and the
    /// window brought to the front either way, because somebody just asked for
    /// this browser and is looking at whatever is covering it.
    /// </summary>
    public void Accept(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            OpenTab(url);
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Activate();
    }

    private string TitleOf(ChromiumWebBrowser browser)
    {
        int at = _browsers.IndexOf(browser);
        return at >= 0 && at < _tabStrip.Tabs.Count ? _tabStrip.Tabs[at].Title : string.Empty;
    }

    private void ToggleBookmark()
    {
        ChromiumWebBrowser? browser = Selected;
        string? url = browser?.Address;
        if (browser is null || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        _bookmarks.Toggle(url, TitleOf(browser));
        RefreshBookmarks();
    }

    private void RefreshBookmarks()
    {
        _bookmarksBar.Show(_bookmarks.All);
        PerformLayout();
        _bookmarksBar.Invalidate();
    }

    private void OnBookmarksChanged(object? sender, EventArgs e) => RefreshBookmarks();

    private void DoWithBookmark(Bookmark bookmark, BookmarkAction action)
    {
        switch (action)
        {
            case BookmarkAction.Open:
                Selected?.LoadUrl(bookmark.Url);
                break;

            case BookmarkAction.OpenInNewTab:
                OpenTab(bookmark.Url);
                break;

            case BookmarkAction.Remove:
                _bookmarks.Remove(bookmark.Url);
                RefreshBookmarks();
                break;

            case BookmarkAction.Rename:
                using (PromptForm prompt = new(Strings.Of("bookmark.renameTitle"), Strings.Of("bookmark.name"), bookmark.Title))
                {
                    if (prompt.ShowDialog(this) == DialogResult.OK && prompt.Value.Length > 0)
                    {
                        _bookmarks.Rename(bookmark.Url, prompt.Value);
                        RefreshBookmarks();
                    }
                }

                break;
        }
    }

    private void ShowOverflow(Point at, IReadOnlyList<Bookmark> hidden)
    {
        ContextMenuStrip menu = _menu.Reset();

        foreach (Bookmark bookmark in hidden)
        {
            string title = string.IsNullOrWhiteSpace(bookmark.Title) ? bookmark.Url : bookmark.Title;
            menu.Add(title, string.Empty, () => Selected?.LoadUrl(bookmark.Url));
        }

        menu.ShowAt(_bookmarksBar, at);
    }

    /// <summary>
    /// The icon for a bookmark. Only the page's address is known, not the icon's,
    /// so the site's root is asked for the usual name; it is fetched once and
    /// then remembered, and until it arrives the bookmark simply has no icon.
    /// </summary>
    private Image? BookmarkIcon(Bookmark bookmark)
    {
        if (!Uri.TryCreate(bookmark.Url, UriKind.Absolute, out Uri? address)
            || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        string host = address.Host;
        if (_bookmarkIcons.TryGetValue(host, out Image? known))
        {
            return known;
        }

        _bookmarkIcons[host] = null;
        string guess = $"{address.Scheme}://{host}/favicon.ico";

        _ = Task.Run(async () =>
        {
            Image? icon = await FaviconCache.GetAsync(guess).ConfigureAwait(false);
            OnUiThread(() =>
            {
                _bookmarkIcons[host] = icon;
                _bookmarksBar.Invalidate();
            });
        });

        return null;
    }

    private void StepTab(int direction)
    {
        if (_tabStrip.Tabs.Count < 2)
        {
            return;
        }

        int count = _tabStrip.Tabs.Count;
        _tabStrip.SelectedIndex = ((_tabStrip.SelectedIndex + direction) % count + count) % count;
    }

    /// <summary>
    /// Zoom, in the steps the engine thinks in: a level rather than a
    /// percentage, where each whole number is a factor of 1.2. Passing nothing
    /// puts the page back to its own size.
    /// </summary>
    private void Zoom(double? step)
    {
        ChromiumWebBrowser? browser = Selected;
        if (browser is null)
        {
            return;
        }

        double current = _zoom.GetValueOrDefault(browser);
        double level = step is null ? 0 : Math.Clamp(current + step.Value, -4, 6);
        _zoom[browser] = level;
        browser.SetZoomLevel(level);
    }

    // ------------------------------------------------------ finding on a page

    /// <summary>
    /// Shows the find bar. Pressing the shortcut while it is already open puts
    /// the cursor back in the box with the last search selected, which is what
    /// makes the key usable for "search for something else" as well.
    /// </summary>
    private void OpenFind()
    {
        bool wasHidden = !_findBar.Visible;

        // The page holds the keyboard the way a window does, not the way a
        // control does: it is the engine's own window, and asking a text box
        // beside it to take the focus leaves the typing going into the page.
        // The engine has to be told to let go first.
        PageKeyboard(mine: false);
        _findBar.Open();

        if (wasHidden)
        {
            PerformLayout();
        }

        // Reopening on a page that still holds a search: ask again, so the
        // matches light up rather than the bar showing a count for highlights
        // that are no longer drawn.
        if (_findBar.SearchText.Length > 0)
        {
            Find(_findBar.SearchText, forward: true, again: false);
        }
    }

    /// <summary>
    /// Asks the page for a word. <paramref name="again"/> is what separates a
    /// fresh search from walking the matches of one already running: the engine
    /// starts over for the first and steps for the second.
    /// </summary>
    private void Find(string text, bool forward, bool again)
    {
        ChromiumWebBrowser? browser = Selected;
        if (browser is null)
        {
            return;
        }

        if (text.Length == 0)
        {
            browser.StopFinding(clearSelection: true);
            _findBar.ShowCounter(string.Empty);
            return;
        }

        browser.Find(text, forward, matchCase: false, again);
    }

    /// <summary>
    /// Hides the bar and takes the highlighting with it. What was searched for
    /// stays in the box, because the next Ctrl+F is usually the same word.
    /// </summary>
    private void CloseFind()
    {
        if (!_findBar.Visible)
        {
            return;
        }

        if (_shownBrowser is { IsDisposed: false, IsBrowserInitialized: true })
            _shownBrowser.StopFinding(clearSelection: true);
        _findBar.ShowCounter(string.Empty);
        _findBar.Visible = false;
        PerformLayout();
        PageKeyboard(mine: true);
    }

    /// <summary>
    /// Hands the keyboard to the page, or takes it back for the chrome.
    /// </summary>
    private void PageKeyboard(bool mine)
    {
        ChromiumWebBrowser? browser = Selected;
        if (browser is not { IsBrowserInitialized: true })
        {
            return;
        }

        browser.GetBrowser().GetHost().SetFocus(mine);
    }

    private void ShowMenu(Point at)
    {
        ContextMenuStrip menu = _menu.Reset();

        void Item(string text, string keys, Action action) => menu.Add(text, keys, action);

        Item(Strings.Of("menu.newTab"), "Ctrl+T", () => OpenTab(NewTabPage));
        Item(Strings.Of("menu.newWindow"), "Ctrl+N", () => _openWindow(null, false));
        Item(Strings.Of("menu.newPrivateWindow"), "Ctrl+Shift+N", () => _openWindow(null, true));
        Item(Strings.Of("menu.closeTab"), "Ctrl+W", () => CloseTab(_tabStrip.SelectedIndex));
        Item(Strings.Of("tab.reopen"), "Ctrl+Shift+T", ReopenTab);
        menu.Items[^1].Enabled = _closedTabs.Count > 0;
        menu.Separator();

        string? here = Selected?.Address;
        bool kept = here is not null && _bookmarks.Contains(here);
        Item(Strings.Of(kept ? "menu.unbookmark" : "menu.bookmark"), "Ctrl+D", ToggleBookmark);
        menu.Items.Add(Submenu(Strings.Of("menu.bookmarks"), _bookmarks.All.Select(b => (b.Title, b.Url))));
        Item(Strings.Of("menu.history"), "Ctrl+H", () => OpenTab($"{InternalPages.Scheme}://history"));
        Item(Strings.Of("menu.downloads"), "Ctrl+J", () => OpenTab($"{InternalPages.Scheme}://downloads"));
        menu.Separator();
        Item(Strings.Of("menu.find"), "Ctrl+F", OpenFind);
        Item(Strings.Of("window.fullscreen"), "F11", () => SetFullscreen(!_fullscreen));
        Item(Strings.Of("menu.zoomIn"), "Ctrl+Plus", () => Zoom(0.5));
        Item(Strings.Of("menu.zoomOut"), "Ctrl+Minus", () => Zoom(-0.5));
        Item(Strings.Of("menu.zoomReset"), "Ctrl+0", () => Zoom(null));
        menu.Separator();
        Item(Strings.Of("menu.print"), "Ctrl+P", () => Selected?.Print());
        menu.Separator();
        Item(Strings.Of("menu.settings"), string.Empty, () => OpenTab($"{InternalPages.Scheme}://settings"));
        menu.Separator();
        Item($"{Strings.Of("menu.about")} {Branding.Name}", string.Empty, ShowAbout);
        Item(Strings.Of("menu.exit"), string.Empty, Close);

        menu.ShowAt(_toolbar, at);
    }

    /// <summary>A list of pages that open when picked, or a note that there are none.</summary>
    private ToolStripMenuItem Submenu(string name, IEnumerable<(string Title, string Url)> pages)
    {
        ToolStripMenuItem parent = new(name);
        foreach ((string title, string url) in pages)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem(
                string.IsNullOrWhiteSpace(title) ? url : title,
                null,
                (_, _) => OpenTab(url)));
        }

        if (parent.DropDownItems.Count == 0)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem(Strings.Of("menu.empty")) { Enabled = false });
        }

        parent.DropDown.Renderer = new MenuRenderer();
        parent.DropDown.BackColor = Theme.Current.Surface;
        parent.DropDown.ForeColor = Theme.Current.Text;
        return parent;
    }

    internal static void RevealFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true,
        });
    }

    private void ShowAbout()
    {
        using AboutForm about = new(_profile);
        about.ShowDialog(this);
    }

    private void OnUiThread(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try { BeginInvoke(() => { if (!IsDisposed && !Disposing) action(); }); }
        catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated) { }
    }

    // ----------------------------------------------------------------- layout

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // One line where the chrome ends. Without it the toolbar and a dark page
        // merge into each other and the window looks like it is missing a piece.
        using Pen pen = new(Theme.Current.Line);
        int y = _pages.Top - 1;
        e.Graphics.DrawLine(pen, 0, y, ClientSize.Width, y);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_settings is null) return; // Form can lay out during base construction.

        _tabStrip.Visible = _captionButtons.Visible = _toolbar.Visible = !_fullscreen;
        if (_fullscreen)
        {
            _bookmarksBar.Visible = false;
            _pages.Bounds = ClientRectangle;
            _exitFullscreen.Location = new Point(Math.Max(0, ClientSize.Width - _exitFullscreen.Width - Dip(12)), Dip(8));
            return;
        }

        // A maximised window sits a border's width outside the screen on every
        // side, so the content has to come in by that much or its top row is cut
        // off. Otherwise the sliver above the tabs is what the top edge is
        // grabbed by.
        int top = WindowState == FormWindowState.Maximized
            ? Win32.ResizeBorder(UiScale)
            : Dip(TopResizeStrip);

        int caption = Dip(CaptionHeight);
        int toolbar = Dip(ToolbarHeight);
        int buttons = _captionButtons.PreferredWidth;

        _captionButtons.SetBounds(ClientSize.Width - buttons, top, buttons, caption);
        _captionButtons.IsMaximised = WindowState == FormWindowState.Maximized;
        _tabStrip.SetBounds(0, top, Math.Max(0, ClientSize.Width - buttons), caption);
        _toolbar.SetBounds(0, top + caption, ClientSize.Width, toolbar);

        // The bar is only there when there is something on it; an empty strip of
        // chrome above every page is the sort of thing browsers used to do.
        bool bar = _settings.Current.ShowBookmarksBar && _bookmarks.All.Count > 0;
        int barHeight = bar ? Dip(BookmarksBarHeight) : 0;
        _bookmarksBar.Visible = bar;
        _bookmarksBar.SetBounds(0, top + caption + toolbar, ClientSize.Width, barHeight);

        int findHeight = _findBar.Visible ? Dip(FindBarHeight) : 0;
        _findBar.SetBounds(0, top + caption + toolbar + barHeight, ClientSize.Width, findHeight);

        int pageTop = top + caption + toolbar + barHeight + findHeight;
        _pages.SetBounds(0, pageTop, ClientSize.Width, Math.Max(0, ClientSize.Height - pageTop));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyWindowStyling();
    }

    private void ApplyWindowStyling()
    {
        // Ask for the rounded corners and the dark frame of a modern window. Both
        // are ignored on Windows 10, which simply leaves square corners.
        int round = Win32.DWMWCP_ROUND;
        Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        int dark = Theme.IsDark ? 1 : 0;
        Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        Strings.Use(_settings.Current.Language);
        Theme.Choose(_settings.Current.Theme);
        _findBar.ApplyTheme(); // the box's own prompt is one of the translated phrases
        RefreshBookmarks();
        ReloadInternalPages();
    }

    /// <summary>
    /// The browser's own pages are drawn in the colours and the language in
    /// force when they were asked for, so a change to either leaves any that are
    /// open out of date until they are fetched again.
    /// </summary>
    private void ReloadInternalPages()
    {
        foreach (ChromiumWebBrowser browser in _browsers)
        {
            if (browser.Address?.StartsWith($"{InternalPages.Scheme}://", StringComparison.OrdinalIgnoreCase) == true)
            {
                browser.Reload();
            }
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        BackColor = Theme.Current.Chrome;
        _toolbar.ApplyTheme();
        _findBar.ApplyTheme();
        ApplyWindowStyling();
        Invalidate(true);
        ReloadInternalPages();
    }

    private void ToggleMaximise() =>
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;

    // --------------------------------------------------------- window messages

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case Win32.WM_NCCALCSIZE when m.WParam != IntPtr.Zero && !_fullscreen:
                OnNonClientCalcSize(ref m);
                return;

            case Win32.WM_NCHITTEST:
                base.WndProc(ref m);
                AdjustHitTest(ref m);
                return;

            case 0x00A0: // WM_NCMOUSEMOVE
                _captionButtons.SetNativeHover((int)m.WParam == Win32.HTMAXBUTTON);
                break;
            case 0x02A2: // WM_NCMOUSELEAVE
                _captionButtons.SetNativeHover(false);
                break;

            case Win32.WM_SETTINGCHANGE:
            case Win32.WM_DWMCOLORIZATIONCOLORCHANGED:
                Theme.Refresh();
                break;
        }

        if (!_fullscreen && Win32.DwmDefWindowProc(Handle, m.Msg, m.WParam, m.LParam, out IntPtr result))
        {
            m.Result = result;
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>
    /// Takes the caption band back as client area while leaving the frame alone.
    ///
    /// Windows is asked first what the client rectangle would normally be, and
    /// only its top is moved back up to the window's own edge. The sides and the
    /// bottom stay where Windows put them, so those edges are still real frame
    /// and still resize without this window doing anything about it.
    /// </summary>
    private void OnNonClientCalcSize(ref Message m)
    {
        Win32.NcCalcSizeParams before = Marshal.PtrToStructure<Win32.NcCalcSizeParams>(m.LParam);
        int windowTop = before.Proposed.Top;

        base.WndProc(ref m);

        Win32.NcCalcSizeParams after = Marshal.PtrToStructure<Win32.NcCalcSizeParams>(m.LParam);
        after.Proposed.Top = windowTop;
        Marshal.StructureToPtr(after, m.LParam, false);
        m.Result = IntPtr.Zero;
    }

    /// <summary>
    /// The top edge is client area now, so Windows reports it as the inside of
    /// the window. Only the sliver above the tabs is turned back into a resize
    /// edge; everything below it belongs to the page.
    /// </summary>
    private void AdjustHitTest(ref Message m)
    {
        if (_fullscreen || m.Result != Win32.HTCLIENT)
        {
            return;
        }

        Point screen = new(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16)));
        Point client = PointToClient(screen);

        if (_captionButtons.MaximiseBounds.Contains(_captionButtons.PointToClient(screen)))
        {
            m.Result = Win32.HTMAXBUTTON;
            return;
        }
        if (_tabStrip.DragArea.Contains(_tabStrip.PointToClient(screen)))
        {
            m.Result = Win32.HTCAPTION;
            return;
        }
        if (WindowState == FormWindowState.Maximized) return;

        if (client.Y >= Dip(TopResizeStrip))
        {
            return;
        }

        int corner = Dip(12);
        m.Result = client.X < corner
            ? Win32.HTTOPLEFT
            : client.X > ClientSize.Width - corner
                ? Win32.HTTOPRIGHT
                : Win32.HTTOP;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Theme.Changed -= OnThemeChanged;
        _settings.Changed -= OnSettingsChanged;
        _bookmarks.Changed -= OnBookmarksChanged;
        base.OnFormClosed(e);

        _menu.Reset();
        _menu.Dispose();

        foreach (ChromiumWebBrowser browser in _browsers)
        {
            if (_audio.Remove(browser, out AudioWatcher? audio)) audio.Dispose();
            (browser.DownloadHandler as IDisposable)?.Dispose();
            browser.Dispose();
        }

        // A private window's cookies and cache were only ever in memory; letting
        // go of the context is what throws them away.
        _context?.Dispose();
    }

    /// <summary>Where this window's data lives; shown in the about box later.</summary>
    public ProfileLocation Profile => _profile;
}
