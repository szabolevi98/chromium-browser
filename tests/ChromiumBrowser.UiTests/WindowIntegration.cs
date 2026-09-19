using CefSharp;
using CefSharp.WinForms;
using ChromiumBrowser.Browser;
using ChromiumBrowser.Controls;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Profile;
using ChromiumBrowser.Native;

namespace ChromiumBrowser.UiTests;

internal static partial class Program
{
    private static bool PumpUntil(Func<bool> ready, int milliseconds = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!ready() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Application.DoEvents();
        return ready();
    }

    private static IntPtr Pack(Point point) => (IntPtr)((point.Y << 16) | (point.X & 0xffff));

    private static object? Evaluate(ChromiumWebBrowser browser, string script)
    {
        using IFrame frame = browser.GetMainFrame();
        Task<JavascriptResponse> result = frame.EvaluateScriptAsync(script);
        if (!PumpUntil(() => result.IsCompleted)) return null;
        return result.Result.Success ? result.Result.Result : null;
    }

    // Opt-in because these checks start Chromium and native Windows handles.
    // All windows are transparent and all data is in a new temporary profile.
    private static void CheckWindowIntegration()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cb-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        using UiDispatcher dispatcher = new();
        BookmarkStore bookmarks = new(string.Empty);
        HistoryStore history = new(string.Empty);
        DownloadStore downloads = new(string.Empty);
        SettingsStore settings = new(string.Empty);
        settings.Save(settings.Current with { HomePage = "browser://private" });
        InternalPages pages = new(history, downloads, settings, bookmarks, new SessionStore(string.Empty), _ => { });
        InternalSchemeFactory factory = new(() => dispatcher, pages);
        CefSettings cefSettings = new()
        {
            RootCachePath = Path.Combine(root, "Cef"),
            CachePath = Path.Combine(root, "Cef", "Cache"),
            LogSeverity = LogSeverity.Disable,
        };
        cefSettings.CefCommandLineArgs.Add("disable-background-networking");
        cefSettings.CefCommandLineArgs.Add("disable-background-timer-throttling");
        cefSettings.CefCommandLineArgs.Add("autoplay-policy", "no-user-gesture-required");
        cefSettings.CefCommandLineArgs.Add("disable-backgrounding-occluded-windows");
        cefSettings.CefCommandLineArgs.Add("disable-features", "CalculateNativeWinOcclusion");
        cefSettings.RegisterScheme(new CefCustomScheme
        {
            SchemeName = "browser", IsStandard = true, IsSecure = true,
            IsDisplayIsolated = true, IsFetchEnabled = true, SchemeHandlerFactory = factory,
        });
        bool initialized = Cef.Initialize(cefSettings);
        Check("native: Chromium initializes with isolated data", initialized);
        if (!initialized) return;

