using CefSharp;
using CefSharp.WinForms;
using ChromiumBrowser.Core;
using ChromiumBrowser.Core.Profile;

namespace ChromiumBrowser;

/// <summary>
/// The window. For now it is one browser view and nothing else: the tab strip,
/// the address bar and the rest of the chrome are drawn by this project rather
/// than assembled out of system controls, and they arrive next.
/// </summary>
public sealed class MainForm : Form
{
    private readonly ChromiumWebBrowser _browser;

    public MainForm(ProfileLocation profile, string? startUrl)
    {
        Text = Branding.Name;
        MinimumSize = new Size(640, 480);
        Size = new Size(1280, 800);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(32, 33, 36);

        _browser = new ChromiumWebBrowser(startUrl ?? "https://www.google.com/")
        {
            Dock = DockStyle.Fill,
        };

        _browser.TitleChanged += (_, e) => BeginInvoke(() =>
            Text = string.IsNullOrWhiteSpace(e.Title) ? Branding.Name : $"{e.Title} - {Branding.Name}");

        Controls.Add(_browser);

        Profile = profile;
    }

    /// <summary>Where this window's data lives; shown in the about box later.</summary>
    public ProfileLocation Profile { get; }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        Cef.Shutdown();
    }
}
