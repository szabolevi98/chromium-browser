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
    /// Shows the menu under a point on a control, and takes care of throwing it
    /// away afterwards — once the click has been dispatched, not while it is
    /// still on its way.
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

        menu.Show(owner, at);
    }
}
