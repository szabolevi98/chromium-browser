namespace ChromiumBrowser.Ui;

/// <summary>
/// The menus this window drops down, and the two rules that keep them working.
///
/// A menu is disposed only after the click it was closed by has been dealt
/// with. Windows Forms raises <see cref="ToolStripDropDown.Closed"/> before the
/// item's own Click, so disposing there throws the menu away with the click
/// still in flight — which looks exactly like a menu entry that does nothing,
/// and was why About, Downloads and most of the rest of this menu did nothing.
///
/// And a menu is shown relative to the control it belongs to rather than at a
/// point worked out in screen coordinates. On one screen the two are the same;
/// with a second screen at a different scaling they are not, and the menu opens
/// on the other monitor.
/// </summary>
public static class DarkMenu
{
    public static ContextMenuStrip Create(Font font) => new()
    {
        Renderer = new MenuRenderer(),
        BackColor = Theme.Current.Surface,
        ForeColor = Theme.Current.Text,
        ShowImageMargin = false,
        Font = font,
    };

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
    /// Shows a menu hanging down and to the left of a point on a control: the
    /// point is its top-right corner, which is how a menu button at the right
    /// end of a toolbar has to open. Opening to the right instead pushes it off
    /// the edge of the window — and, on a machine with a second screen, onto
    /// that screen.
    ///
    /// It also takes care of throwing the menu away afterwards, once the click
    /// has been dispatched rather than while it is still on its way.
    /// </summary>
    public static void ShowAt(this ContextMenuStrip menu, Control owner, Point at)
    {
        menu.Closed += (_, _) =>
        {
            if (owner.IsDisposed || !owner.IsHandleCreated)
            {
                menu.Dispose();
                return;
            }

            owner.BeginInvoke(menu.Dispose);
        };

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
