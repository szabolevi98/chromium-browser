namespace ChromiumBrowser.Ui;

/// <summary>
/// The menus this window drops down, and the three things that keep them
/// working.
///
/// **One menu, refilled.** A window keeps a single menu for its whole life and
/// fills it again each time it opens, rather than building one per click and
/// throwing it away afterwards. Throwing it away is what there is no safe
/// moment for: Windows Forms raises <see cref="ToolStripDropDown.Closed"/>
/// before it dispatches the item's Click, so disposing there takes the click
/// with it and the entry does nothing — and deferring the disposal instead is
/// worse, because an entry that opens a dialog runs a message loop of its own,
/// the deferred disposal runs inside it, and the click comes back to a menu
/// that is gone. That one crashed the browser outright.
///
/// **Refilling disposes what was there.** The items belong to the menu, and
/// clearing a collection does not dispose them.
///
/// **Shown relative to the control.** A menu placed at a point worked out in
/// screen coordinates opens on the wrong monitor once a second screen is scaled
/// differently, and a menu button at the right end of a toolbar has to hang
/// down and to the left or it goes off the edge of the window entirely.
/// </summary>
public static class DarkMenu
{
    /// <summary>The one menu a window keeps. Made once, filled many times.</summary>
    public static ContextMenuStrip Create(Font font) => new()
    {
        Renderer = new MenuRenderer(),
        ShowImageMargin = false,
        Font = font,
    };

    /// <summary>
    /// Empties the menu, disposing what was in it, and puts it back in the
    /// colours in force — which may have changed since it was last opened.
    /// </summary>
    public static ContextMenuStrip Reset(this ContextMenuStrip menu)
    {
        foreach (ToolStripItem item in menu.Items.Cast<ToolStripItem>().ToArray())
        {
            item.Dispose();
        }

        menu.Items.Clear();
        menu.BackColor = Theme.Current.Surface;
        menu.ForeColor = Theme.Current.Text;
        return menu;
    }

    /// <summary>One entry, with the keys that do the same thing shown beside it.</summary>
    public static ToolStripMenuItem Add(this ContextMenuStrip menu, string text, string keys, Action action)
    {
        ToolStripMenuItem item = new(text, null, (_, _) => action())
        {
            ShortcutKeyDisplayString = keys,
        };

        menu.Items.Add(item);
        return item;
    }

    public static void Separator(this ContextMenuStrip menu) => menu.Items.Add(new ToolStripSeparator());

    /// <summary>
    /// Shows the menu hanging down and to the left of a point on a control: the
    /// point is its top-right corner.
    /// </summary>
    public static void ShowAt(this ContextMenuStrip menu, Control owner, Point at)
    {
        menu.Show(owner, at, ToolStripDropDownDirection.BelowLeft);

        // Windows Forms keeps a drop-down on the screen its owner is on, but only
        // by the edge it would cross first. A menu taller than what is left below
        // the button is moved up; one that would still hang off the bottom of the
        // screen is pulled back onto it here, because a menu whose last entries
        // are under the taskbar is a menu missing Exit.
        Rectangle screen = Screen.FromControl(owner).WorkingArea;
        Rectangle where = menu.Bounds;

        int x = Math.Clamp(where.X, screen.Left, Math.Max(screen.Left, screen.Right - where.Width));
        int y = Math.Clamp(where.Y, screen.Top, Math.Max(screen.Top, screen.Bottom - where.Height));

        if (x != where.X || y != where.Y)
        {
            menu.Location = new Point(x, y);
        }
    }
}
