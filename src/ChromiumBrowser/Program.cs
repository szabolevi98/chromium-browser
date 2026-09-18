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

        ProfileLocation profile = ProfileLocator.Resolve(
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProfileLocator.CanWrite);

        Directory.CreateDirectory(profile.Path);

        // The stores belong to the profile rather than to a window, so several
        // windows would share one history rather than fight over the file.
        BookmarkStore bookmarks = new(Path.Combine(profile.Path, "bookmarks.json"));
        HistoryStore history = new(Path.Combine(profile.Path, "history.json"));
        DownloadStore downloads = new(Path.Combine(profile.Path, "downloads.json"));
        SettingsStore settings = new(Path.Combine(profile.Path, "settings.json"));

        Strings.Use(settings.Current.Language);

        // The theme choice has to be in force before the first window is drawn,
        // or it would come up in the system's colours and change under the user.
        Theme.Choose(settings.Current.Theme);

        BrowserWindow? window = null;
        InternalPages pages = new(history, downloads, settings, bookmarks, BrowserWindow.RevealFile);

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

            // The window does not exist yet: the engine wants its schemes before
            // it starts, so the factory is given a way to find it later.
            SchemeHandlerFactory = new InternalSchemeFactory(() => window, pages),
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

        window = new BrowserWindow(profile, bookmarks, history, downloads, settings, args.FirstOrDefault());
        using (window)
        {
            Application.Run(window);
        }

        return 0;
    }
}
