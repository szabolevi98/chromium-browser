using System.Reflection;
using ChromiumBrowser.Browser;
using ChromiumBrowser.Controls;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Localisation;
using ChromiumBrowser.Core.Ui;

namespace ChromiumBrowser.UiTests;

/// <summary>
/// Drives the controls the way a pointer would, without showing a window.
///
/// The mouse handlers are protected, as they should be, so the tests reach them
/// the same way Windows does rather than by widening the controls' surface to
/// suit the tests. Nothing here needs a visible window, which is what keeps the
/// checks runnable in one second on any machine.
/// </summary>
internal static class Program
{
    private static int _failures;
    private static int _total;

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        CheckTabStrip();
        CheckCaptionButtons();
        CheckShortcuts();
        CheckInternalPages();
        CheckBookmarksBar();

        Console.WriteLine();
        Console.WriteLine($"{_total - _failures}/{_total} user interface checks passed.");
        return _failures == 0 ? 0 : 1;
    }

    private static void Check(string name, bool passed, string detail = "")
    {
        _total++;
        if (passed)
        {
            Console.WriteLine($"PASS  {name}");
            return;
        }

        _failures++;
        Console.WriteLine($"FAIL  {name}{(detail.Length > 0 ? $" ({detail})" : string.Empty)}");
    }

    private static void Send(Control control, string handler, MouseEventArgs e) =>
        typeof(Control)
            .GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, [e]);

    private static void Click(Control control, int x, int y, MouseButtons button = MouseButtons.Left)
    {
        Send(control, "OnMouseDown", new MouseEventArgs(button, 1, x, y, 0));
        Send(control, "OnMouseUp", new MouseEventArgs(button, 1, x, y, 0));
    }

    private static void CheckTabStrip()
    {
        using TabStripControl strip = new();
        strip.Size = new Size(1120, 40);

        strip.AddTab(new TabItem { Title = "First" });
        strip.AddTab(new TabItem { Title = "Second" });
        strip.AddTab(new TabItem { Title = "Third" });

        Check("strip: adding a tab selects it", strip.SelectedIndex == 2);

        // Where things are: the layout is the same arithmetic the control paints
        // from, with the strip's own left inset added.
        int inset = 6;
        int tabWidth = TabStripLayout.MaxTabWidth;
        int firstTabCentre = inset + (tabWidth / 2);
        int newTabX = inset + (3 * tabWidth) + 4;

        Click(strip, firstTabCentre, 20);
        Check("strip: clicking a tab selects it", strip.SelectedIndex == 0, $"got {strip.SelectedIndex}");

        bool newTabAsked = false;
        strip.NewTabRequested += (_, _) => newTabAsked = true;
        Click(strip, newTabX + 8, 20);
        Check("strip: clicking the plus asks for a new tab", newTabAsked);

        int closed = -1;
        strip.TabCloseRequested += (_, index) => closed = index;
        int closeX = inset + tabWidth - 8 - 8; // the cross sits 8px in from the tab's right edge
        Click(strip, closeX, 20);
        Check("strip: clicking the cross closes that tab", closed == 0, $"got {closed}");

        closed = -1;
        Click(strip, inset + tabWidth + (tabWidth / 2), 20, MouseButtons.Middle);
        Check("strip: the middle button closes a tab without aiming at the cross",
            closed == 1, $"got {closed}");

        bool dragStarted = false;
        strip.EmptyAreaPressed += (_, _) => dragStarted = true;
        Click(strip, 1100, 20);
        Check("strip: the empty part of the strip drags the window", dragStarted);

        // Dragging: press the first tab and move it past the middle of the second.
        // Exactly on that middle is deliberately not enough — a tab changes
        // places once it has passed its neighbour, not once it has reached it —
        // so the drag here goes a few pixels beyond.
        (int From, int To) moved = (-1, -1);
        strip.TabMoved += (_, move) => moved = move;
        int justPast = firstTabCentre + tabWidth + 12;
        Send(strip, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, firstTabCentre, 20, 0));
        Send(strip, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, firstTabCentre + tabWidth, 20, 0));
        Check("strip: a tab resting exactly on its neighbour's middle does not swap",
            moved == (-1, -1), $"got {moved}");
        Send(strip, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, justPast, 20, 0));
        Send(strip, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, justPast, 20, 0));

        Check("strip: dragging a tab past its neighbour swaps them",
            moved == (0, 1) && strip.Tabs[0].Title == "Second" && strip.Tabs[1].Title == "First",
            $"got {moved} and [{string.Join(", ", strip.Tabs.Select(t => t.Title))}]");

        Check("strip: the dragged tab stays the selected one",
            strip.SelectedIndex == 1 && strip.Tabs[strip.SelectedIndex].Title == "First",
            $"got {strip.SelectedIndex}");

        // Removing follows the same rule every browser uses: the tab to the left
        // takes over, so closing several in a row does not jump about.
        strip.SelectedIndex = 2;
        strip.RemoveTab(2);
        Check("strip: closing the last tab selects the one before it",
            strip.Tabs.Count == 2 && strip.SelectedIndex == 1, $"got {strip.SelectedIndex}");
    }

    private static void CheckShortcuts()
    {
        // The page and the window read the same table, so this is the only place
        // the two can be checked against each other.
        Check("keys: Ctrl+T opens a tab",
            ShortcutHandler.Match((int)Keys.T, control: true, shift: false) == BrowserCommand.NewTab);
        Check("keys: Ctrl+W closes one",
            ShortcutHandler.Match((int)Keys.W, control: true, shift: false) == BrowserCommand.CloseTab);
        Check("keys: Ctrl+Tab walks forward and Ctrl+Shift+Tab back",
            ShortcutHandler.Match((int)Keys.Tab, true, false) == BrowserCommand.NextTab
            && ShortcutHandler.Match((int)Keys.Tab, true, true) == BrowserCommand.PreviousTab);
        Check("keys: both plus keys zoom in",
            ShortcutHandler.Match((int)Keys.Add, true, false) == BrowserCommand.ZoomIn
            && ShortcutHandler.Match((int)Keys.Oemplus, true, false) == BrowserCommand.ZoomIn);
        Check("keys: Ctrl+D keeps the page",
            ShortcutHandler.Match((int)Keys.D, control: true, shift: false) == BrowserCommand.BookmarkPage);
        Check("keys: F5 reloads without a modifier",
            ShortcutHandler.Match((int)Keys.F5, false, false) == BrowserCommand.Reload);

        // Anything this window does not claim has to reach the page untouched,
        // or copying and finding on a page would quietly stop working.
        Check("keys: Ctrl+C is left to the page",
            ShortcutHandler.Match((int)Keys.C, true, false) is null);
        Check("keys: Ctrl+F is left to the page",
            ShortcutHandler.Match((int)Keys.F, true, false) is null);
        Check("keys: a letter on its own is left to the page",
            ShortcutHandler.Match((int)Keys.T, false, false) is null);
    }

    private static void CheckInternalPages()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cb-pages-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        HistoryStore history = new(Path.Combine(directory, "history.json"));
        DownloadStore downloads = new(Path.Combine(directory, "downloads.json"));
        SettingsStore settings = new(Path.Combine(directory, "settings.json"));
        BookmarkStore bookmarks = new(Path.Combine(directory, "bookmarks.json"));
        InternalPages pages = new(history, downloads, settings, bookmarks, _ => { });

        DateTimeOffset now = DateTimeOffset.Now;
        history.Record("https://example.com/", "Example Domain", now);
        history.Record("https://nesdev.org/", "NESdev Wiki", now.AddMinutes(-5));

        string listed = pages.Render(new Uri("browser://history"));
        Check("pages: history lists what was visited",
            listed.Contains("Example Domain", StringComparison.Ordinal)
            && listed.Contains("NESdev Wiki", StringComparison.Ordinal));
        Check("pages: history groups by day", listed.Contains("Today", StringComparison.Ordinal));

        string searched = pages.Render(new Uri("browser://history?q=nesdev"));
        Check("pages: the search box filters the list",
            searched.Contains("NESdev Wiki", StringComparison.Ordinal)
            && !searched.Contains("Example Domain", StringComparison.Ordinal));

        // A page title comes from a web page, so it must never reach the page
        // this program draws as anything but text.
        history.Record("https://evil.test/", "<script>alert(1)</script>", now);
        string escaped = pages.Render(new Uri("browser://history"));
        Check("pages: a title from a web page cannot bring markup with it",
            !escaped.Contains("<script>alert", StringComparison.Ordinal)
            && escaped.Contains("&lt;script&gt;", StringComparison.Ordinal));

        string cleared = pages.Render(new Uri("browser://history?clear=all"));
        Check("pages: clearing empties the list and says so",
            history.All.Count == 0 && cleared.Contains("Nowhere yet", StringComparison.Ordinal));

        downloads.Begin(7, "https://example.com/file.zip", "file.zip", 2048);
        string shown = pages.Render(new Uri("browser://downloads"));
        Check("pages: downloads are listed with where they came from",
            shown.Contains("file.zip", StringComparison.Ordinal)
            && shown.Contains("example.com", StringComparison.Ordinal));

        // The settings page is a form, and what it sends back has to land in the
        // settings rather than merely being shown again.
        string form = pages.Render(new Uri("browser://settings"));
        Check("pages: the settings show what is in force",
            form.Contains("Home page", StringComparison.Ordinal)
            && form.Contains(settings.Current.HomePage, StringComparison.Ordinal));

        pages.Render(new Uri("browser://settings?save=1&home=https%3A%2F%2Flevente.net%2F&engine=DuckDuckGo&theme=Dark"));
        Check("pages: saving the form changes the settings",
            settings.Current is { HomePage: "https://levente.net/", SearchEngine: "DuckDuckGo", Theme: ThemeChoice.Dark },
            $"got {settings.Current.HomePage}, {settings.Current.SearchEngine}, {settings.Current.Theme}");

        // A form sends nothing at all for a box that is not ticked, so the
        // absence of the field is what has to turn the bar off.
        Check("pages: an unticked box is heard as off", !settings.Current.ShowBookmarksBar);
        pages.Render(new Uri("browser://settings?save=1&bar=1"));
        Check("pages: and a ticked one as on", settings.Current.ShowBookmarksBar);

        history.Record("https://example.com/", "Example", DateTimeOffset.Now);
        pages.Render(new Uri("browser://settings?forget=history"));
        Check("pages: clearing from the settings empties the history", history.All.Count == 0);

        // The pages are drawn in whichever language is in force, title included:
        // a tab labelled "Settings" above a Hungarian page is the giveaway that
        // one string was missed.
        Strings.Use("hu");
        string hungarian = pages.Render(new Uri("browser://settings"));
        Check("pages: the settings speak the language that was chosen",
            hungarian.Contains("Kezdőlap", StringComparison.Ordinal)
            && hungarian.Contains("<title>Beállítások</title>", StringComparison.Ordinal),
            hungarian.Contains("Kezdőlap", StringComparison.Ordinal) ? "the title was missed" : "the page was missed");

        Check("pages: so does the history page",
            pages.Render(new Uri("browser://history")).Contains("Előzmények", StringComparison.Ordinal));
        Strings.Use("en");

        Check("pages: an address with no page behind it says so",
            pages.Render(new Uri("browser://nowhere")).Contains("no such page", StringComparison.Ordinal));

        Directory.Delete(directory, true);
    }

    private static void CheckBookmarksBar()
    {
        Bookmark[] bookmarks =
        [
            new() { Url = "https://example.com/", Title = "Example" },
            new() { Url = "https://nesdev.org/", Title = "NESdev Wiki" },
            new() { Url = "https://levente.net/", Title = "levente.net" },
        ];

        using BookmarksBarControl bar = new();
        bar.Size = new Size(600, 34);
        bar.Show(bookmarks);

        (Bookmark Bookmark, BookmarkAction Action)? asked = null;
        bar.Requested += (_, request) => asked = request;

        // The first bookmark starts at the bar's own left inset; a few pixels in
        // is inside it whatever its title turned out to measure.
        Click(bar, 20, 17);
        Check("bar: a bookmark opens where it is clicked",
            asked?.Bookmark.Url == "https://example.com/" && asked?.Action == BookmarkAction.Open,
            $"got {asked?.Bookmark.Url} {asked?.Action}");

        Click(bar, 20, 17, MouseButtons.Middle);
        Check("bar: the middle button opens it in a new tab",
            asked?.Action == BookmarkAction.OpenInNewTab, $"got {asked?.Action}");

        asked = null;
        Click(bar, 590, 17);
        Check("bar: clicking past the last bookmark does nothing", asked is null);

        // Narrow enough that they cannot all fit: the ones left over belong
        // behind the chevron rather than being drawn half-width.
        IReadOnlyList<Bookmark>? hidden = null;
        bar.OverflowRequested += (_, overflow) => hidden = overflow.Hidden;
        bar.Size = new Size(200, 34);
        Click(bar, 190, 17);
        Check("bar: what does not fit goes behind the chevron",
            hidden is { Count: > 0 } && hidden[^1].Url == "https://levente.net/",
            hidden is null ? "no overflow" : string.Join(", ", hidden.Select(b => b.Title)));
    }

    private static void CheckCaptionButtons()
    {
        using CaptionButtons buttons = new();
        buttons.Size = new Size(buttons.PreferredWidth, 40);
        int width = buttons.PreferredWidth / 3;

        string pressed = string.Empty;
        buttons.MinimiseClicked += (_, _) => pressed = "minimise";
        buttons.MaximiseClicked += (_, _) => pressed = "maximise";
        buttons.CloseClicked += (_, _) => pressed = "close";

        Send(buttons, "OnMouseClick", new MouseEventArgs(MouseButtons.Left, 1, width / 2, 20, 0));
        Check("caption: the first button minimises", pressed == "minimise", pressed);

        Send(buttons, "OnMouseClick", new MouseEventArgs(MouseButtons.Left, 1, width + (width / 2), 20, 0));
        Check("caption: the second maximises", pressed == "maximise", pressed);

        Send(buttons, "OnMouseClick", new MouseEventArgs(MouseButtons.Left, 1, (2 * width) + (width / 2), 20, 0));
        Check("caption: the third closes", pressed == "close", pressed);

        pressed = string.Empty;
        Send(buttons, "OnMouseClick", new MouseEventArgs(MouseButtons.Right, 1, width / 2, 20, 0));
        Check("caption: the right button does nothing", pressed.Length == 0, pressed);
    }
}