        BrowserWindow? window = null;
        BrowserWindow? secret = null;
        try
        {
            window = new(new ProfileLocation(root, true), bookmarks, history, downloads, settings,
                "browser://private", false, (_, _) => { }, factory)
            { Opacity = 0, ShowInTaskbar = false };
            window.Show();
            List<ChromiumWebBrowser> browsers = Field<List<ChromiumWebBrowser>>(window, "_browsers");
            Check("native: browser loads an internal page", PumpUntil(() => browsers[0].IsBrowserInitialized
                && Equals(Evaluate(browsers[0], "document.location.host"), "private")));
            TabStripControl strip = Field<TabStripControl>(window, "_tabStrip");
            CaptionButtons caption = Field<CaptionButtons>(window, "_captionButtons");
            Point drag = strip.PointToScreen(new Point(strip.DragArea.Left + 12, 20));
            Check("native: child yields the caption hit test", Win32.SendMessageW(strip.Handle, 0x84, IntPtr.Zero, Pack(drag)) == -1);
            Check("native: window identifies caption", Win32.SendMessageW(window.Handle, 0x84, IntPtr.Zero, Pack(drag)) == 2);
            Point max = caption.PointToScreen(new Point(caption.MaximiseBounds.Left + 12, 20));
            Check("native: maximize child yields hit test", Win32.SendMessageW(caption.Handle, 0x84, IntPtr.Zero, Pack(max)) == -1);
            Check("native: window identifies Snap target", Win32.SendMessageW(window.Handle, 0x84, IntPtr.Zero, Pack(max)) == 9);
            Win32.SendMessageW(window.Handle, 0xA3, (IntPtr)2, Pack(drag)); // WM_NCLBUTTONDBLCLK
            Check("native: real nonclient double click maximizes", window.WindowState == FormWindowState.Maximized);
            drag = strip.PointToScreen(new Point(strip.DragArea.Left + 12, 20));
            Check("native: maximized caption is still draggable", Win32.SendMessageW(window.Handle, 0x84, IntPtr.Zero, Pack(drag)) == 2);
            Win32.SendMessageW(window.Handle, 0xA3, (IntPtr)2, Pack(drag));
            Check("native: second double click restores", window.WindowState == FormWindowState.Normal);

            max = caption.PointToScreen(new Point(caption.MaximiseBounds.Left + 12, 20));
            Win32.SendMessageW(window.Handle, 0xA0, (IntPtr)9, Pack(max));
            Check("native: maximize hover does not change window state", window.WindowState == FormWindowState.Normal);
            Win32.SendMessageW(window.Handle, 0xA1, (IntPtr)9, Pack(max));
            Check("native: maximize press captures without opening a second button", window.Capture
                && window.WindowState == FormWindowState.Normal);
            Win32.SendMessageW(window.Handle, 0x0202, IntPtr.Zero, Pack(window.PointToClient(max)));
            Check("native: one maximize click immediately maximizes", window.WindowState == FormWindowState.Maximized
                && !window.Capture);
            max = caption.PointToScreen(new Point(caption.MaximiseBounds.Left + 12, 20));
            Win32.SendMessageW(window.Handle, 0xA1, (IntPtr)9, Pack(max));
            Win32.SendMessageW(window.Handle, 0xA2, (IntPtr)9, Pack(max));
            Check("native: one restore click immediately restores", window.WindowState == FormWindowState.Normal);
            max = caption.PointToScreen(new Point(caption.MaximiseBounds.Left + 12, 20));
            Win32.SendMessageW(window.Handle, 0xA1, (IntPtr)9, Pack(max));
            Win32.SendMessageW(window.Handle, 0x0202, IntPtr.Zero, Pack(new Point(20, 100)));
            Check("native: releasing outside maximize cancels the click", window.WindowState == FormWindowState.Normal
                && !window.Capture);
            Win32.SendMessageW(window.Handle, 0xA1, (IntPtr)9, Pack(max));
            Win32.SendMessageW(window.Handle, 0x001F, IntPtr.Zero, IntPtr.Zero);
            Check("native: cancelling maximize tracking releases capture", !window.Capture
                && !Field<bool>(window, "_maximisePressed") && window.WindowState == FormWindowState.Normal);
            Win32.SendMessageW(window.Handle, 0xA1, (IntPtr)9, Pack(max));
            window.Capture = false;
            Check("native: capture loss cancels maximize tracking", !Field<bool>(window, "_maximisePressed")
                && window.WindowState == FormWindowState.Normal);
            Check("native: Snap hover target survives custom click handling", Win32.SendMessageW(window.Handle, 0x84, IntPtr.Zero, Pack(max)) == 9);

            Invoke(window, "Run", BrowserCommand.FocusAddress);
            ToolbarControl toolbar = Field<ToolbarControl>(window, "_toolbar");
            Check("native: Ctrl+L takes focus from Chromium", toolbar.AddressHasFocus);
            toolbar.Controls.OfType<TextBox>().Single().Text = "unfinished";
            toolbar.ShowAddress("browser://history");
            Check("native: redirect does not overwrite editing", toolbar.Controls.OfType<TextBox>().Single().Text == "unfinished");
            Invoke(window, "Run", BrowserCommand.Escape);
            Check("native: Escape restores most recent address", toolbar.Controls.OfType<TextBox>().Single().Text == "browser://history"
                && !toolbar.AddressHasFocus);

            Invoke(window, "OpenTab", "browser://history", null, false);
            PumpUntil(() => browsers[^1].IsBrowserInitialized && !browsers[^1].IsLoading);
            strip.Tabs[0].IsLoading = true;
            strip.SelectedIndex = 0;
            bool loading = toolbar.IsLoading;
            strip.Tabs[1].IsLoading = false;
            strip.SelectedIndex = 1;
            Check("native: switching tabs synchronizes stop/reload", loading && !toolbar.IsLoading);
            Invoke(window, "CloseTab", 1);
            Invoke(window, "Run", BrowserCommand.ReopenTab);
            Check("native: reopen restores the closed URL", PumpUntil(() => browsers.Count == 2 && browsers[1].Address == "browser://history/")
                || (browsers.Count == 2 && browsers[1].Address.TrimEnd('/') == "browser://history"));
            int changes = 0;
            window.ContentsChanged += (_, _) => changes++;
            strip.SelectedIndex = 0;
            Check("native: selection requests session save", changes > 0);
            Invoke(window, "ToggleMute", 0);
            Check("native: tab mute updates UI", strip.Tabs[0].IsMuted);
            Check("native: audio is not diverted into capture", browsers[0].AudioHandler is null);
            Evaluate(browsers[0], "window.auditAudio = new AudioContext(); window.auditAudio.resume(); 'ready'");
            Check("native: WebAudio activity lights the tab", PumpUntil(() => strip.Tabs[0].IsAudible));
            Evaluate(browsers[0], "window.auditAudio.close(); 'closed'");
            Check("native: stopped audio clears the indicator", PumpUntil(() => !strip.Tabs[0].IsAudible));

            Rectangle normalBounds = window.Bounds;
            Invoke(window, "Run", BrowserCommand.Fullscreen);
            Check("native: F11 hides chrome and fills monitor", !strip.Visible && window.FormBorderStyle == FormBorderStyle.None
                && window.Bounds == Screen.FromControl(window).Bounds);
            SavedWindow snapshot = window.Snapshot();
            Check("native: full screen does not pollute saved bounds", snapshot.Width == normalBounds.Width && snapshot.Height == normalBounds.Height);
            Invoke(window, "Run", BrowserCommand.Escape);
            Check("native: Escape leaves full screen", strip.Visible && window.FormBorderStyle == FormBorderStyle.Sizable
                && window.Bounds == normalBounds);

            secret = new(new ProfileLocation(root, true), bookmarks, history, downloads, settings,
                "browser://downloads", true, (_, _) => { }, factory)
            { Opacity = 0, ShowInTaskbar = false };
            secret.Show();
            var secretBrowsers = Field<List<ChromiumWebBrowser>>(secret, "_browsers");
            Check("native: private downloads page loads", PumpUntil(() => secretBrowsers[0].IsBrowserInitialized
                && Equals(Evaluate(secretBrowsers[0], "document.location.host"), "downloads")));
            DownloadStore privateDownloads = Field<DownloadStore>(secret, "_downloads");
            downloads.Begin(10, "https://normal.test", "normal-only", 100);
            privateDownloads.Begin(11, "https://secret.test", "private-only", 100);
            bool live = PumpUntil(() => Equals(Evaluate(secretBrowsers[0], "document.body.innerText.includes('private-only')"), true), 6000);
            Check("native: live downloads appear without navigation", live);
            Check("native: private factory hides normal downloads", Equals(Evaluate(secretBrowsers[0], "!document.body.innerText.includes('normal-only')"), true));
            privateDownloads.Progressed(11, 75, 100, "");
            Check("native: live download percentage updates", PumpUntil(() => Equals(Evaluate(secretBrowsers[0], "document.body.innerText.includes('75')"), true), 5000));
            bookmarks.Add("browser://history", "Shared bookmark");
            Check("native: bookmark change reaches both windows", Field<BookmarksBarControl>(window, "_bookmarksBar").Visible
                && Field<BookmarksBarControl>(secret, "_bookmarksBar").Visible);
        }
        finally
        {
            secret?.Close();
            window?.Close();
            secret?.Dispose();
            window?.Dispose();
            Application.DoEvents();
            Cef.Shutdown();
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { Console.WriteLine($"Temporary Chromium files remain at {root}"); }
        }
    }
}
