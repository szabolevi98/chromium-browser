using System.Reflection;
using ChromiumBrowser.Browser;
using ChromiumBrowser.Controls;
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
