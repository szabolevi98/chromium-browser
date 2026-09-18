using System.Text;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Localisation;
using ChromiumBrowser.Ui;

namespace ChromiumBrowser.Browser;

/// <summary>
/// The browser's own pages: <c>browser://history</c> and
/// <c>browser://downloads</c>.
///
/// They are pages rather than dialogs because that is what they are in every
/// browser worth copying: they open in a tab, they can be bookmarked, the back
/// button works on them, and the list scrolls the way a page scrolls rather than
/// the way a grid control does.
///
/// There is no scripting in them and no bridge into the program. Anything that
/// acts — clearing the list, opening a file's folder — is a plain link back to
/// the same scheme with a query on it, which this class reads and carries out
/// before drawing the page again. A form with a text box does the searching, by
/// the same route. That keeps the pages readable, and keeps the surface between
/// a web page and this program down to what can be written in an address.
/// </summary>
public sealed class InternalPages
{
    public const string Scheme = "browser";

    /// <summary>Where the words go in a search address, as text rather than as a format.</summary>
    private const string WordsPlaceholder = "{0}";

    private readonly HistoryStore _history;
    private readonly DownloadStore _downloads;
    private readonly SettingsStore _settings;
    private readonly BookmarkStore _bookmarks;
    private readonly SessionStore _session;
    private readonly Action<string> _reveal;

    public InternalPages(
        HistoryStore history,
        DownloadStore downloads,
        SettingsStore settings,
        BookmarkStore bookmarks,
        SessionStore session,
        Action<string> reveal)
    {
        _session = session;
        _history = history;
        _downloads = downloads;
        _settings = settings;
        _bookmarks = bookmarks;
        _reveal = reveal;
    }

    /// <summary>The page for an address, after doing whatever the address asked for.</summary>
    public string Render(Uri address)
    {
        Dictionary<string, string> query = ParseQuery(address.Query);
        string page = address.Host.Length > 0 ? address.Host : address.AbsolutePath.Trim('/');

        return page switch
        {
            "history" => History(query),
            "downloads" => Downloads(query),
            "settings" => SettingsPage(query),
            "private" => PrivatePage(),
            _ => Document(Strings.Of("page.notFound"), $"<p class=\"empty\">{Strings.Of("page.notFound")}</p>"),
        };
    }

    /// <summary>
    /// What a private window opens on. It says what the window does not keep,
    /// and — the part every browser has learned to spell out — what it does not
    /// do, because "private" reads to most people as "invisible".
    /// </summary>
    private static string PrivatePage() =>
        Document(Strings.Of("private.window"), $"""
            <h1>{Strings.Of("private.title")}</h1>
            <p class="lead">{Strings.Of("private.what")}</p>
            <p class="empty">{Strings.Of("private.notInvisible")}</p>
            """);

    private string History(Dictionary<string, string> query)
    {
        if (query.ContainsKey("clear"))
        {
            _history.ClearAll();
        }

        query.TryGetValue("q", out string? search);
        search ??= string.Empty;

        IReadOnlyList<HistoryEntry> entries = search.Length > 0
            ? _history.Search(search, 500)
            : _history.All;

        StringBuilder body = new();
        body.Append($"""
            <h1>{Strings.Of("page.history")}</h1>
            <form class="bar" method="get" action="{Scheme}://history">
              <input class="search" type="search" name="q" placeholder="{Strings.Of("history.search")}"
                     value="{Escape(search)}" autofocus>
              <a class="button" href="{Scheme}://history?clear=all">{Strings.Of("history.clear")}</a>
            </form>
            """);

        if (entries.Count == 0)
        {
            body.Append($"<p class=\"empty\">{Strings.Of(search.Length > 0 ? "history.noMatch" : "history.empty")}</p>");
            return Document(Strings.Of("page.history"), body.ToString());
        }

        body.Append("<ul class=\"list\">");
        DateTimeOffset? day = null;
        foreach (HistoryEntry entry in entries)
        {
            DateTimeOffset date = entry.LastVisit.Date;
            if (day != date)
            {
                day = date;
                body.Append($"<li class=\"day\">{DayName(entry.LastVisit)}</li>");
            }

            body.Append($"""
                <li>
                  <span class="time">{entry.LastVisit:HH:mm}</span>
                  <a href="{Escape(entry.Url)}">{Escape(entry.Title)}</a>
                  <span class="host">{Escape(HostOf(entry.Url))}</span>
                  {(entry.Visits > 1 ? $"<span class=\"count\">{entry.Visits} {Strings.Of("history.visits")}</span>" : string.Empty)}
                </li>
                """);
        }

        body.Append("</ul>");
        return Document(Strings.Of("page.history"), body.ToString());
    }

