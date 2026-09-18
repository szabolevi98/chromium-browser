using ChromiumBrowser.Core;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Profile;
using ChromiumBrowser.Core.Ui;
using ChromiumBrowser.Core.Web;

int failures = 0;
int total = 0;

void Check(string name, bool passed, string detail = "")
{
    total++;
    if (passed)
    {
        Console.WriteLine($"PASS  {name}");
        return;
    }

    failures++;
    Console.WriteLine($"FAIL  {name}{(detail.Length > 0 ? $" ({detail})" : string.Empty)}");
}

// ------------------------------------------------------------------ profile

{
    ProfileLocation portable = ProfileLocator.Resolve(
        @"E:\stick\browser", @"C:\Users\someone\AppData\Local", _ => true);
    Check("profile: a writable folder keeps the data beside the program",
        portable.IsPortable && portable.Path == @"E:\stick\browser\Data", portable.Path);

    ProfileLocation installed = ProfileLocator.Resolve(
        @"C:\Program Files\Browser", @"C:\Users\someone\AppData\Local", _ => false);
    Check("profile: a folder it may not write to falls back to the user's own",
        !installed.IsPortable
        && installed.Path == Path.Combine(@"C:\Users\someone\AppData\Local", Branding.FolderName),
        installed.Path);

    string temporary = Path.Combine(Path.GetTempPath(), $"cb-{Guid.NewGuid():N}");
    Check("profile: the writability test answers by actually writing",
        ProfileLocator.CanWrite(temporary));
    Check("profile: and says no for a drive that is not there",
        !ProfileLocator.CanWrite(@"Z:\nowhere\at\all"));
    Directory.Delete(temporary, true);
}

// ----------------------------------------------------------------- tab strip

{
    // One tab has no reason to stretch across the window.
    TabStripBounds single = TabStripLayout.Compute(tabCount: 1, available: 1000, newTabWidth: 36);
    Check("tabs: one tab stops at its full width",
        single.Tabs[0].Width == TabStripLayout.MaxTabWidth && single.NewTabX == TabStripLayout.MaxTabWidth,
        $"got {single.Tabs[0].Width} and {single.NewTabX}");
    Check("tabs: a strip that is not full does not scroll", !single.Scrolls);

    // Enough of them to share the room: they must fill it exactly, so the
    // remainder that does not divide is handed out a pixel at a time.
    TabStripBounds shared = TabStripLayout.Compute(tabCount: 5, available: 1000, newTabWidth: 36);
    int filled = shared.Tabs.Sum(t => t.Width);
    Check("tabs: sharing the strip fills it to the pixel", filled == 964, $"got {filled}");
    Check("tabs: the leftover pixels go to the leftmost tabs",
        shared.Tabs[0].Width == 193 && shared.Tabs[3].Width == 193 && shared.Tabs[4].Width == 192,
        string.Join(',', shared.Tabs.Select(t => t.Width)));
    Check("tabs: every tab starts where the one before it ended",
        shared.Tabs.Zip(shared.Tabs.Skip(1)).All(p => p.First.X + p.First.Width == p.Second.X));
    Check("tabs: the new-tab button follows the last tab",
        shared.NewTabX == shared.Tabs[^1].X + shared.Tabs[^1].Width, $"got {shared.NewTabX}");

    // Too many to show: they stop shrinking and the strip scrolls instead.
    TabStripBounds crowded = TabStripLayout.Compute(tabCount: 30, available: 800, newTabWidth: 36);
    Check("tabs: a tab never shrinks below what a tab needs",
        crowded.Scrolls && crowded.Tabs.All(t => t.Width == TabStripLayout.MinTabWidth));

    int offset = TabStripLayout.ScrollToShow(
        index: 29, tabCount: 30, available: 800, newTabWidth: 36, currentOffset: 0);
    TabStripBounds scrolled = TabStripLayout.Compute(30, 800, 36, offset);
    TabBounds last = scrolled.Tabs[29];
    Check("tabs: scrolling to the last tab brings it fully into view",
        last.X >= 0 && last.X + last.Width == 800 - 36, $"got x {last.X}");
    Check("tabs: a tab already in view does not move the strip",
        TabStripLayout.ScrollToShow(28, 30, 800, 36, offset) == offset);

    Check("tabs: a point picks out the tab under it",
        TabStripLayout.HitTest(shared, 0) == 0
        && TabStripLayout.HitTest(shared, 200) == 1
        && TabStripLayout.HitTest(shared, 963) == 4
        && TabStripLayout.HitTest(shared, 980) == -1);

    // Dragging: a tab changes places once it has passed a neighbour's middle,
    // not when the pointer crosses an edge. Tab middles here are 96, 289, 482,
    // 675 and 868.
    Check("tabs: a tab held still stays where it was",
        TabStripLayout.DropIndex(shared, draggedIndex: 0, draggedCentreX: 96) == 0
        && TabStripLayout.DropIndex(shared, draggedIndex: 2, draggedCentreX: 482) == 2);
    Check("tabs: dragging right past one neighbour moves it by one",
        TabStripLayout.DropIndex(shared, draggedIndex: 0, draggedCentreX: 300) == 1,
        $"got {TabStripLayout.DropIndex(shared, 0, 300)}");
    Check("tabs: dragging left past two neighbours moves it by two",
        TabStripLayout.DropIndex(shared, draggedIndex: 4, draggedCentreX: 300) == 2,
        $"got {TabStripLayout.DropIndex(shared, 4, 300)}");
    Check("tabs: a tab dragged off either end lands at that end",
        TabStripLayout.DropIndex(shared, draggedIndex: 3, draggedCentreX: -200) == 0
        && TabStripLayout.DropIndex(shared, draggedIndex: 1, draggedCentreX: 2000) == 4);
}

