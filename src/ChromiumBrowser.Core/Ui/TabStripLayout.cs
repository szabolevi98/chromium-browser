namespace ChromiumBrowser.Core.Ui;

/// <summary>Where one tab sits in the strip.</summary>
/// <param name="Index">Which tab this is, counting from the left.</param>
/// <param name="X">Its left edge, relative to the start of the strip.</param>
/// <param name="Width">How wide it ended up.</param>
public readonly record struct TabBounds(int Index, int X, int Width);

/// <summary>The whole strip worked out at once.</summary>
/// <param name="Tabs">Every tab, left to right.</param>
/// <param name="NewTabX">Where the button that opens another tab goes.</param>
/// <param name="Scrolls">
/// Whether the tabs had to be cut off. Below a certain width a tab stops being a
/// tab — there is no room left for a title — so the strip stops shrinking them
/// and starts scrolling instead.
/// </param>
public readonly record struct TabStripBounds(TabBounds[] Tabs, int NewTabX, bool Scrolls);

/// <summary>
/// How wide each tab is, given how many there are and how much room the window
/// has.
///
/// This is arithmetic rather than drawing, which is why it lives here and not in
/// the window: the awkward parts are all off-by-one. Tabs have to fill the strip
/// exactly, or a hairline of the wrong colour shows at the right; the widths
/// rarely divide evenly, so the remainder has to go somewhere and it goes to the
/// leftmost tabs, a pixel each; and the new-tab button has to end up immediately
/// after the last tab rather than at a position computed a second way.
/// </summary>
public static class TabStripLayout
{
    /// <summary>A tab never grows past this, however few there are.</summary>
    public const int MaxTabWidth = 240;

    /// <summary>
    /// A tab never shrinks past this. It is the width of the favicon, the close
    /// button and a few characters of title; narrower than that the strip
    /// scrolls instead, the way every browser does it.
    /// </summary>
    public const int MinTabWidth = 66;

    /// <summary>Works out the strip.</summary>
    /// <param name="tabCount">How many tabs are open; at least one.</param>
    /// <param name="available">The room the strip has, in pixels.</param>
    /// <param name="newTabWidth">The width of the button that opens another tab.</param>
    /// <param name="scrollOffset">How far the strip is scrolled, when it scrolls.</param>
    public static TabStripBounds Compute(int tabCount, int available, int newTabWidth, int scrollOffset = 0, float scale = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tabCount, 1);

        int room = Math.Max(0, available - newTabWidth);
        int minimum = Math.Max(1, (int)(MinTabWidth * scale));
        int maximum = Math.Max(minimum, (int)(MaxTabWidth * scale));
        int ideal = room / tabCount;

        if (ideal >= minimum)
        {
            int width = Math.Min(ideal, maximum);

            // Only spread the remainder when the tabs are actually filling the
            // strip. Once they are at their full width the leftover belongs to
            // the empty space on the right, not to the tabs.
            int remainder = width == maximum ? 0 : room - (width * tabCount);

            TabBounds[] tabs = new TabBounds[tabCount];
            int x = 0;
            for (int index = 0; index < tabCount; index++)
            {
                int extra = index < remainder ? 1 : 0;
                tabs[index] = new TabBounds(index, x, width + extra);
                x += width + extra;
            }

            return new TabStripBounds(tabs, x, Scrolls: false);
        }

        // Too many to show: every tab takes the smallest width that still reads
        // as a tab, and the strip is scrolled to bring the wanted one into view.
        {
            TabBounds[] tabs = new TabBounds[tabCount];
            for (int index = 0; index < tabCount; index++)
            {
                tabs[index] = new TabBounds(index, (index * minimum) - scrollOffset, minimum);
            }

            return new TabStripBounds(tabs, room, Scrolls: true);
        }
    }

    /// <summary>How far the strip must be scrolled for a tab to be fully visible.</summary>
    /// <param name="index">The tab that has to be seen.</param>
    /// <param name="tabCount">How many there are.</param>
    /// <param name="available">The room the strip has.</param>
    /// <param name="newTabWidth">The width of the new-tab button.</param>
    /// <param name="currentOffset">Where the strip is scrolled to now.</param>
    public static int ScrollToShow(int index, int tabCount, int available, int newTabWidth, int currentOffset, float scale = 1)
    {
        TabStripBounds strip = Compute(tabCount, available, newTabWidth, currentOffset, scale);
        if (!strip.Scrolls)
        {
            return 0;
        }

        int minimum = Math.Max(1, (int)(MinTabWidth * scale));
        index = Math.Clamp(index, 0, tabCount - 1);
        int left = index * minimum;
        int right = left + minimum;
        int visible = Math.Max(0, available - newTabWidth);
        currentOffset = Math.Clamp(currentOffset, 0, Math.Max(0, tabCount * minimum - visible));

        if (left - currentOffset < 0)
        {
            return left;                      // off the left edge: bring it flush
        }

        if (right - currentOffset > visible)
        {
            return right - visible;           // off the right edge: pull it back
        }

        return currentOffset;
    }

    /// <summary>Which tab a point falls on, or -1 for none of them.</summary>
    public static int HitTest(TabStripBounds strip, int x)
    {
        foreach (TabBounds tab in strip.Tabs)
        {
            if (x >= tab.X && x < tab.X + tab.Width)
            {
                return tab.Index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Where a tab being dragged should land, given where it is held.
    ///
    /// Two things make this less obvious than it looks. The comparison is against
    /// the middle of the dragged tab rather than the pointer, so a tab changes
    /// places when it has visibly passed its neighbour and not when the pointer
    /// happens to cross an edge. And the dragged tab's own slot is left out of
    /// the count: it is being held above the strip, so the question is how many
    /// of the <em>others</em> it has got past.
    /// </summary>
    /// <param name="strip">The strip as it is laid out, dragged tab included.</param>
    /// <param name="draggedIndex">Which tab is being held.</param>
    /// <param name="draggedCentreX">The middle of that tab, where it is now.</param>
    public static int DropIndex(TabStripBounds strip, int draggedIndex, int draggedCentreX)
    {
        int passed = 0;
        foreach (TabBounds tab in strip.Tabs)
        {
            if (tab.Index == draggedIndex)
            {
                continue;
            }

            if (tab.X + (tab.Width / 2) < draggedCentreX)
            {
                passed++;
            }
        }

        return passed;
    }
}
