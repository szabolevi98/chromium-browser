using CefSharp;
using CefSharp.WinForms;
using ChromiumBrowser.Core;
using ChromiumBrowser.Core.Profile;

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

        CefSettings settings = new()
        {
            // Everything Chromium remembers goes in the profile folder, so a
            // portable copy leaves nothing behind on the machine that ran it.
            RootCachePath = Path.Combine(profile.Path, "Cef"),
            CachePath = Path.Combine(profile.Path, "Cef", "Cache"),
            LogFile = Path.Combine(profile.Path, "Cef", "debug.log"),
            LogSeverity = LogSeverity.Warning,
        };

        if (!Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null))
        {
            MessageBox.Show(
                "The Chromium engine could not start.",
                Branding.Name,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        using MainForm window = new(profile, args.FirstOrDefault());
        Application.Run(window);
        return 0;
    }
}
