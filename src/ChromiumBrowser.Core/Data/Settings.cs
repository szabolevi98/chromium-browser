namespace ChromiumBrowser.Core.Data;

/// <summary>Whether to follow Windows, or to insist.</summary>
public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>Everything the browser lets somebody decide.</summary>
public sealed record Settings
{
    public string HomePage { get; init; } = "https://www.google.com/";

    /// <summary>The name of one of the built-in engines, or <c>Custom</c>.</summary>
    public string SearchEngine { get; init; } = SearchEngines.Default.Name;

    /// <summary>Used when the engine is <c>Custom</c>; the search goes where <c>{0}</c> is.</summary>
    public string CustomSearchTemplate { get; init; } = string.Empty;

    public ThemeChoice Theme { get; init; } = ThemeChoice.System;

    public bool ShowBookmarksBar { get; init; } = true;

    /// <summary>Two letters: <c>en</c> or <c>hu</c>.</summary>
    public string Language { get; init; } = "en";

    /// <summary>Where a search for this text goes.</summary>
    public string SearchTemplate =>
        string.Equals(SearchEngine, "Custom", StringComparison.OrdinalIgnoreCase)
        && CustomSearchTemplate.Contains("{0}", StringComparison.Ordinal)
            ? CustomSearchTemplate
            : SearchEngines.ByName(SearchEngine).Template;
}

/// <summary>A place searches are sent to.</summary>
/// <param name="Name">What it is called in the settings.</param>
/// <param name="Template">Its address, with <c>{0}</c> where the search goes.</param>
public readonly record struct SearchEngine(string Name, string Template);

/// <summary>
/// The engines offered, and the one used when nothing has been chosen.
///
/// They are a short list rather than a setting only, because typing a search
/// address by hand with the escaping right is not something anybody should have
/// to do to change where their searches go.
/// </summary>
public static class SearchEngines
{
    public static IReadOnlyList<SearchEngine> All { get; } =
    [
        new("Google", "https://www.google.com/search?q={0}"),
        new("DuckDuckGo", "https://duckduckgo.com/?q={0}"),
        new("Bing", "https://www.bing.com/search?q={0}"),
        new("Startpage", "https://www.startpage.com/sp/search?query={0}"),
        new("Wikipedia", "https://en.wikipedia.org/w/index.php?search={0}"),
    ];

    public static SearchEngine Default => All[0];

    public static SearchEngine ByName(string name) =>
        All.FirstOrDefault(
            engine => string.Equals(engine.Name, name, StringComparison.OrdinalIgnoreCase),
            Default);
}

/// <summary>The settings, kept in the profile beside everything else.</summary>
public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(string path)
    {
        _path = path;

        // One object rather than a list, but the same file handling: a damaged
        // settings file gives the defaults instead of stopping the browser.
        Current = JsonStore.Load<Settings>(path).FirstOrDefault() ?? new Settings();
    }

    public Settings Current { get; private set; }

    /// <summary>Raised after something changed, so the window can follow it.</summary>
    public event EventHandler? Changed;

    public void Save(Settings settings)
    {
        Current = settings;
        JsonStore.Save(_path, new[] { settings });
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
