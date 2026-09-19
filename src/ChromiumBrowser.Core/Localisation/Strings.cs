namespace ChromiumBrowser.Core.Localisation;

/// <summary>A language the browser speaks: the code it is kept under, and what it calls itself.</summary>
/// <param name="Code">Two letters, which is what the settings store.</param>
/// <param name="Name">Its own name for itself, because that is what people look for in a list.</param>
public readonly record struct Spoken(string Code, string Name);

/// <summary>
/// Everything the browser says, in every language it says it in.
///
/// One table rather than a file per language, because a browser this size has a
/// hundred phrases and a file per language turns every addition into five edits
/// in five places. Here a phrase is one line with every language on it, which is
/// also what makes the check that nothing was left untranslated possible at all:
/// a phrase copied across and not translated is a line where two columns match,
/// and the check names it.
///
/// A key that is missing falls back to English rather than showing the key, so a
/// phrase added and not yet translated reads oddly rather than looking broken.
/// </summary>
public static class Strings
{
    /// <summary>One phrase, in all of them.</summary>
    public readonly record struct Phrase(
        string English,
        string Hungarian,
        string German,
        string French,
        string Spanish)
    {
        /// <summary>The five, in the order <see cref="Languages"/> lists them.</summary>
        public string[] Every => [English, Hungarian, German, French, Spanish];
    }

    /// <summary>The languages on offer, in the order the settings list them.</summary>
    public static IReadOnlyList<Spoken> Languages { get; } =
    [
        new("en", "English"),
        new("hu", "Magyar"),
        new("de", "Deutsch"),
        new("fr", "Français"),
        new("es", "Español"),
    ];

    /// <summary>The two-letter code in use.</summary>
    public static string Language { get; private set; } = "en";

    public static bool IsHungarian => Language == "hu";

    /// <summary>Raised after the language changed, so what is on screen can be redrawn.</summary>
    public static event EventHandler? Changed;

