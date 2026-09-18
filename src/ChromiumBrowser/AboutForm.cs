using System.Reflection;
using CefSharp;
using ChromiumBrowser.Core;
using ChromiumBrowser.Core.Profile;
using ChromiumBrowser.Ui;
using ChromiumBrowser.Core.Localisation;

namespace ChromiumBrowser;

/// <summary>
/// What this is, what it is built on, and — the part that matters for a portable
/// program — where it is keeping its data. Somebody running a browser from a
/// stick wants to be able to check that it really is writing there.
/// </summary>
public sealed class AboutForm : Form
{
    public AboutForm(ProfileLocation profile)
    {
        Palette palette = Theme.Current;

        Text = $"{Strings.Of("about.title")} {Branding.Name}";
        Icon = BrowserWindow.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = palette.Chrome;
        ForeColor = palette.Text;
        ClientSize = new Size(460, 250);
        Font = new Font("Segoe UI", 9f);

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";

        Label name = Line(Branding.Name, 20, new Font("Segoe UI Semibold", 15f), palette.Text);
        Label tagline = Line(Strings.Of("about.tagline"), 54, Font, palette.TextMuted);
        Label build = Line($"{Strings.Of("about.version")} {version}", 90, Font, palette.Text);
        Label engine = Line($"Chromium {Cef.ChromiumVersion}, CEF {Cef.CefSharpVersion}", 114, Font, palette.TextMuted);
        Label where = Line(Strings.Of(profile.IsPortable ? "about.portable" : "about.installed"), 150, Font, palette.Text);
        Label path = Line(profile.Path, 174, Font, palette.TextMuted);

        Button close = new()
        {
            Text = Strings.Of("button.close"),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = palette.Hover,
            ForeColor = palette.Text,
            Size = new Size(96, 30),
            Location = new Point(ClientSize.Width - 116, ClientSize.Height - 48),
        };
        close.FlatAppearance.BorderColor = palette.Line;

        Controls.AddRange([name, tagline, build, engine, where, path, close]);
        AcceptButton = close;
        CancelButton = close;
    }

    private Label Line(string text, int top, Font font, Color colour) => new()
    {
        Text = text,
        Font = font,
        ForeColor = colour,
        AutoSize = false,
        AutoEllipsis = true,
        Location = new Point(24, top),
        Size = new Size(ClientSize.Width - 48, font.Height + 6),
        BackColor = Color.Transparent,
    };
}
