namespace ChromiumBrowser.Core.Data;

/// <summary>One tab as it was left.</summary>
public sealed record SavedTab
{
    public required string Url { get; init; }

    /// <summary>
    /// What the tab said last time. Kept so the strip can be labelled before the
    /// page has loaded — a restored window full of tabs called "New tab" is a
    /// window nobody can find anything in.
    /// </summary>
    public string Title { get; init; } = string.Empty;
}

/// <summary>One window as it was left: its tabs, and where it sat.</summary>
public sealed record SavedWindow
{
    public List<SavedTab> Tabs { get; init; } = [];

    public int Selected { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public bool Maximised { get; init; }
}

/// <summary>
/// What was open when the browser was last closed.
///
/// Written as things change rather than on the way out, because the run that
/// most needs the list is the one that ended without a way out: a browser that
/// only saves its tabs when it closes cleanly loses them exactly when it
/// mattered.
///
/// Private windows are never in here. That is the one rule this file has, and
/// it is kept by the caller not offering them rather than by this class
/// filtering them, so there is nowhere for a private window to slip through.
/// </summary>
public sealed class SessionStore
{
    private readonly string _path;

    public SessionStore(string path)
    {
        _path = path;
        Windows = JsonStore.Load<SavedWindow>(path);
    }

    /// <summary>The windows that were open, in the order they were opened.</summary>
    public IReadOnlyList<SavedWindow> Windows { get; private set; }

    /// <summary>Whether there is anything worth restoring.</summary>
    public bool HasSomething => Windows.Any(window => window.Tabs.Count > 0);

    public void Save(IReadOnlyList<SavedWindow> windows)
    {
        // A window with nothing in it is not worth writing, and neither is a
        // list of them: that is what the file would hold for the moment between
        // the last window closing and the program ending, and restoring it
        // would open an empty browser.
        List<SavedWindow> worth = [.. windows.Where(window => window.Tabs.Count > 0)];
        if (worth.Count == 0)
        {
            return;
        }

        Windows = worth;
        JsonStore.Save(_path, worth);
    }

    /// <summary>Forgets what was open, for the settings page's "clear" list.</summary>
    public void Clear()
    {
        Windows = [];
        JsonStore.Save(_path, Array.Empty<SavedWindow>());
    }
}