    /// <summary>
    /// Switches language. A code the browser does not speak becomes English
    /// rather than an error: a settings file from a later version, or one edited
    /// by hand, should not leave somebody without words.
    /// </summary>
    public static void Use(string language)
    {
        string wanted = Languages.Any(spoken => spoken.Code == language) ? language : "en";
        if (wanted == Language)
        {
            return;
        }

        Language = wanted;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>The phrase for a key, in the language in force.</summary>
    public static string Of(string key)
    {
        if (!Table.TryGetValue(key, out Phrase phrase))
        {
            return key;
        }

        return Language switch
        {
            "hu" => phrase.Hungarian,
            "de" => phrase.German,
            "fr" => phrase.French,
            "es" => phrase.Spanish,
            _ => phrase.English,
        };
    }

    /// <summary>Every phrase, for the checks that hold the translations complete.</summary>
    public static IReadOnlyDictionary<string, Phrase> All => Table;

    private static Phrase P(string en, string hu, string de, string fr, string es) => new(en, hu, de, fr, es);

    private static readonly Dictionary<string, Phrase> Table = new()
    {
        ["tab.reopen"] = P("Reopen closed tab", "Bezárt lap visszanyitása", "Geschlossenen Tab öffnen", "Rouvrir l'onglet fermé", "Reabrir pestaña cerrada"),
        ["find.previous"] = P("Previous match (Shift+F3)", "Előző találat (Shift+F3)", "Vorheriger Treffer (Umschalt+F3)", "Résultat précédent (Maj+F3)", "Resultado anterior (Mayús+F3)"),
        ["find.next"] = P("Next match (F3)", "Következő találat (F3)", "Nächster Treffer (F3)", "Résultat suivant (F3)", "Siguiente resultado (F3)"),
        ["find.close"] = P("Close search (Esc)", "Keresés bezárása (Esc)", "Suche schließen (Esc)", "Fermer la recherche (Échap)", "Cerrar búsqueda (Esc)"),
        ["tab.duplicate"] = P("Duplicate tab", "Lap duplikálása", "Tab duplizieren", "Dupliquer l'onglet", "Duplicar pestaña"),
        ["tab.closeOthers"] = P("Close other tabs", "Többi lap bezárása", "Andere Tabs schließen", "Fermer les autres onglets", "Cerrar las otras pestañas"),
        ["tab.closeRight"] = P("Close tabs to the right", "Jobbra lévő lapok bezárása", "Tabs rechts schließen", "Fermer les onglets à droite", "Cerrar pestañas a la derecha"),
        ["tab.mute"] = P("Mute tab", "Lap némítása", "Tab stummschalten", "Couper le son de l'onglet", "Silenciar pestaña"),
        ["tab.unmute"] = P("Unmute tab", "Lap hangjának visszaállítása", "Tab-Ton einschalten", "Rétablir le son de l'onglet", "Activar sonido de pestaña"),
        ["window.minimise"] = P("Minimise window", "Ablak kis méretre", "Fenster minimieren", "Réduire la fenêtre", "Minimizar ventana"),
        ["window.maximise"] = P("Maximise window", "Ablak teljes méretre", "Fenster maximieren", "Agrandir la fenêtre", "Maximizar ventana"),
        ["window.restore"] = P("Restore window", "Ablak méretének visszaállítása", "Fenster wiederherstellen", "Restaurer la fenêtre", "Restaurar ventana"),
        ["window.close"] = P("Close window", "Ablak bezárása", "Fenster schließen", "Fermer la fenêtre", "Cerrar ventana"),
        ["window.fullscreen"] = P("Full screen", "Teljes képernyő", "Vollbild", "Plein écran", "Pantalla completa"),
        ["window.exitFullscreen"] = P("Exit full screen", "Kilépés a teljes képernyőből", "Vollbild verlassen", "Quitter le plein écran", "Salir de pantalla completa"),
        ["toolbar.address"] = P("Address or search", "Cím vagy keresés", "Adresse oder Suche", "Adresse ou recherche", "Dirección o búsqueda"),
        ["toolbar.back"] = P("Back (Alt+Left)", "Vissza (Alt+Bal)", "Zurück (Alt+Links)", "Précédent (Alt+Gauche)", "Atrás (Alt+Izquierda)"),
        ["toolbar.forward"] = P("Forward (Alt+Right)", "Előre (Alt+Jobb)", "Vorwärts (Alt+Rechts)", "Suivant (Alt+Droite)", "Adelante (Alt+Derecha)"),
        ["toolbar.reload"] = P("Reload (F5)", "Újratöltés (F5)", "Neu laden (F5)", "Actualiser (F5)", "Recargar (F5)"),
        ["toolbar.stop"] = P("Stop loading (Esc)", "Betöltés leállítása (Esc)", "Laden stoppen (Esc)", "Arrêter le chargement (Esc)", "Detener carga (Esc)"),
        ["toolbar.home"] = P("Home page", "Kezdőoldal", "Startseite", "Page d'accueil", "Página de inicio"),
        ["toolbar.menu"] = P("Browser menu", "Böngésző menüje", "Browsermenü", "Menu du navigateur", "Menú del navegador"),
        ["downloads.paused"] = P("Paused", "Szüneteltetve", "Angehalten", "En pause", "En pausa"),
        ["downloads.pause"] = P("Pause download", "Letöltés szüneteltetése", "Download anhalten", "Suspendre le téléchargement", "Pausar descarga"),
        ["downloads.resume"] = P("Resume download", "Letöltés folytatása", "Download fortsetzen", "Reprendre le téléchargement", "Reanudar descarga"),
        ["downloads.cancel"] = P("Cancel download", "Letöltés megszakítása", "Download abbrechen", "Annuler le téléchargement", "Cancelar descarga"),
        ["downloads.retry"] = P("Retry download", "Letöltés újrapróbálása", "Download erneut versuchen", "Réessayer le téléchargement", "Reintentar descarga"),
        // The window and its menu
        ["tab.new"] = P("New tab", "Új lap", "Neuer Tab", "Nouvel onglet", "Nueva pestaña"),
        ["menu.newTab"] = P("New tab", "Új lap", "Neuer Tab", "Nouvel onglet", "Nueva pestaña"),
        ["menu.newWindow"] = P("New window", "Új ablak", "Neues Fenster", "Nouvelle fenêtre", "Nueva ventana"),
        ["menu.newPrivateWindow"] = P("New private window", "Új privát ablak", "Neues privates Fenster",
                                      "Nouvelle fenêtre privée", "Nueva ventana privada"),
        ["menu.closeTab"] = P("Close tab", "Lap bezárása", "Tab schließen", "Fermer l'onglet", "Cerrar pestaña"),
        ["menu.bookmark"] = P("Bookmark this page", "Oldal könyvjelzőzése", "Seite als Lesezeichen",
                              "Ajouter aux favoris", "Añadir a marcadores"),
        ["menu.unbookmark"] = P("Remove bookmark", "Könyvjelző törlése", "Lesezeichen entfernen",
                                "Retirer des favoris", "Quitar de marcadores"),
        ["menu.bookmarks"] = P("Bookmarks", "Könyvjelzők", "Lesezeichen", "Favoris", "Marcadores"),
        ["menu.history"] = P("History", "Előzmények", "Verlauf", "Historique", "Historial"),
        ["menu.downloads"] = P("Downloads", "Letöltések", "Downloads", "Téléchargements", "Descargas"),
        ["menu.settings"] = P("Settings", "Beállítások", "Einstellungen", "Paramètres", "Configuración"),
        ["menu.zoomIn"] = P("Zoom in", "Nagyítás", "Vergrößern", "Zoom avant", "Acercar"),
        ["menu.zoomOut"] = P("Zoom out", "Kicsinyítés", "Verkleinern", "Zoom arrière", "Alejar"),
        ["menu.zoomReset"] = P("Reset zoom", "Eredeti méret", "Zoom zurücksetzen", "Rétablir le zoom",
                               "Restablecer el zoom"),
        ["menu.print"] = P("Print...", "Nyomtatás...", "Drucken...", "Imprimer...", "Imprimir..."),
        ["menu.about"] = P("About", "Névjegy", "Über", "À propos de", "Acerca de"),
        ["menu.exit"] = P("Exit", "Kilépés", "Beenden", "Quitter", "Salir"),
        ["menu.empty"] = P("Nothing yet", "Még semmi", "Noch nichts", "Rien pour l'instant", "Nada todavía"),

        ["menu.find"] = P("Find on this page...", "Keresés az oldalon...", "Auf dieser Seite suchen...",
                          "Rechercher dans la page...", "Buscar en esta página..."),

        // The window that remembers nothing
        ["private.badge"] = P("Private", "Privát", "Privat", "Privé", "Privado"),
        ["private.window"] = P("Private window", "Privát ablak", "Privates Fenster", "Fenêtre privée",
                               "Ventana privada"),
        ["private.title"] = P("You are browsing privately", "Privátan böngészel", "Du surfst privat",
                              "Vous naviguez en privé", "Estás navegando en privado"),
        ["private.what"] = P(
            "This window keeps nothing: no history, no download list, and its cookies go when it closes. What you download stays, and so do bookmarks you add.",
            "Ez az ablak semmit nem őriz meg: se előzményt, se letöltési listát, a sütijei pedig a bezárásakor eltűnnek. Amit letöltesz, az megmarad, és a felvett könyvjelzők is.",
            "Dieses Fenster merkt sich nichts: keinen Verlauf, keine Downloadliste, und seine Cookies verschwinden beim Schließen. Was du herunterlädst, bleibt, und die Lesezeichen, die du anlegst, ebenso.",
            "Cette fenêtre ne garde rien : pas d'historique, pas de liste de téléchargements, et ses cookies disparaissent à la fermeture. Ce que vous téléchargez reste, tout comme les favoris que vous ajoutez.",
            "Esta ventana no guarda nada: ni historial, ni lista de descargas, y sus cookies desaparecen al cerrarla. Lo que descargues se queda, y también los marcadores que añadas."),
        ["private.notInvisible"] = P(
            "It does not hide you from the sites you visit, from your employer, or from whoever runs the network.",
            "Attól még lát téged a meglátogatott oldal, a munkahelyed és az is, aki a hálózatot üzemelteti.",
            "Vor den besuchten Seiten, vor deinem Arbeitgeber und vor dem Betreiber des Netzwerks verbirgt es dich nicht.",
            "Elle ne vous cache pas des sites que vous visitez, de votre employeur, ni de celui qui gère le réseau.",
            "No te oculta de los sitios que visitas, de tu empresa ni de quien gestiona la red."),

        // Finding words on a page
        ["find.placeholder"] = P("Find on this page", "Keresés az oldalon", "Auf dieser Seite suchen",
                                 "Rechercher dans la page", "Buscar en esta página"),
        ["find.none"] = P("No matches", "Nincs találat", "Keine Treffer", "Aucun résultat", "Sin resultados"),

        // A bookmark's own menu
        ["bookmark.open"] = P("Open", "Megnyitás", "Öffnen", "Ouvrir", "Abrir"),
        ["bookmark.openNewTab"] = P("Open in a new tab", "Megnyitás új lapon", "In neuem Tab öffnen",
                                    "Ouvrir dans un nouvel onglet", "Abrir en una pestaña nueva"),
        ["bookmark.rename"] = P("Rename...", "Átnevezés...", "Umbenennen...", "Renommer...", "Cambiar el nombre..."),
        ["bookmark.remove"] = P("Remove", "Törlés", "Entfernen", "Supprimer", "Quitar"),
        ["bookmark.renameTitle"] = P("Rename bookmark", "Könyvjelző átnevezése", "Lesezeichen umbenennen",
                                     "Renommer le favori", "Cambiar el nombre del marcador"),
        ["bookmark.name"] = P("Name", "Név", "Name", "Nom", "Nombre"),

        // Buttons in dialogs
        ["button.ok"] = P("OK", "OK", "OK", "OK", "Aceptar"),
        ["button.cancel"] = P("Cancel", "Mégse", "Abbrechen", "Annuler", "Cancelar"),
        ["button.close"] = P("Close", "Bezárás", "Schließen", "Fermer", "Cerrar"),

        // The about box
        ["about.title"] = P("About", "Névjegy", "Über", "À propos de", "Acerca de"),
        ["about.tagline"] = P("A portable browser on the Chromium engine.",
                              "Hordozható böngésző a Chromium motorjára építve.",
                              "Ein portabler Browser auf der Chromium-Engine.",
                              "Un navigateur portable bâti sur le moteur Chromium.",
                              "Un navegador portátil sobre el motor Chromium."),
        ["about.version"] = P("Version", "Verzió", "Version", "Version", "Versión"),
        ["about.portable"] = P("Portable: data kept beside the program",
                               "Hordozható: az adatok a program mellett vannak",
                               "Portabel: die Daten liegen neben dem Programm",
                               "Portable : les données sont à côté du programme",
                               "Portátil: los datos se guardan junto al programa"),
        ["about.installed"] = P("Data kept in your user profile",
                                "Az adatok a felhasználói profilodban vannak",
                                "Die Daten liegen in deinem Benutzerprofil",
                                "Les données sont dans votre profil utilisateur",
                                "Los datos se guardan en tu perfil de usuario"),

        // The pages the browser draws itself
        ["page.history"] = P("History", "Előzmények", "Verlauf", "Historique", "Historial"),
        ["page.downloads"] = P("Downloads", "Letöltések", "Downloads", "Téléchargements", "Descargas"),
        ["page.settings"] = P("Settings", "Beállítások", "Einstellungen", "Paramètres", "Configuración"),
        ["page.notFound"] = P("There is no such page.", "Nincs ilyen oldal.", "Diese Seite gibt es nicht.",
                              "Cette page n'existe pas.", "No existe esa página."),
        ["history.search"] = P("Search history", "Keresés az előzményekben", "Verlauf durchsuchen",
                               "Rechercher dans l'historique", "Buscar en el historial"),
        ["history.clear"] = P("Clear all", "Összes törlése", "Alles löschen", "Tout effacer", "Borrar todo"),
        ["history.empty"] = P("Nowhere yet. Pages you visit will be listed here.",
                              "Még sehol. A meglátogatott oldalak itt jelennek meg.",
                              "Noch nirgends. Besuchte Seiten erscheinen hier.",
                              "Nulle part pour l'instant. Les pages visitées apparaîtront ici.",
                              "En ningún sitio todavía. Las páginas que visites aparecerán aquí."),
        ["history.noMatch"] = P("Nothing matches that.", "Erre nincs találat.", "Dazu gibt es nichts.",
                                "Aucun résultat pour cela.", "No hay nada que coincida."),
        ["history.today"] = P("Today", "Ma", "Heute", "Aujourd'hui", "Hoy"),
        ["history.yesterday"] = P("Yesterday", "Tegnap", "Gestern", "Hier", "Ayer"),
        ["history.visits"] = P("visits", "látogatás", "Besuche", "visites", "visitas"),
        ["downloads.empty"] = P("Nothing downloaded yet.", "Még nincs letöltés.", "Noch nichts heruntergeladen.",
                                "Aucun téléchargement pour l'instant.", "Aún no hay descargas."),
        ["downloads.clear"] = P("Clear finished", "Befejezettek törlése", "Abgeschlossene entfernen",
                                "Effacer les terminés", "Borrar las terminadas"),
        ["downloads.reveal"] = P("Show in folder", "Megjelenítés a mappában", "Im Ordner anzeigen",
                                 "Afficher dans le dossier", "Mostrar en la carpeta"),
        ["downloads.cancelled"] = P("Cancelled", "Megszakítva", "Abgebrochen", "Annulé", "Cancelada"),
        ["downloads.interrupted"] = P("Interrupted", "Félbeszakadt", "Unterbrochen", "Interrompu", "Interrumpida"),
        ["downloads.unknownSize"] = P("unknown size", "ismeretlen méret", "unbekannte Größe", "taille inconnue",
                                      "tamaño desconocido"),
        ["downloads.soFar"] = P("so far", "eddig", "bisher", "jusqu'ici", "hasta ahora"),
        ["downloads.of"] = P("of", "ebből", "von", "sur", "de"),

        // The settings page
        ["settings.home"] = P("Home page", "Kezdőlap", "Startseite", "Page d'accueil", "Página de inicio"),
        ["settings.homeHint"] = P("Where the home button and every new tab go.",
                                  "Ide visz a kezdőlap gomb és minden új lap.",
                                  "Dorthin führen die Startseiten-Schaltfläche und jeder neue Tab.",
                                  "C'est là que mènent le bouton d'accueil et chaque nouvel onglet.",
                                  "Adonde llevan el botón de inicio y cada pestaña nueva."),
        ["settings.search"] = P("Search with", "Keresés ezzel", "Suchen mit", "Rechercher avec", "Buscar con"),
        ["settings.searchOther"] = P("Something else", "Valami más", "Etwas anderes", "Autre chose", "Otra cosa"),
        ["settings.searchHint"] = P(
            "Anything typed in the bar that is not an address is searched for. An address of your own needs {0} where the words go.",
            "Amit a sávba írsz és nem cím, arra rákeres. A saját címben oda kell a {0}, ahova a keresett szavak kerülnek.",
            "Alles, was in der Leiste steht und keine Adresse ist, wird gesucht. Eine eigene Adresse braucht {0} an der Stelle, an die die Wörter kommen.",
            "Tout ce qui est tapé dans la barre et n'est pas une adresse est recherché. Une adresse à vous a besoin de {0} là où vont les mots.",
            "Todo lo que escribas en la barra y no sea una dirección se busca. Una dirección propia necesita {0} donde van las palabras."),
        ["settings.appearance"] = P("Appearance", "Megjelenés", "Darstellung", "Apparence", "Apariencia"),
        ["settings.themeSystem"] = P("Follow Windows", "Windows szerint", "Wie Windows", "Comme Windows",
                                     "Según Windows"),
        ["settings.themeLight"] = P("Light", "Világos", "Hell", "Clair", "Claro"),
        ["settings.themeDark"] = P("Dark", "Sötét", "Dunkel", "Sombre", "Oscuro"),
        ["settings.bar"] = P("Show the bookmarks bar", "Könyvjelzősáv megjelenítése", "Lesezeichenleiste anzeigen",
                             "Afficher la barre de favoris", "Mostrar la barra de marcadores"),
        ["settings.language"] = P("Language", "Nyelv", "Sprache", "Langue", "Idioma"),
        ["settings.onStart"] = P("On starting", "Indításkor", "Beim Start", "Au démarrage", "Al iniciar"),
        ["settings.restore"] = P("Open the windows and tabs that were open last time",
                                 "Nyíljon meg az, ami legutóbb nyitva volt",
                                 "Fenster und Tabs vom letzten Mal wieder öffnen",
                                 "Rouvrir les fenêtres et onglets de la dernière fois",
                                 "Abrir las ventanas y pestañas de la última vez"),
        ["settings.clearSession"] = P("Forget what was open", "A legutóbb nyitva volt lapok elfelejtése",
                                      "Vergessen, was offen war", "Oublier ce qui était ouvert",
                                      "Olvidar lo que estaba abierto"),
        ["settings.fromLastTime"] = P("from last time", "a legutóbbi alkalomból", "vom letzten Mal",
                                      "de la dernière fois", "de la última vez"),
        ["settings.tabOne"] = P("tab", "lap", "Tab", "onglet", "pestaña"),
        ["settings.tabMany"] = P("tabs", "lap", "Tabs", "onglets", "pestañas"),
        ["settings.save"] = P("Save", "Mentés", "Speichern", "Enregistrer", "Guardar"),
        ["settings.saved"] = P("Saved.", "Elmentve.", "Gespeichert.", "Enregistré.", "Guardado."),
        ["settings.cleared"] = P("Cleared.", "Törölve.", "Gelöscht.", "Effacé.", "Borrado."),
        ["settings.clear"] = P("Clear", "Törlés", "Löschen", "Effacer", "Borrar"),
        ["settings.clearHistory"] = P("Clear history", "Előzmények törlése", "Verlauf löschen",
                                      "Effacer l'historique", "Borrar el historial"),
        ["settings.clearDownloads"] = P("Clear the download list", "Letöltési lista törlése",
                                        "Downloadliste löschen", "Effacer la liste des téléchargements",
                                        "Borrar la lista de descargas"),
        ["settings.inHistory"] = P("in history", "az előzményekben", "im Verlauf", "dans l'historique",
                                   "en el historial"),
        ["settings.kept"] = P("kept", "elmentve", "gespeichert", "enregistrés", "guardados"),
        ["settings.pageOne"] = P("page", "oldal", "Seite", "page", "página"),
        ["settings.pageMany"] = P("pages", "oldal", "Seiten", "pages", "páginas"),
        ["settings.bookmarkOne"] = P("bookmark", "könyvjelző", "Lesezeichen", "favori", "marcador"),
        ["settings.bookmarkMany"] = P("bookmarks", "könyvjelző", "Lesezeichen", "favoris", "marcadores"),

        // Trouble
        ["error.engine"] = P("The Chromium engine could not start.", "A Chromium motor nem tudott elindulni.",
                             "Die Chromium-Engine konnte nicht starten.",
                             "Le moteur Chromium n'a pas pu démarrer.", "El motor Chromium no pudo iniciarse."),
    };
}
