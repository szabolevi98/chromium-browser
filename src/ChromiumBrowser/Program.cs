using CefSharp;
using CefSharp.WinForms;
using ChromiumBrowser.Browser;
using ChromiumBrowser.Core;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Profile;
using ChromiumBrowser.Ui;
using ChromiumBrowser.Core.Localisation;

namespace ChromiumBrowser;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // A private window can be asked for on the command line, the way every
        // other browser allows, so a shortcut can open one straight away.
        bool wantsPrivate = args.Any(a => a is "--private" or "-private" or "/private");
        string? startUrl = args.FirstOrDefault(a => !a.StartsWith('-') && !a.StartsWith('/'));

        ProfileLocation profile = ProfileLocator.Resolve(
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProfileLocator.CanWrite);

        Directory.CreateDirectory(profile.Path);

        // Before the engine is started, because starting it is the expensive
        // part and a second launch is only here to hand over an address.
        using Mutex? only = SingleInstance.Claim(profile.Path);
        if (only is null && SingleInstance.SendRequest(profile.Path, new(startUrl, wantsPrivate)))
        {
            return 0;
        }

        // The stores belong to the profile rather than to a window, so several
        // windows would share one history rather than fight over the file.
        BookmarkStore bookmarks = new(Path.Combine(profile.Path, "bookmarks.json"));
        HistoryStore history = new(Path.Combine(profile.Path, "history.json"));
        DownloadStore downloads = new(Path.Combine(profile.Path, "downloads.json"));
        SettingsStore settings = new(Path.Combine(profile.Path, "settings.json"));
        SessionStore session = new(Path.Combine(profile.Path, "session.json"));

        Strings.Use(settings.Current.Language);

        // The theme choice has to be in force before the first window is drawn,
        // or it would come up in the system's colours and change under the user.
        Theme.Choose(settings.Current.Theme);

        using BrowserSession windows = new(profile, bookmarks, history, downloads, settings, session);
        InternalPages pages = new(history, downloads, settings, bookmarks, session, BrowserWindow.RevealFile);
        InternalSchemeFactory internalPages = new(() => windows.Dispatcher, pages);
        windows.InternalPages = internalPages;

        CefSettings cefSettings = new()
        {
            // Everything Chromium remembers goes in the profile folder, so a
            // portable copy leaves nothing behind on the machine that ran it.
            RootCachePath = Path.Combine(profile.Path, "Cef"),
            CachePath = Path.Combine(profile.Path, "Cef", "Cache"),
            LogFile = Path.Combine(profile.Path, "Cef", "debug.log"),
            LogSeverity = LogSeverity.Warning,
        };

        cefSettings.RegisterScheme(new CefCustomScheme
        {
            SchemeName = InternalPages.Scheme,

            // Standard, so that addresses under it have a host and a path the
            // way http does, and so the engine treats each of these pages as a
            // place rather than as an opaque blob.
            IsStandard = true,
            IsSecure = true,
            IsLocal = false,
            IsDisplayIsolated = true,
            IsFetchEnabled = true,

            // The window does not exist yet: the engine wants its schemes before
            // it starts, so the factory is given a way to find it later.
            SchemeHandlerFactory = internalPages,
        });

        if (!Cef.Initialize(cefSettings, performDependencyCheck: true, browserProcessHandler: null))
        {
            MessageBox.Show(
                Strings.Of("error.engine"),
                Branding.Name,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        // What was open comes back first, so an address from the command line
        // opens as another tab in it rather than instead of it.
        bool restored = startUrl is null && !wantsPrivate && windows.Restore();
        BrowserWindow first = restored
            ? windows.Newest!
            : windows.Open(startUrl, wantsPrivate);

        SingleInstance.ListenRequests(profile.Path, windows.Accept);

        Application.Run(windows);

        // After the last window, not after the first: the engine is shared by
        // all of them, and shutting it down while another is still open takes
        // that one's pages with it.
        Cef.Shutdown();
        return 0;
    }
}