    private string Downloads(Dictionary<string, string> query)
    {
        if (query.ContainsKey("clear"))
        {
            _downloads.Clear();
        }

        if (query.TryGetValue("reveal", out string? path) && path.Length > 0)
        {
            _reveal(path);
        }

        IReadOnlyList<DownloadRecord> records = _downloads.All;

        StringBuilder body = new();
        body.Append($"""
            <h1>{Strings.Of("page.downloads")}</h1>
            <div class="bar">
              <a class="button" href="{Scheme}://downloads?clear=all">{Strings.Of("downloads.clear")}</a>
            </div>
            """);

        if (records.Count == 0)
        {
            body.Append($"<p class=\"empty\">{Strings.Of("downloads.empty")}</p>");
            return Document(Strings.Of("page.downloads"), body.ToString());
        }

        body.Append("<ul class=\"list\">");
        foreach (DownloadRecord record in records)
        {
            string state = record.State switch
            {
                DownloadState.Completed => Size(record.ReceivedBytes),
                DownloadState.InProgress => record.Progress is { } fraction
                    ? $"{fraction:P0} {Strings.Of("downloads.of")} {Size(record.TotalBytes)}"
                    : $"{Size(record.ReceivedBytes)} {Strings.Of("downloads.soFar")}",
                DownloadState.Cancelled => Strings.Of("downloads.cancelled"),
                _ => Strings.Of("downloads.interrupted"),
            };

            string action = record.Path.Length > 0 && record.State == DownloadState.Completed
                ? $"<a class=\"link\" href=\"{Scheme}://downloads?reveal={Uri.EscapeDataString(record.Path)}\">{Strings.Of("downloads.reveal")}</a>"
                : string.Empty;

            body.Append($"""
                <li>
                  <span class="file">{Escape(record.FileName)}</span>
                  <span class="host">{Escape(HostOf(record.Url))}</span>
                  <span class="state">{state}</span>
                  {action}
                </li>
                """);
        }

        body.Append("</ul>");
        return Document(Strings.Of("page.downloads"), body.ToString());
    }

