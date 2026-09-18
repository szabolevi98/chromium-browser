# Chromium Browser

A portable web browser built on the Chromium engine: unpack it anywhere, run it,
and it keeps its history, bookmarks and settings in a folder beside itself. There
is nothing to install and nothing is written to the registry.

> **2.0 is being written now.** The 2020 version that used to live here is kept
> on the [`2020_variant`](https://github.com/szabolevi98/chromium-browser/tree/2020_variant)
> branch and as [release v1.0.1](https://github.com/szabolevi98/chromium-browser/releases/tag/v1.0.1).
> It is not being modernised — it is being replaced, from an empty folder.

![The window as it stands](docs/screenshot.png)

The chrome is painted by this project: the tab strip, the window buttons and the
toolbar are drawn rather than assembled from system controls.

## Where it is

- [x] The engine runs: CefSharp 152 (Chromium 152) on .NET 9, x64
- [x] A portable profile: everything the browser remembers sits beside the
      executable, falling back to the user profile only when that folder cannot
      be written to
- [x] A window that draws its own title bar, keeping the snapping, shadow and
      rounded corners Windows gives a normal one
- [x] Tabs: drawn rather than assembled, dragged to reorder, closed with the
      middle button, shrinking as they multiply and scrolling once they cannot
      shrink further
- [x] Back, forward, reload, home, and a bar that takes an address or a search
- [x] Light and dark, following the system, with the desktop's own accent colour
- [x] Favicons on the tabs, a menu, and the keyboard shortcuts a browser is
      expected to answer, including inside a page that has the focus
- [x] Bookmarks, history and downloads: kept as JSON in the portable profile,
      reachable from the menu, with the engine's downloads routed into the list
- [x] History and downloads as pages of the browser's own, at
      `browser://history` and `browser://downloads`, in the window's colours
- [x] A bookmarks bar: each one as wide as its title needs, icons guessed
      from each site's root, the rest behind a chevron, and Ctrl+Shift+B to
      hide it
- [x] Settings at `browser://settings`: home page, search engine including
      one of your own, light or dark or follow Windows, and clearing what
      has been kept
- [x] English and Hungarian, switched in the settings, with a check that
      nothing was left untranslated
- [x] Find on a page: Ctrl+F opens a bar in the chrome, Enter and F3 walk
      the matches, and the count beside the box climbs as the engine
      finds them
- [ ] Session restore, private windows

## What 2.0 has to do at least

Everything the 2020 version did, which was: several tabs, a search box, bookmarks
with a manager, a home page that can be changed, a download handler, and a
Hungarian/English switch. That is the floor, not the target.

## Building

Needs the .NET 9 SDK.

```
dotnet build ChromiumBrowser.sln
dotnet run --project tests/ChromiumBrowser.Tests    # 71 offline checks
dotnet run --project tests/ChromiumBrowser.UiTests  # 52 user interface checks
```

The user interface checks drive the controls the way a pointer would, without
showing a window: they click tabs, drag one past its neighbour and press the
window buttons. `tools/screenshot.ps1` captures the running window on its own,
which is how the pictures here are taken.

## Layout

```
src/ChromiumBrowser          the Windows Forms application
src/ChromiumBrowser.Core     profile, layout arithmetic, and the data behind the
                             browser — no user interface dependencies
tests/ChromiumBrowser.Tests   offline checks
tests/ChromiumBrowser.UiTests checks that drive the controls
tools/                        the screenshot tool
```

## License

MIT. See [LICENSE](LICENSE).
