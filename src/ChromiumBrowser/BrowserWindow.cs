using System.Runtime.InteropServices;
using CefSharp;
using CefSharp.WinForms;
using ChromiumBrowser.Browser;
using ChromiumBrowser.Controls;
using ChromiumBrowser.Core;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Profile;
using ChromiumBrowser.Native;
using ChromiumBrowser.Ui;

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
    private readonly Panel _pages = new() { Dock = DockStyle.None };
    private readonly List<ChromiumWebBrowser> _browsers = [];

    /// <summary>The zoom each page is at, which the engine does not hand back synchronously.</summary>
    private readonly Dictionary<ChromiumWebBrowser, double> _zoom = [];

    private readonly BookmarkStore _bookmarks;
    private readonly HistoryStore _history;
    private readonly DownloadStore _downloads;

    public BrowserWindow(
        ProfileLocation profile,
        BookmarkStore bookmarks,
        HistoryStore history,
        DownloadStore downloads,
        string? startUrl)
    {
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
        _toolbar.Navigated += (_, typed) => Selected?.LoadUrl(AsUrl(typed));
        _toolbar.MenuRequested += (_, at) => ShowMenu(_toolbar.PointToScreen(at));

        Controls.Add(_pages);
        Controls.Add(_toolbar);
        Controls.Add(_tabStrip);
        Controls.Add(_captionButtons);

        Theme.Changed += OnThemeChanged;

        OpenTab(startUrl ?? HomePage);
    }

    /// <summary>Until there are settings to read it from.</summary>
    private static string HomePage => "https://www.google.com/";

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

            _tabStrip.Tabs[at].Title = string.IsNullOrWhiteSpace(e.Title) ? "New tab" : e.Title;
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
        _tabStrip.AddTab(new TabItem { Title = "New tab", IsLoading = true });
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

        Item("New tab", "Ctrl+T", () => OpenTab(HomePage));
        Item("Close tab", "Ctrl+W", () => CloseTab(_tabStrip.SelectedIndex));
        menu.Items.Add(new ToolStripSeparator());

        string? here = Selected?.Address;
        bool kept = here is not null && _bookmarks.Contains(here);
        Item(kept ? "Remove bookmark" : "Bookmark this page", "Ctrl+D", ToggleBookmark);
        menu.Items.Add(Submenu("Bookmarks", _bookmarks.All.Select(b => (b.Title, b.Url))));
        Item("History", "Ctrl+H", () => OpenTab($"{InternalPages.Scheme}://history"));
        Item("Downloads", "Ctrl+J", () => OpenTab($"{InternalPages.Scheme}://downloads"));
        menu.Items.Add(new ToolStripSeparator());
        Item("Zoom in", "Ctrl+Plus", () => Zoom(0.5));
        Item("Zoom out", "Ctrl+Minus", () => Zoom(-0.5));
        Item("Reset zoom", "Ctrl+0", () => Zoom(null));
        menu.Items.Add(new ToolStripSeparator());
        Item("Print...", "Ctrl+P", () => Selected?.Print());
        menu.Items.Add(new ToolStripSeparator());
        Item($"About {Branding.Name}", string.Empty, ShowAbout);
        Item("Exit", string.Empty, Close);

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
            parent.DropDownItems.Add(new ToolStripMenuItem("Nothing yet") { Enabled = false });
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

    /// <summary>
    /// What to do with what was typed. Anything that parses as an address is one;
    /// anything else is a search, which is why a browser's address bar is the
    /// only text field people trust to take either.
    /// </summary>
    private static string AsUrl(string typed)
    {
        if (typed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || typed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || typed.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            || typed.StartsWith($"{InternalPages.Scheme}://", StringComparison.OrdinalIgnoreCase))
        {
            return typed;
        }

        bool looksLikeHost = !typed.Contains(' ')
            && (typed.Contains('.') || typed.StartsWith("localhost", StringComparison.OrdinalIgnoreCase));

        return looksLikeHost
            ? "https://" + typed
            : "https://www.google.com/search?q=" + Uri.EscapeDataString(typed);
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
        _pages.SetBounds(
            0,
            top + caption + toolbar,
            ClientSize.Width,
            Math.Max(0, ClientSize.Height - top - caption - toolbar));
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

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        BackColor = Theme.Current.Chrome;
        _toolbar.ApplyTheme();
        ApplyWindowStyling();
        Invalidate(true);
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