    private string SettingsPage(Dictionary<string, string> query)
    {
        string saved = string.Empty;

        if (query.ContainsKey("save"))
        {
            // A form sends nothing at all for an unticked box, so the presence of
            // the marker is what says the form was sent, and the absence of the
            // box is what says it was unticked.
            Settings current = _settings.Current;
            _settings.Save(current with
            {
                HomePage = query.GetValueOrDefault("home", current.HomePage).Trim(),
                SearchEngine = query.GetValueOrDefault("engine", current.SearchEngine),
                CustomSearchTemplate = query.GetValueOrDefault("custom", current.CustomSearchTemplate).Trim(),
                Theme = Enum.TryParse(query.GetValueOrDefault("theme"), true, out ThemeChoice theme)
                    ? theme
                    : current.Theme,
                ShowBookmarksBar = query.ContainsKey("bar"),
                RestoreSession = query.ContainsKey("restore"),
                Language = query.GetValueOrDefault("language", current.Language),
            });

            Strings.Use(_settings.Current.Language);

            saved = $"<p class=\"saved\">{Strings.Of("settings.saved")}</p>";
        }

        if (query.TryGetValue("forget", out string? what))
        {
            switch (what)
            {
                case "history": _history.ClearAll(); break;
                case "downloads": _downloads.Clear(); break;
                case "session": _session.Clear(); break;
            }

            saved = $"<p class=\"saved\">{Strings.Of("settings.cleared")}</p>";
        }

        Settings settings = _settings.Current;

        StringBuilder engines = new();
        foreach (SearchEngine engine in SearchEngines.All)
        {
            engines.Append($"<option value=\"{Escape(engine.Name)}\"{Selected(engine.Name, settings.SearchEngine)}>"
                + $"{Escape(engine.Name)}</option>");
        }

        engines.Append($"<option value=\"Custom\"{Selected("Custom", settings.SearchEngine)}>{Strings.Of("settings.searchOther")}</option>");

        string body = $"""
            <h1>{Strings.Of("page.settings")}</h1>
            {saved}
            <form method="get" action="{Scheme}://settings">
              <input type="hidden" name="save" value="1">

              <section>
                <label for="home">{Strings.Of("settings.home")}</label>
                <input id="home" class="field" type="text" name="home" value="{Escape(settings.HomePage)}">
                <p class="hint">{Strings.Of("settings.homeHint")}</p>
              </section>

              <section>
                <label for="engine">{Strings.Of("settings.search")}</label>
                <select id="engine" class="field" name="engine">{engines}</select>
                <input class="field" type="text" name="custom" placeholder="https://example.com/?q={WordsPlaceholder}"
                       value="{Escape(settings.CustomSearchTemplate)}">
                <p class="hint">{string.Format(Strings.Of("settings.searchHint"), WordsPlaceholder)}</p>
              </section>

              <section>
                <label for="theme">{Strings.Of("settings.appearance")}</label>
                <select id="theme" class="field" name="theme">
                  <option value="System"{Selected("System", settings.Theme.ToString())}>{Strings.Of("settings.themeSystem")}</option>
                  <option value="Light"{Selected("Light", settings.Theme.ToString())}>{Strings.Of("settings.themeLight")}</option>
                  <option value="Dark"{Selected("Dark", settings.Theme.ToString())}>{Strings.Of("settings.themeDark")}</option>
                </select>
                <label for="language" class="after">{Strings.Of("settings.language")}</label>
                <select id="language" class="field" name="language">
                  <option value="en"{Selected("en", settings.Language)}>English</option>
                  <option value="hu"{Selected("hu", settings.Language)}>Magyar</option>
                </select>

                <label class="tick"><input type="checkbox" name="bar" value="1"
                  {(settings.ShowBookmarksBar ? "checked" : string.Empty)}> {Strings.Of("settings.bar")}</label>
              </section>

              <section>
                <label>{Strings.Of("settings.onStart")}</label>
                <label class="tick"><input type="checkbox" name="restore" value="1"
                  {(settings.RestoreSession ? "checked" : string.Empty)}> {Strings.Of("settings.restore")}</label>
              </section>

              <button class="button primary" type="submit">{Strings.Of("settings.save")}</button>
            </form>

            <section>
              <label>{Strings.Of("settings.clear")}</label>
              <p class="hint">{Count(_history.All.Count, "settings.page")} {Strings.Of("settings.inHistory")},
                 {Count(_bookmarks.All.Count, "settings.bookmark")} {Strings.Of("settings.kept")},
                 {Count(_session.Windows.Sum(window => window.Tabs.Count), "settings.tab")} {Strings.Of("settings.fromLastTime")}.</p>
              <a class="button" href="{Scheme}://settings?forget=history">{Strings.Of("settings.clearHistory")}</a>
              <a class="button" href="{Scheme}://settings?forget=downloads">{Strings.Of("settings.clearDownloads")}</a>
              <a class="button" href="{Scheme}://settings?forget=session">{Strings.Of("settings.clearSession")}</a>
            </section>
            """;

        return Document(Strings.Of("page.settings"), body);
    }

    /// <summary>
    /// "1 page" rather than "1 pages". English changes the noun and Hungarian
    /// does not, which is why both plurals are phrases in the table rather than
    /// an "s" added here.
    /// </summary>
    private static string Count(int number, string key) =>
        $"{number} {Strings.Of(number == 1 ? key + "One" : key + "Many")}";

