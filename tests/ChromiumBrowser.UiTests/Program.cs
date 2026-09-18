using System.Reflection;
using ChromiumBrowser.Browser;
using ChromiumBrowser.Controls;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Localisation;
using ChromiumBrowser.Core.Profile;
using ChromiumBrowser.Core.Ui;
using ChromiumBrowser.Ui;

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
        CheckFindBar();
        CheckMenu();
        CheckPrivateBadge();
        CheckPrivatePage();
        CheckHandover();

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

    private static void Send(Control control, string handler, EventArgs e) =>
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
        Check("keys: Ctrl+A is left to the page",
            ShortcutHandler.Match((int)Keys.A, true, false) is null);

        // Ctrl+F is the one key this window took back off the page, because a
        // browser's own find bar is what people expect it to open.
        Check("keys: Ctrl+N opens a window and Ctrl+Shift+N a private one",
            ShortcutHandler.Match((int)Keys.N, true, false) == BrowserCommand.NewWindow
            && ShortcutHandler.Match((int)Keys.N, true, true) == BrowserCommand.NewPrivateWindow);

        Check("keys: Ctrl+F opens the find bar",
            ShortcutHandler.Match((int)Keys.F, true, false) == BrowserCommand.FindInPage);
        Check("keys: F3 walks the matches, and with Shift walks back",
            ShortcutHandler.Match((int)Keys.F3, false, false) == BrowserCommand.FindNext
            && ShortcutHandler.Match((int)Keys.F3, false, true) == BrowserCommand.FindPrevious);
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
        SessionStore session = new(Path.Combine(directory, "session.json"));
        InternalPages pages = new(history, downloads, settings, bookmarks, session, _ => { });

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

        // The same rule decides whether the browser comes back to what was open.
        Check("pages: the settings offer to bring back what was open",
            pages.Render(new Uri("browser://settings")).Contains("were open last time", StringComparison.Ordinal));
        pages.Render(new Uri("browser://settings?save=1"));
        Check("pages: unticking it is heard", !settings.Current.RestoreSession);
        pages.Render(new Uri("browser://settings?save=1&restore=1"));
        Check("pages: and ticking it again is too", settings.Current.RestoreSession);

        session.Save([new SavedWindow { Tabs = [new SavedTab { Url = "https://example.com/" }] }]);
        pages.Render(new Uri("browser://settings?forget=session"));
        Check("pages: and it can forget what was open", !session.HasSomething);

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

        // The page says which language it is in, because that is what a screen
        // reader and the engine's own spell checking go by.
        Check("pages: and the page itself says which language it is in",
            pages.Render(new Uri("browser://history")).Contains("<html lang=\"hu\">", StringComparison.Ordinal));

        // The three that were added later, each checked somewhere different so
        // that a language wired into one page only would show up.
        Strings.Use("de");
        Check("pages: German reaches the settings",
            pages.Render(new Uri("browser://settings")) is string german
            && german.Contains("Startseite", StringComparison.Ordinal)
            && german.Contains("<title>Einstellungen</title>", StringComparison.Ordinal));

        Strings.Use("fr");
        Check("pages: French reaches the downloads",
            pages.Render(new Uri("browser://downloads")).Contains("Téléchargements", StringComparison.Ordinal));

        Strings.Use("es");
        Check("pages: Spanish reaches the private window's page",
            pages.Render(new Uri("browser://private")).Contains("Estás navegando en privado", StringComparison.Ordinal));

        // Every language has to be in the list, or one of them could never be
        // chosen in the first place.
        string offered = pages.Render(new Uri("browser://settings"));
        Check("pages: all five languages are on the settings page",
            Strings.Languages.All(spoken => offered.Contains($"value=\"{spoken.Code}\"", StringComparison.Ordinal)
                                            && offered.Contains(spoken.Name, StringComparison.Ordinal)));

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

    private static void CheckFindBar()
    {
        using FindBarControl bar = new();
        bar.Size = new Size(900, 38);

        // The box is a real text box inside the bar, so typing is what the tests
        // do to it rather than a method added for their benefit.
        TextBox box = bar.Controls.OfType<TextBox>().First();

        string typed = string.Empty;
        bar.SearchChanged += (_, text) => typed = text;
        box.Text = "wiki";
        Check("find bar: what is typed is what is searched for",
            typed == "wiki" && bar.SearchText == "wiki", $"got {typed}");

        bool? forward = null;
        bar.StepRequested += (_, next) => forward = next;

        Send(box, "OnKeyDown", new KeyEventArgs(Keys.Enter));
        Check("find bar: Enter walks to the next match", forward == true, $"got {forward}");

        Send(box, "OnKeyDown", new KeyEventArgs(Keys.Enter | Keys.Shift));
        Check("find bar: and with Shift back to the previous one", forward == false, $"got {forward}");

        bool closed = false;
        bar.CloseRequested += (_, _) => closed = true;
        Send(box, "OnKeyDown", new KeyEventArgs(Keys.Escape));
        Check("find bar: Escape closes it without touching the mouse", closed);

        // The three buttons follow the box: previous, next, close. Their places
        // are the same arithmetic the bar paints from. The bar reads a finished
        // click rather than the press and the release, so that is what is sent.
        void ClickBar(int x) =>
            Send(bar, "OnMouseClick", new MouseEventArgs(MouseButtons.Left, 1, x, 19, 0));

        forward = null;
        closed = false;
        ClickBar(bar.ButtonRect(0).X + 14);
        Check("find bar: the first button is the previous match", forward == false, $"got {forward}");

        ClickBar(bar.ButtonRect(1).X + 14);
        Check("find bar: the second is the next one", forward == true, $"got {forward}");

        ClickBar(bar.ButtonRect(2).X + 14);
        Check("find bar: the third closes the bar", closed);

        // Clicking the box itself must not be read as one of the buttons.
        forward = null;
        closed = false;
        ClickBar(20);
        Check("find bar: clicking in the box does nothing else", forward is null && !closed);

        Check("find bar: it starts hidden, so a window that never searches never shows it",
            !new FindBarControl().Visible);
    }

    private static void CheckMenu()
    {
        using Form owner = new();
        owner.CreateControl();

        ContextMenuStrip menu = DarkMenu.Create(owner.Font);

        int ran = 0;
        menu.Add("Downloads", "Ctrl+J", () => ran++);
        menu.Separator();
        ToolStripMenuItem about = menu.Add("About", string.Empty, () => ran += 10);

        Check("menu: an entry carries the keys that do the same thing",
            menu.Items[0] is ToolStripMenuItem { ShortcutKeyDisplayString: "Ctrl+J" });

        // The window where the menu button actually lives: against the right
        // edge of the screen, with the button in its top-right corner. This is
        // the case that sent the menu onto the second monitor.
        Rectangle screen = Screen.PrimaryScreen!.WorkingArea;
        owner.Size = new Size(600, 400);
        owner.Location = new Point(screen.Right - owner.Width, screen.Top + 100);

        Point button = new(owner.Width - 10, 40);
        menu.ShowAt(owner, button);

        Point corner = owner.PointToScreen(button);
        Check("menu: it hangs down and to the left of the button",
            Math.Abs(menu.Bounds.Right - corner.X) < 40 && Math.Abs(menu.Bounds.Y - corner.Y) < 40,
            $"button at {corner}, menu at {menu.Bounds}");

        Check("menu: and stays on the screen the window is on",
            screen.Contains(menu.Bounds),
            $"{menu.Bounds} is not inside {screen}");

        // Windows Forms closes the menu before it dispatches the click, so a menu
        // thrown away on Closed takes the click with it — which is what made
        // About and Downloads do nothing at all. Closing and then clicking is
        // exactly that order.
        menu.Close();
        about.PerformClick();

        Check("menu: an entry still does its work after the menu has closed",
            ran == 10, $"ran {ran}");

        ((ToolStripMenuItem)menu.Items[0]).PerformClick();
        Check("menu: and so does the next one", ran == 11, $"ran {ran}");

        Application.DoEvents(); // the menu is thrown away here, once the clicks are through

        // What the About entry opens. Built rather than shown, so a mistake in
        // it fails here instead of the entry looking dead when it is clicked.
        using AboutForm box = new(new ProfileLocation(@"E:\stickrowser\Data", IsPortable: true));
        box.CreateControl();
        Check("menu: the about box can be built, which is what About opens",
            box.Controls.Count > 0 && box.Text.Contains("Chromium Browser", StringComparison.Ordinal),
            box.Text);
    }

    private static void CheckPrivateBadge()
    {
        // Drawn rather than described: the badge is the only thing telling a
        // private window apart from a normal one at a glance, so the check
        // paints the toolbar and looks at the pixels.
        static Bitmap Paint(bool isPrivate)
        {
            using ToolbarControl toolbar = new();
            toolbar.Size = new Size(900, 44);
            toolbar.IsPrivate = isPrivate;

            Bitmap picture = new(toolbar.Width, toolbar.Height);
            toolbar.DrawToBitmap(picture, new Rectangle(0, 0, toolbar.Width, toolbar.Height));
            return picture;
        }

        using Bitmap normal = Paint(false);
        using Bitmap marked = Paint(true);



        int differing = 0;
        for (int y = 0; y < normal.Height; y++)
        {
            for (int x = 0; x < normal.Width; x++)
            {
                if (normal.GetPixel(x, y) != marked.GetPixel(x, y))
                {
                    differing++;
                }
            }
        }

        Check("badge: a private window's toolbar does not look like a normal one",
            differing > 200, $"{differing} pixels differ");

        // The pill on its own is not the point: it has to say something. The
        // text is drawn in the muted colour, so what is counted is pixels of it
        // well inside the pill, away from its border.
        // The pill on its own is not the point: it has to say something. The
        // lettering is counted inside the pill alone, because the rest of the
        // toolbar has muted pixels of its own.
        //
        // This is also the check that caught the badge coming out blank:
        // Graphics.DrawString draws nothing on this control's surface, and the
        // GDI path WinForms uses for its own labels draws it.
        Color muted = Theme.Current.TextMuted;
        Rectangle pill = new(778, 11, 74, 22);
        int lettering = 0;
        for (int y = pill.Top + 3; y < pill.Bottom - 3; y++)
        {
            for (int x = pill.Left + 3; x < pill.Right - 3; x++)
            {
                Color pixel = marked.GetPixel(x, y);
                if (Math.Abs(pixel.R - muted.R) < 60
                    && Math.Abs(pixel.G - muted.G) < 60
                    && Math.Abs(pixel.B - muted.B) < 60)
                {
                    lettering++;
                }
            }
        }

        Check("badge: and it says which kind of window this is",
            lettering > 30, $"{lettering} pixels of lettering");
    }

    private static void CheckPrivatePage()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"cb-private-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        InternalPages pages = new(
            new HistoryStore(Path.Combine(directory, "history.json")),
            new DownloadStore(Path.Combine(directory, "downloads.json")),
            new SettingsStore(Path.Combine(directory, "settings.json")),
            new BookmarkStore(Path.Combine(directory, "bookmarks.json")),
            new SessionStore(Path.Combine(directory, "session.json")),
            _ => { });

        string page = pages.Render(new Uri("browser://private"));
        Check("private page: it says what the window does not keep",
            page.Contains("keeps nothing", StringComparison.Ordinal));

        // The half every browser has had to learn to spell out: people read
        // "private" as "invisible".
        Check("private page: and that it does not make you invisible",
            page.Contains("does not hide you", StringComparison.Ordinal));

        Strings.Use("hu");
        Check("private page: in the language that was chosen, title included",
            pages.Render(new Uri("browser://private")) is string hungarian
            && hungarian.Contains("Privátan böngészel", StringComparison.Ordinal)
            && hungarian.Contains("<title>Privát ablak</title>", StringComparison.Ordinal));
        Strings.Use("en");

        Directory.Delete(directory, true);
    }

    private static void CheckHandover()
    {
        // Two different folders are two different browsers: a copy on a memory
        // stick must not hand its addresses to the one installed on the machine.
        string mine = Path.Combine(Path.GetTempPath(), $"cb-instance-{Guid.NewGuid():N}");
        string somewhereElse = Path.Combine(Path.GetTempPath(), $"cb-instance-{Guid.NewGuid():N}");

        using Mutex? first = SingleInstance.Claim(mine);
        Check("one copy: the first launch claims the profile", first is not null);

        using Mutex? second = SingleInstance.Claim(mine);
        Check("one copy: the second does not", second is null);

        using Mutex? elsewhere = SingleInstance.Claim(somewhereElse);
        Check("one copy: but another folder is another browser", elsewhere is not null);

        string? handed = "nothing yet";
        using ManualResetEventSlim arrived = new(false);
        SingleInstance.Listen(mine, url =>
        {
            handed = url;
            arrived.Set();
        });

        Check("one copy: the address is taken by the copy already running",
            SingleInstance.Send(mine, "https://example.com/"));
        Check("one copy: and arrives as it was sent",
            arrived.Wait(TimeSpan.FromSeconds(3)) && handed == "https://example.com/", $"got {handed}");

        // Started with no address at all: still a request to show the browser.
        arrived.Reset();
        SingleInstance.Send(mine, null);
        Check("one copy: a launch with no address still asks for the window",
            arrived.Wait(TimeSpan.FromSeconds(3)) && handed is null, $"got {handed}");

        Check("one copy: nobody is listening for the other folder",
            !SingleInstance.Send(somewhereElse, "https://example.com/"));
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
