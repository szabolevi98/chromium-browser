using System.Runtime.InteropServices;
using CefSharp;
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
    private readonly Panel _pages = new() { Dock = DockStyle.None };
    private readonly List<ChromiumWebBrowser> _browsers = [];

    /// <summary>The zoom each page is at, which the engine does not hand back synchronously.</summary>
    private readonly Dictionary<ChromiumWebBrowser, double> _zoom = [];

    private readonly BookmarkStore _bookmarks;

    /// <summary>Icons for the bookmarks bar, guessed from each site's root.</summary>
    private readonly Dictionary<string, Image?> _bookmarkIcons = new(StringComparer.OrdinalIgnoreCase);

    private readonly HistoryStore _history;
    private readonly DownloadStore _downloads;
    private readonly SettingsStore _settings;

    public BrowserWindow(
        ProfileLocation profile,
        BookmarkStore bookmarks,
        HistoryStore history,
        DownloadStore downloads,
        SettingsStore settings,
        string? startUrl)
    {
        _settings = settings;
        _profile = profile;
        _bookmarks = bookmarks;
        _history = history;
        _downloads = downloads;

        Text = Branding.Name;
        MinimumSize = new Size(560, 380);
        Size = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        BackColor = Theme.Current.Chrome;

        _tabStrip.SelectedIndexChanged += (_, _) => ShowSelectedPage();
        _tabStrip.TabCloseRequested += (_, index) => CloseTab(index);
        _tabStrip.NewTabRequested += (_, _) => OpenTab(HomePage);
        _tabStrip.TabMoved += (_, move) => MoveBrowser(move.From, move.To);
        _tabStrip.EmptyAreaPressed += (_, _) => Win32.BeginWindowDrag(Handle);
        _tabStrip.EmptyAreaDoubleClicked += (_, _) => ToggleMaximise();

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
                Selected?.LoadUrl(url);
            }
        };
        _toolbar.MenuRequested += (_, at) => ShowMenu(_toolbar.PointToScreen(at));

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

        Theme.Changed += OnThemeChanged;
        _settings.Changed += OnSettingsChanged;

        OpenTab(startUrl ?? HomePage);
    }

    private string HomePage => _settings.Current.HomePage;

    private ChromiumWebBrowser? Selected =>
        _tabStrip.SelectedIndex >= 0 && _tabStrip.SelectedIndex < _browsers.Count
            ? _browsers[_tabStrip.SelectedIndex]
            : null;

    private float UiScale => DeviceDpi / 96f;

    private int Dip(int value) => (int)(value * UiScale);

    // ------------------------------------------------------------------- tabs

    private void OpenTab(string url)
    {
        ChromiumWebBrowser browser = new(url) { Dock = DockStyle.Fill };

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
                Text = $"{_tabStrip.Tabs[at].Title} - {Branding.Name}";
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
            if (!e.IsLoading)
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
        });

        browser.KeyboardHandler = new ShortcutHandler(shortcut => OnUiThread(() => Run(shortcut)));

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
        _tabStrip.AddTab(new TabItem { Title = Strings.Of("tab.new"), IsLoading = true });
        ShowSelectedPage();
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
        _browsers.RemoveAt(index);
        _zoom.Remove(browser);
        _pages.Controls.Remove(browser);
        browser.Dispose();

        _tabStrip.RemoveTab(index);
        ShowSelectedPage();
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
    }

    private void ShowSelectedPage()
    {
        // A search belongs to the page it was typed against, so moving to
        // another tab ends it rather than carrying a count across.
        CloseFind();

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
        _toolbar.ShowAddress(selected.Address ?? string.Empty);
        _toolbar.CanGoBack = selected.CanGoBack;
        _toolbar.CanGoForward = selected.CanGoForward;
        _toolbar.Invalidate();

        int at = _tabStrip.SelectedIndex;
        if (at >= 0 && at < _tabStrip.Tabs.Count)
        {
            Text = $"{_tabStrip.Tabs[at].Title} - {Branding.Name}";
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
            keyData.HasFlag(Keys.Shift));

        if (shortcut is null)
        {
            return base.ProcessCmdKey(ref message, keyData);
        }

        Run(shortcut.Value);
        return true;
    }

    private void Run(BrowserCommand shortcut)
    {
        switch (shortcut)
        {
            case BrowserCommand.NewTab: OpenTab(HomePage); break;
            case BrowserCommand.CloseTab: CloseTab(_tabStrip.SelectedIndex); break;
            case BrowserCommand.NextTab: StepTab(1); break;
            case BrowserCommand.PreviousTab: StepTab(-1); break;
            case BrowserCommand.FocusAddress: _toolbar.FocusAddress(); break;
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
        ContextMenuStrip menu = new()
        {
            Renderer = new MenuRenderer(),
            BackColor = Theme.Current.Surface,
            ForeColor = Theme.Current.Text,
            ShowImageMargin = false,
            Font = Font,
        };

        foreach (Bookmark bookmark in hidden)
        {
            menu.Items.Add(new ToolStripMenuItem(
                string.IsNullOrWhiteSpace(bookmark.Title) ? bookmark.Url : bookmark.Title,
                null,
                (_, _) => Selected?.LoadUrl(bookmark.Url)));
        }

        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(_bookmarksBar, at);
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

        Selected?.StopFinding(clearSelection: true);
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

    private void ShowMenu(Point screen)
    {
        ContextMenuStrip menu = new()
        {
            Renderer = new MenuRenderer(),
            BackColor = Theme.Current.Surface,
            ForeColor = Theme.Current.Text,
            ShowImageMargin = false,
            Font = Font,
        };

        void Item(string text, string keys, Action action) =>
            menu.Items.Add(new ToolStripMenuItem(text, null, (_, _) => action())
            {
                ShortcutKeyDisplayString = keys,
            });

        Item(Strings.Of("menu.newTab"), "Ctrl+T", () => OpenTab(HomePage));
        Item(Strings.Of("menu.closeTab"), "Ctrl+W", () => CloseTab(_tabStrip.SelectedIndex));
        menu.Items.Add(new ToolStripSeparator());

        string? here = Selected?.Address;
        bool kept = here is not null && _bookmarks.Contains(here);
        Item(Strings.Of(kept ? "menu.unbookmark" : "menu.bookmark"), "Ctrl+D", ToggleBookmark);
        menu.Items.Add(Submenu(Strings.Of("menu.bookmarks"), _bookmarks.All.Select(b => (b.Title, b.Url))));
        Item(Strings.Of("menu.history"), "Ctrl+H", () => OpenTab($"{InternalPages.Scheme}://history"));
        Item(Strings.Of("menu.downloads"), "Ctrl+J", () => OpenTab($"{InternalPages.Scheme}://downloads"));
        menu.Items.Add(new ToolStripSeparator());
        Item(Strings.Of("menu.find"), "Ctrl+F", OpenFind);
        Item(Strings.Of("menu.zoomIn"), "Ctrl+Plus", () => Zoom(0.5));
        Item(Strings.Of("menu.zoomOut"), "Ctrl+Minus", () => Zoom(-0.5));
        Item(Strings.Of("menu.zoomReset"), "Ctrl+0", () => Zoom(null));
        menu.Items.Add(new ToolStripSeparator());
        Item(Strings.Of("menu.print"), "Ctrl+P", () => Selected?.Print());
        menu.Items.Add(new ToolStripSeparator());
        Item(Strings.Of("menu.settings"), string.Empty, () => OpenTab($"{InternalPages.Scheme}://settings"));
        menu.Items.Add(new ToolStripSeparator());
        Item($"{Strings.Of("menu.about")} {Branding.Name}", string.Empty, ShowAbout);
        Item(Strings.Of("menu.exit"), string.Empty, Close);

        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(screen);
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

        BeginInvoke(action);
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
            case Win32.WM_NCCALCSIZE when m.WParam != IntPtr.Zero:
                OnNonClientCalcSize(ref m);
                return;

            case Win32.WM_NCHITTEST:
                base.WndProc(ref m);
                AdjustHitTest(ref m);
                return;

            case Win32.WM_SETTINGCHANGE:
            case Win32.WM_DWMCOLORIZATIONCOLORCHANGED:
                Theme.Refresh();
                break;
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
        if (m.Result != Win32.HTCLIENT || WindowState == FormWindowState.Maximized)
        {
            return;
        }

        Point screen = new(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16)));
        Point client = PointToClient(screen);

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
        base.OnFormClosed(e);
        Cef.Shutdown();
    }

    /// <summary>Where this window's data lives; shown in the about box later.</summary>
    public ProfileLocation Profile => _profile;
}
