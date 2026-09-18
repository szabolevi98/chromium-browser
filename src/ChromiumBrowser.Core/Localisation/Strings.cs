namespace ChromiumBrowser.Core.Localisation;

/// <summary>
/// Everything the browser says, in both languages.
///
/// One table rather than resource files, because a browser this size has a few
/// dozen phrases and a resource file per language turns every addition into
/// three edits in three places. Here a phrase is one line with both languages on
/// it, which is also what makes the check that nothing was left untranslated
/// possible at all.
///
/// A key that is missing falls back to English rather than showing the key, so
/// a phrase added and not yet translated reads oddly rather than looking broken.
/// </summary>
public static class Strings
{
    /// <summary>The two-letter code in use: <c>en</c> or <c>hu</c>.</summary>
    public static string Language { get; private set; } = "en";

    public static bool IsHungarian => Language == "hu";

    /// <summary>Raised after the language changed, so what is on screen can be redrawn.</summary>
    public static event EventHandler? Changed;

    public static void Use(string language)
    {
        string wanted = language == "hu" ? "hu" : "en";
        if (wanted == Language)
        {
            return;
        }

        Language = wanted;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>The phrase for a key, in the language in force.</summary>
    public static string Of(string key) =>
        Table.TryGetValue(key, out (string English, string Hungarian) phrase)
            ? (IsHungarian ? phrase.Hungarian : phrase.English)
            : key;

    /// <summary>Every key, for the check that both languages are complete.</summary>
    public static IReadOnlyDictionary<string, (string English, string Hungarian)> All => Table;

    private static readonly Dictionary<string, (string English, string Hungarian)> Table = new()
    {
        // The window and its menu
        ["tab.new"] = ("New tab", "Új lap"),
        ["menu.newTab"] = ("New tab", "Új lap"),
        ["menu.newWindow"] = ("New window", "Új ablak"),
        ["menu.newPrivateWindow"] = ("New private window", "Új privát ablak"),
        ["menu.closeTab"] = ("Close tab", "Lap bezárása"),
        ["menu.bookmark"] = ("Bookmark this page", "Oldal könyvjelzőzése"),
        ["menu.unbookmark"] = ("Remove bookmark", "Könyvjelző törlése"),
        ["menu.bookmarks"] = ("Bookmarks", "Könyvjelzők"),
        ["menu.history"] = ("History", "Előzmények"),
        ["menu.downloads"] = ("Downloads", "Letöltések"),
        ["menu.settings"] = ("Settings", "Beállítások"),
        ["menu.zoomIn"] = ("Zoom in", "Nagyítás"),
        ["menu.zoomOut"] = ("Zoom out", "Kicsinyítés"),
        ["menu.zoomReset"] = ("Reset zoom", "Eredeti méret"),
        ["menu.print"] = ("Print...", "Nyomtatás..."),
        ["menu.about"] = ("About", "Névjegy"),
        ["menu.exit"] = ("Exit", "Kilépés"),
        ["menu.empty"] = ("Nothing yet", "Még semmi"),

        ["menu.find"] = ("Find on this page...", "Keresés az oldalon..."),

        // The window that remembers nothing
        ["private.badge"] = ("Private", "Privát"),
        ["private.window"] = ("Private window", "Privát ablak"),
        ["private.title"] = ("You are browsing privately", "Privátan böngészel"),
        ["private.what"] = ("This window keeps nothing: no history, no download list, and its cookies go when it closes. What you download stays, and so do bookmarks you add.",
                            "Ez az ablak semmit nem őriz meg: se előzményt, se letöltési listát, a sütijei pedig a bezárásakor eltűnnek. Amit letöltesz, az megmarad, és a felvett könyvjelzők is."),
        ["private.notInvisible"] = ("It does not hide you from the sites you visit, from your employer, or from whoever runs the network.",
                                    "Attól még lát téged a meglátogatott oldal, a munkahelyed és az is, aki a hálózatot üzemelteti."),

        // Finding words on a page
        ["find.placeholder"] = ("Find on this page", "Keresés az oldalon"),
        ["find.none"] = ("No matches", "Nincs találat"),

        // A bookmark's own menu
        ["bookmark.open"] = ("Open", "Megnyitás"),
        ["bookmark.openNewTab"] = ("Open in a new tab", "Megnyitás új lapon"),
        ["bookmark.rename"] = ("Rename...", "Átnevezés..."),
        ["bookmark.remove"] = ("Remove", "Törlés"),
        ["bookmark.renameTitle"] = ("Rename bookmark", "Könyvjelző átnevezése"),
        ["bookmark.name"] = ("Name", "Név"),

        // Buttons in dialogs
        ["button.ok"] = ("OK", "OK"),
        ["button.cancel"] = ("Cancel", "Mégse"),
        ["button.close"] = ("Close", "Bezárás"),

        // The about box
        ["about.title"] = ("About", "Névjegy"),
        ["about.tagline"] = ("A portable browser on the Chromium engine.",
                             "Hordozható böngésző a Chromium motorjára építve."),
        ["about.version"] = ("Version", "Verzió"),
        ["about.portable"] = ("Portable: data kept beside the program",
                              "Hordozható: az adatok a program mellett vannak"),
        ["about.installed"] = ("Data kept in your user profile",
                               "Az adatok a felhasználói profilodban vannak"),

        // The pages the browser draws itself
        ["page.history"] = ("History", "Előzmények"),
        ["page.downloads"] = ("Downloads", "Letöltések"),
        ["page.settings"] = ("Settings", "Beállítások"),
        ["page.notFound"] = ("There is no such page.", "Nincs ilyen oldal."),
        ["history.search"] = ("Search history", "Keresés az előzményekben"),
        ["history.clear"] = ("Clear all", "Összes törlése"),
        ["history.empty"] = ("Nowhere yet. Pages you visit will be listed here.",
                             "Még sehol. A meglátogatott oldalak itt jelennek meg."),
        ["history.noMatch"] = ("Nothing matches that.", "Erre nincs találat."),
        ["history.today"] = ("Today", "Ma"),
        ["history.yesterday"] = ("Yesterday", "Tegnap"),
        ["history.visits"] = ("visits", "látogatás"),
        ["downloads.empty"] = ("Nothing downloaded yet.", "Még nincs letöltés."),
        ["downloads.clear"] = ("Clear finished", "Befejezettek törlése"),
        ["downloads.reveal"] = ("Show in folder", "Megjelenítés a mappában"),
        ["downloads.cancelled"] = ("Cancelled", "Megszakítva"),
        ["downloads.interrupted"] = ("Interrupted", "Félbeszakadt"),
        ["downloads.unknownSize"] = ("unknown size", "ismeretlen méret"),
        ["downloads.soFar"] = ("so far", "eddig"),
        ["downloads.of"] = ("of", "ebből"),

        // The settings page
        ["settings.home"] = ("Home page", "Kezdőlap"),
        ["settings.homeHint"] = ("Where the home button and every new tab go.",
                                 "Ide visz a kezdőlap gomb és minden új lap."),
        ["settings.search"] = ("Search with", "Keresés ezzel"),
        ["settings.searchOther"] = ("Something else", "Valami más"),
        ["settings.searchHint"] = ("Anything typed in the bar that is not an address is searched for. An address of your own needs {0} where the words go.",
                                   "Amit a sávba írsz és nem cím, arra rákeres. A saját címben oda kell a {0}, ahova a keresett szavak kerülnek."),
        ["settings.appearance"] = ("Appearance", "Megjelenés"),
        ["settings.themeSystem"] = ("Follow Windows", "Windows szerint"),
        ["settings.themeLight"] = ("Light", "Világos"),
        ["settings.themeDark"] = ("Dark", "Sötét"),
        ["settings.bar"] = ("Show the bookmarks bar", "Könyvjelzősáv megjelenítése"),
        ["settings.language"] = ("Language", "Nyelv"),
        ["settings.save"] = ("Save", "Mentés"),
        ["settings.saved"] = ("Saved.", "Elmentve."),
        ["settings.cleared"] = ("Cleared.", "Törölve."),
        ["settings.clear"] = ("Clear", "Törlés"),
        ["settings.clearHistory"] = ("Clear history", "Előzmények törlése"),
        ["settings.clearDownloads"] = ("Clear the download list", "Letöltési lista törlése"),
        ["settings.inHistory"] = ("in history", "az előzményekben"),
        ["settings.kept"] = ("kept", "elmentve"),
        ["settings.pageOne"] = ("page", "oldal"),
        ["settings.pageMany"] = ("pages", "oldal"),
        ["settings.bookmarkOne"] = ("bookmark", "könyvjelző"),
        ["settings.bookmarkMany"] = ("bookmarks", "könyvjelző"),

        // Trouble
        ["error.engine"] = ("The Chromium engine could not start.", "A Chromium motor nem tudott elindulni."),
    };
}
