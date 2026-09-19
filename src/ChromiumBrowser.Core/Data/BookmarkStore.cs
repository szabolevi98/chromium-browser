namespace ChromiumBrowser.Core.Data;

/// <summary>A page somebody wanted to keep.</summary>
public sealed record Bookmark
{
    public required string Url { get; init; }

    public required string Title { get; init; }

    public DateTimeOffset Added { get; init; } = DateTimeOffset.Now;
}

/// <summary>
/// The bookmarks, in the order they are shown.
///
/// A bookmark is identified by its address, because that is what the star in the
/// toolbar asks about: is <em>this page</em> kept? So adding the same address
/// twice updates the title of the one that is already there rather than leaving
/// two entries that cannot be told apart. Order is the list's own, since
/// bookmarks are dragged into the order their owner wants rather than sorted.
/// </summary>
public sealed class BookmarkStore
{
    private readonly string _path;
    private readonly List<Bookmark> _bookmarks;

    public BookmarkStore(string path)
    {
        _path = path;
        _bookmarks = JsonStore.Load<Bookmark>(path);
    }

    public IReadOnlyList<Bookmark> All => _bookmarks;
    public event EventHandler? Changed;

    public bool Contains(string url) => IndexOf(url) >= 0;

    /// <summary>Adds the page, or renames the bookmark that already points at it.</summary>
    public void Add(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        int at = IndexOf(url);
        if (at >= 0)
        {
            _bookmarks[at] = _bookmarks[at] with { Title = title };
        }
        else
        {
            _bookmarks.Add(new Bookmark { Url = url, Title = title });
        }

        Save();
    }

    public void Remove(string url)
    {
        int at = IndexOf(url);
        if (at < 0)
        {
            return;
        }

        _bookmarks.RemoveAt(at);
        Save();
    }

    /// <summary>Adds the page if it is not kept, and removes it if it is.</summary>
    /// <returns>Whether the page is a bookmark afterwards.</returns>
    public bool Toggle(string url, string title)
    {
        if (Contains(url))
        {
            Remove(url);
            return false;
        }

        Add(url, title);
        return true;
    }

    public void Rename(string url, string title) => Add(url, title);

    /// <summary>Moves a bookmark to another position, as dragging it would.</summary>
    public void Move(int from, int to)
    {
        if (from < 0 || from >= _bookmarks.Count || from == to)
        {
            return;
        }

        Bookmark moved = _bookmarks[from];
        _bookmarks.RemoveAt(from);
        _bookmarks.Insert(Math.Clamp(to, 0, _bookmarks.Count), moved);
        Save();
    }

    private int IndexOf(string url) =>
        _bookmarks.FindIndex(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));

    private void Save()
    {
        JsonStore.Save(_path, _bookmarks);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
