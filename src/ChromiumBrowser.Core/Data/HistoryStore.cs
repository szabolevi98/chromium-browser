namespace ChromiumBrowser.Core.Data;

/// <summary>A page that has been visited, and how often.</summary>
public sealed record HistoryEntry
{
    public required string Url { get; init; }

    public required string Title { get; init; }

    public DateTimeOffset LastVisit { get; init; }

    public int Visits { get; init; } = 1;
}

/// <summary>
/// Where the browser has been.
///
/// Kept one entry per address rather than one per visit: a list showing the same
/// page forty times is useless for finding anything, and the visit count is what
/// makes a frequently used address rise to the top of the address bar's
/// suggestions later. The newest visit is the one that decides the order.
///
/// The list is capped, because this file is written to a stick and nobody ever
/// looks at the page they visited eleven thousand pages ago. When the cap is
/// reached the oldest go, not the least visited: a page nobody has opened for a
/// year is gone whether it was opened once or fifty times.
/// </summary>
public sealed class HistoryStore
{
    /// <summary>How many addresses are kept before the oldest are dropped.</summary>
    public const int Capacity = 5000;

    private readonly string _path;
    private readonly List<HistoryEntry> _entries;

    public HistoryStore(string path)
    {
        _path = path;
        _entries = JsonStore.Load<HistoryEntry>(path);
        Order();
    }

    /// <summary>Everything, newest first.</summary>
    public IReadOnlyList<HistoryEntry> All => _entries;

    /// <summary>
    /// Notes a visit. The same address visited again moves to the top and its
    /// count goes up; it does not appear twice.
    /// </summary>
    public void Record(string url, string title, DateTimeOffset when)
    {
        if (!IsWorthKeeping(url))
        {
            return;
        }

        int at = IndexOf(url);
        if (at >= 0)
        {
            HistoryEntry existing = _entries[at];
            _entries.RemoveAt(at);
            _entries.Insert(0, existing with
            {
                // A page whose title arrives after the address should not lose
                // the title it had, so an empty one never overwrites.
                Title = string.IsNullOrWhiteSpace(title) ? existing.Title : title,
                LastVisit = when,
                Visits = existing.Visits + 1,
            });
        }
        else
        {
            _entries.Insert(0, new HistoryEntry
            {
                Url = url,
                Title = string.IsNullOrWhiteSpace(title) ? url : title,
                LastVisit = when,
            });
        }

        if (_entries.Count > Capacity)
        {
            _entries.RemoveRange(Capacity, _entries.Count - Capacity);
        }

        Save();
    }

    /// <summary>Addresses and titles containing the text, newest first.</summary>
    public IReadOnlyList<HistoryEntry> Search(string text, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return _entries
            .Where(e => e.Url.Contains(text, StringComparison.OrdinalIgnoreCase)
                || e.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
    }

    /// <summary>Forgets everything visited since a moment, which is what "clear the last hour" means.</summary>
    public void ClearSince(DateTimeOffset since)
    {
        int removed = _entries.RemoveAll(e => e.LastVisit >= since);
        if (removed > 0)
        {
            Save();
        }
    }

    public void ClearAll()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        _entries.Clear();
        Save();
    }

    /// <summary>
    /// Whether an address belongs in history at all. Blank pages and the
    /// browser's own pages do not: nobody wants to find <c>about:blank</c> in a
    /// list of where they have been.
    /// </summary>
    private static bool IsWorthKeeping(string url) =>
        !string.IsNullOrWhiteSpace(url)
        && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private int IndexOf(string url) =>
        _entries.FindIndex(e => string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase));

    private void Order()
    {
        _entries.Sort((left, right) => right.LastVisit.CompareTo(left.LastVisit));
        if (_entries.Count > Capacity)
        {
            _entries.RemoveRange(Capacity, _entries.Count - Capacity);
        }
    }

    private void Save() => JsonStore.Save(_path, _entries);
}
