using Microsoft.Win32;

namespace ChromiumBrowser.Ui;

/// <summary>Every colour the window draws with.</summary>
public sealed record Palette
{
    /// <summary>Behind the tabs and the toolbar.</summary>
    public required Color Chrome { get; init; }

    /// <summary>The card the current tab is drawn as, and the page behind it.</summary>
    public required Color Surface { get; init; }

    /// <summary>A tab the pointer is over but which is not the current one.</summary>
    public required Color Hover { get; init; }

    /// <summary>Pressed, or a control being held.</summary>
    public required Color Pressed { get; init; }

    public required Color Text { get; init; }

    /// <summary>Titles of tabs that are not the current one, and secondary labels.</summary>
    public required Color TextMuted { get; init; }

    /// <summary>Hairlines: between the toolbar and the page, around the address bar.</summary>
    public required Color Line { get; init; }

    /// <summary>The user's own accent colour, used sparingly.</summary>
    public required Color Accent { get; init; }

    /// <summary>The close button's own hover colour, which is red everywhere.</summary>
    public Color CloseHover { get; init; } = Color.FromArgb(232, 17, 35);
}

/// <summary>
/// Light and dark, and which of the two Windows is currently asking for.
///
/// The browser follows the system rather than carrying a theme of its own,
/// because a window that stays white when everything around it has gone dark is
/// the thing people notice first. The accent colour is read from the same place
/// Windows keeps it, so the browser matches the rest of the desktop.
/// </summary>
public static class Theme
{
    public static Palette Dark { get; } = new()
    {
        Chrome = Color.FromArgb(32, 33, 36),
        Surface = Color.FromArgb(48, 49, 52),
        Hover = Color.FromArgb(42, 43, 46),
        Pressed = Color.FromArgb(58, 59, 62),
        Text = Color.FromArgb(232, 234, 237),
        TextMuted = Color.FromArgb(154, 160, 166),
        Line = Color.FromArgb(60, 62, 66),
        Accent = Color.FromArgb(138, 180, 248),
    };

    public static Palette Light { get; } = new()
    {
        Chrome = Color.FromArgb(222, 225, 230),
        Surface = Color.FromArgb(255, 255, 255),
        Hover = Color.FromArgb(236, 238, 241),
        Pressed = Color.FromArgb(210, 213, 218),
        Text = Color.FromArgb(32, 33, 36),
        TextMuted = Color.FromArgb(95, 99, 104),
        Line = Color.FromArgb(200, 203, 208),
        Accent = Color.FromArgb(26, 115, 232),
    };

    /// <summary>The palette in force, refreshed whenever Windows says it changed.</summary>
    public static Palette Current { get; private set; } = Build();

    /// <summary>Raised after <see cref="Current"/> has changed.</summary>
    public static event EventHandler? Changed;

    /// <summary>Whether Windows is currently in its dark mode.</summary>
    public static bool IsDark { get; private set; } = ReadIsDark();

    /// <summary>Re-reads the system setting; called when a window is told the theme changed.</summary>
    public static void Refresh()
    {
        bool dark = ReadIsDark();
        Palette palette = Build();
        if (dark == IsDark && palette == Current)
        {
            return;
        }

        IsDark = dark;
        Current = palette;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static Palette Build()
    {
        Palette palette = ReadIsDark() ? Dark : Light;
        Color? accent = ReadAccent();
        return accent is null ? palette : palette with { Accent = accent.Value };
    }

    private static bool ReadIsDark()
    {
        // Zero means dark; the value is missing on editions that never had the
        // setting, and those are all light.
        object? value = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1);

        return value is int light && light == 0;
    }

    private static Color? ReadAccent()
    {
        // Packed ARGB, where the alpha is the desktop's own blend amount rather
        // than anything this window should apply, so only the three colour
        // channels are taken.
        object? value = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "ColorizationColor", null);

        if (value is not int packed)
        {
            return null;
        }

        Color colour = Color.FromArgb(packed);
        return Color.FromArgb(colour.R, colour.G, colour.B);
    }
}