    private static string Selected(string value, string current) =>
        string.Equals(value, current, StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty;

    /// <summary>
    /// The page around the content, in the colours the window is using. An
    /// internal page that stays white when the browser is dark is worse than no
    /// internal page at all.
    /// </summary>
    private static string Document(string title, string body)
    {
        Palette palette = Theme.Current;

        string Colour(Color colour) => $"#{colour.R:x2}{colour.G:x2}{colour.B:x2}";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <title>{{Escape(title)}}</title>
            <style>
              :root {
                color-scheme: {{(Theme.IsDark ? "dark" : "light")}};
                --page: {{Colour(palette.Chrome)}};
                --card: {{Colour(palette.Surface)}};
                --hover: {{Colour(palette.Hover)}};
                --text: {{Colour(palette.Text)}};
                --muted: {{Colour(palette.TextMuted)}};
                --line: {{Colour(palette.Line)}};
                --accent: {{Colour(palette.Accent)}};
              }
              * { box-sizing: border-box; }
              body {
                margin: 0; padding: 48px 32px 64px; background: var(--page); color: var(--text);
                font: 14px/1.5 "Segoe UI", system-ui, sans-serif;
              }
              main { max-width: 860px; margin: 0 auto; }
              h1 { font-size: 26px; font-weight: 600; margin: 0 0 24px; }
              .bar { display: flex; gap: 12px; margin-bottom: 20px; align-items: center; }
              .search {
                flex: 1; padding: 10px 14px; border-radius: 999px; border: 1px solid var(--line);
                background: var(--card); color: var(--text); font: inherit; outline: none;
              }
              .search:focus { border-color: var(--accent); }
              .button, .link {
                /* Inline by default, and an inline box's padding does not push
                   the line apart, so the buttons would sit on top of the text
                   above them. */
                display: inline-block; margin-top: 6px;
                padding: 9px 16px; border-radius: 999px; border: 1px solid var(--line);
                background: var(--card); color: var(--text); text-decoration: none; white-space: nowrap;
              }
              .button:hover, .link:hover { background: var(--hover); }
              .list { list-style: none; margin: 0; padding: 0; }
              .list li {
                display: flex; align-items: baseline; gap: 14px; padding: 11px 14px;
                border-radius: 10px;
              }
              .list li:hover { background: var(--card); }
              .list li.day {
                margin-top: 22px; color: var(--muted); font-weight: 600; text-transform: uppercase;
                letter-spacing: .06em; font-size: 12px; border-bottom: 1px solid var(--line);
                border-radius: 0; padding-left: 0;
              }
              .list li.day:hover { background: none; }
              .time, .count, .state { color: var(--muted); font-variant-numeric: tabular-nums; }
              .host { color: var(--muted); font-size: 13px; }
              .file { font-weight: 600; }
              a { color: var(--text); text-decoration: none; }
              .list a:hover { text-decoration: underline; text-decoration-color: var(--accent); }
              .list li > a:first-of-type { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
              .empty { color: var(--muted); padding: 40px 0; }
              .lead { font-size: 16px; max-width: 62ch; }
              section { margin: 0 0 28px; }
              /* What follows the form is a different subject, and a rule says so. */
              form + section { margin-top: 36px; padding-top: 28px; border-top: 1px solid var(--line); }
              label { display: block; font-weight: 600; margin-bottom: 8px; }
              .field {
                width: 100%; max-width: 520px; padding: 10px 14px; margin-bottom: 8px;
                border-radius: 10px; border: 1px solid var(--line); background: var(--card);
                color: var(--text); font: inherit; outline: none;
              }
              .field:focus { border-color: var(--accent); }
              .hint { color: var(--muted); margin: 6px 0 0; font-size: 13px; max-width: 520px; }
              .tick { font-weight: 400; display: flex; gap: 8px; align-items: center; margin-top: 12px; }
              .after { margin-top: 18px; }
              .tick input { accent-color: var(--accent); }
              button.button { cursor: pointer; font: inherit; }
              .primary { border-color: var(--accent); }
              .saved { color: var(--accent); margin: -10px 0 18px; }
              .link { font-size: 13px; padding: 6px 12px; }
            </style>
            </head>
            <body><main>{{body}}</main></body>
            </html>
            """;
    }

    private static string DayName(DateTimeOffset when)
    {
        DateTimeOffset today = DateTimeOffset.Now.Date;
        return when.Date == today
            ? "Today"
            : when.Date == today.AddDays(-1)
                ? "Yesterday"
                : when.ToString("dddd, d MMMM yyyy");
    }

    private static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? address) ? address.Host : string.Empty;

    private static string Size(long bytes) => bytes switch
    {
        <= 0 => Strings.Of("downloads.unknownSize"),
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
    };

    /// <summary>
    /// Titles come from web pages, so every one of them reaches the page through
    /// this. A page called <c>&lt;script&gt;</c> is a page somebody made to see
    /// whether anyone was paying attention.
    ///
    /// Only the five characters that can change what the markup means are
    /// touched. The framework's own encoder also turns every accented letter
    /// into a numeric entity, which is unnecessary in a page that says it is
    /// UTF-8 and turns "Beállítások" into something unreadable in the source.
    /// </summary>
    private static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&#39;", StringComparison.Ordinal);

    private static Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            string key = equals < 0 ? pair : pair[..equals];
            string value = equals < 0 ? string.Empty : pair[(equals + 1)..];
            values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return values;
    }
}