// --------------------------------------------------------------------- data

string Scratch()
{
    string directory = Path.Combine(Path.GetTempPath(), $"cb-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    return directory;
}

{
    string directory = Scratch();
    string path = Path.Combine(directory, "things.json");

    JsonStore.Save(path, new[] { "one", "two" });
    Check("store: what is written comes back",
        JsonStore.Load<string>(path) is ["one", "two"]);

    Check("store: a file that is not there is an empty list",
        JsonStore.Load<string>(Path.Combine(directory, "missing.json")).Count == 0);

    File.WriteAllText(path, "{ this is not json");
    Check("store: a damaged file is an empty list rather than a crash",
        JsonStore.Load<string>(path).Count == 0);

    // The save writes beside the file and then replaces it, so an interrupted
    // write cannot leave a half-file behind under the real name.
    JsonStore.Save(path, new[] { "three" });
    Check("store: saving leaves no working file behind",
        !File.Exists(path + ".writing") && JsonStore.Load<string>(path) is ["three"]);

    Directory.Delete(directory, true);
}

{
    string directory = Scratch();
    string path = Path.Combine(directory, "bookmarks.json");

    BookmarkStore bookmarks = new(path);
    bookmarks.Add("https://example.com/", "Example");
    bookmarks.Add("https://nesdev.org/", "NESdev");
    Check("bookmarks: kept in the order they were added",
        bookmarks.All.Select(b => b.Title).ToList() is ["Example", "NESdev"]);

    bookmarks.Add("https://example.com/", "Example Domain");
    Check("bookmarks: the same address twice renames rather than duplicates",
        bookmarks.All.Count == 2 && bookmarks.All[0].Title == "Example Domain");

    Check("bookmarks: the star knows whether this page is kept",
        bookmarks.Contains("https://EXAMPLE.com/") && !bookmarks.Contains("https://other.test/"));

    bookmarks.Move(0, 1);
    Check("bookmarks: dragging one past the other reorders them",
        bookmarks.All.Select(b => b.Title).ToList() is ["NESdev", "Example Domain"]);

    Check("bookmarks: the star adds and removes the same page",
        !bookmarks.Toggle("https://nesdev.org/", "NESdev")
        && bookmarks.All.Count == 1
        && bookmarks.Toggle("https://nesdev.org/", "NESdev"));

    BookmarkStore reopened = new(path);
    Check("bookmarks: they are still there next time",
        reopened.All.Count == 2 && reopened.Contains("https://nesdev.org/"));

    Directory.Delete(directory, true);
}

{
    string directory = Scratch();
    string path = Path.Combine(directory, "history.json");
    DateTimeOffset noon = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    HistoryStore history = new(path);
    history.Record("https://example.com/", "Example", noon);
    history.Record("https://nesdev.org/", "NESdev", noon.AddMinutes(1));
    history.Record("https://example.com/", "Example", noon.AddMinutes(2));

    Check("history: one entry per address, not one per visit",
        history.All.Count == 2);
    Check("history: a page visited again rises to the top and is counted",
        history.All[0].Url == "https://example.com/" && history.All[0].Visits == 2,
        $"got {history.All[0].Url} visited {history.All[0].Visits}");

    history.Record("https://example.com/", string.Empty, noon.AddMinutes(3));
    Check("history: a visit with no title does not erase the title it had",
        history.All[0].Title == "Example");

    history.Record("about:blank", "Nothing", noon.AddMinutes(4));
    history.Record(string.Empty, "Nothing", noon.AddMinutes(5));
    Check("history: blank pages are not somewhere you have been",
        history.All.Count == 2);

    Check("history: searching matches the title and the address, either case",
        history.Search("NESDEV").Count == 1 && history.Search("example.com").Count == 1);

    history.ClearSince(noon.AddMinutes(2));
    Check("history: clearing the last while leaves what came before",
        history.All.Count == 1 && history.All[0].Url == "https://nesdev.org/");

    HistoryStore reopened = new(path);
    Check("history: it is still there next time", reopened.All.Count == 1);
    reopened.ClearAll();
    Check("history: and can be emptied", new HistoryStore(path).All.Count == 0);

    Directory.Delete(directory, true);
}

{
    string directory = Scratch();
    string path = Path.Combine(directory, "downloads.json");

    DownloadStore downloads = new(path);
    downloads.Begin(1, "https://example.com/file.zip", "file.zip", 1000);
    downloads.Progressed(1, 400, 1000, Path.Combine(directory, "file.zip"));

    Check("downloads: progress is known but not yet finished",
        downloads.All[0].ReceivedBytes == 400
        && downloads.All[0].Progress == 0.4
        && downloads.All[0].State == DownloadState.InProgress);

    Check("downloads: a size the server did not give means no progress bar",
        new DownloadRecord { Url = "u", FileName = "f" }.Progress is null);

    downloads.Finish(1, DownloadState.Completed, 1000, Path.Combine(directory, "file.zip"));
    Check("downloads: finishing records where the file went",
        downloads.All[0].State == DownloadState.Completed
        && downloads.All[0].Finished is not null
        && downloads.All[0].Path.EndsWith("file.zip", StringComparison.Ordinal),
        $"state {downloads.All[0].State}, path '{downloads.All[0].Path}'");

    // Something still running when the browser closes did not carry on running.
    downloads.Begin(2, "https://example.com/big.iso", "big.iso", 9000);
    downloads.Progressed(2, 100, 9000, string.Empty);
    JsonStore.Save(path, downloads.All);

    DownloadStore reopened = new(path);
    Check("downloads: one interrupted by closing is not shown as still running",
        reopened.All.Any(d => d.FileName == "big.iso" && d.State == DownloadState.Interrupted),
        string.Join(", ", reopened.All.Select(d => $"{d.FileName}:{d.State}")));

    reopened.Clear();
    Check("downloads: the list can be emptied", new DownloadStore(path).All.Count == 0);

    Directory.Delete(directory, true);
}

// ----------------------------------------------------------- what was typed

{
    string google = SearchEngines.Default.Template;

    Check("typed: an address with a scheme is left alone",
        AddressParser.Parse("https://example.com/a?b=c", google) == "https://example.com/a?b=c");
    Check("typed: the browser's own pages are addresses too",
        AddressParser.Parse("browser://history", google) == "browser://history");

    Check("typed: a bare host name gets a scheme",
        AddressParser.Parse("levente.net", google) == "https://levente.net");
    Check("typed: so does a host with a path",
        AddressParser.Parse("levente.net/hu/portfolio", google) == "https://levente.net/hu/portfolio");
    Check("typed: a local server with a port is an address",
        AddressParser.Parse("localhost:3000", google) == "https://localhost:3000"
        && AddressParser.LooksLikeAddress("127.0.0.1:8080"));

    Check("typed: words with a space in them are a search",
        AddressParser.Parse("nes emulator", google)
        == "https://www.google.com/search?q=nes%20emulator");
    Check("typed: a single word with no dot is a search",
        AddressParser.Parse("wikipedia", google) == "https://www.google.com/search?q=wikipedia");
    Check("typed: a sentence that happens to contain a dot is still a search",
        AddressParser.Parse("what is a .ico file", google).StartsWith(
            "https://www.google.com/search?q=", StringComparison.Ordinal));

    Check("typed: the search goes wherever the settings say",
        AddressParser.Parse("cats", SearchEngines.ByName("DuckDuckGo").Template)
        == "https://duckduckgo.com/?q=cats");
    Check("typed: characters that would break the address are escaped",
        AddressParser.Parse("c# & f#", google) == "https://www.google.com/search?q=c%23%20%26%20f%23");
    Check("typed: nothing typed goes nowhere", AddressParser.Parse("   ", google).Length == 0);
}

// ------------------------------------------------------------------ settings

{
    string directory = Scratch();
    string path = Path.Combine(directory, "settings.json");

    SettingsStore settings = new(path);
    Check("settings: the defaults are a working browser",
        settings.Current.HomePage.Length > 0
        && settings.Current.Theme == ThemeChoice.System
        && settings.Current.SearchTemplate == SearchEngines.Default.Template);

    bool told = false;
    settings.Changed += (_, _) => told = true;
    settings.Save(settings.Current with
    {
        HomePage = "https://levente.net/",
        SearchEngine = "DuckDuckGo",
        Theme = ThemeChoice.Dark,
        ShowBookmarksBar = false,
    });

    Check("settings: saving says so, so the window can follow", told);
    Check("settings: they are still there next time",
        new SettingsStore(path).Current is
        {
            HomePage: "https://levente.net/", SearchEngine: "DuckDuckGo",
            Theme: ThemeChoice.Dark, ShowBookmarksBar: false,
        });

    settings.Save(settings.Current with { SearchEngine = "Custom", CustomSearchTemplate = "https://s.test/?q={0}" });
    Check("settings: a search address of your own is used when it has a place for the words",
        settings.Current.SearchTemplate == "https://s.test/?q={0}");

    settings.Save(settings.Current with { CustomSearchTemplate = "https://s.test/" });
    Check("settings: one with nowhere to put the words falls back rather than searching for nothing",
        settings.Current.SearchTemplate == SearchEngines.Default.Template);

    File.WriteAllText(path, "not settings at all");
    Check("settings: a damaged file gives the defaults rather than stopping the browser",
        new SettingsStore(path).Current.HomePage == new Settings().HomePage);

    Directory.Delete(directory, true);
}

Console.WriteLine();
Console.WriteLine($"{total - failures}/{total} passed");
return failures == 0 ? 0 : 1;
