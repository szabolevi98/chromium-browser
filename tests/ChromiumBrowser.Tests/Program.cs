using ChromiumBrowser.Core;
using ChromiumBrowser.Core.Profile;
using ChromiumBrowser.Core.Ui;

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

Console.WriteLine();
Console.WriteLine($"{total - failures}/{total} passed");
return failures == 0 ? 0 : 1;
