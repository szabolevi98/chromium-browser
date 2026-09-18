using System.Net;
using System.Text;
using ChromiumBrowser.Core.Data;
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

    private readonly HistoryStore _history;
    private readonly DownloadStore _downloads;
    private readonly Action<string> _reveal;

    public InternalPages(HistoryStore history, DownloadStore downloads, Action<string> reveal)
    {
        _history = history;
        _downloads = downloads;
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
            _ => Document("Not found", "<p class=\"empty\">There is no such page.</p>"),
        };
    }

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
            <h1>History</h1>
            <form class="bar" method="get" action="{Scheme}://history">
              <input class="search" type="search" name="q" placeholder="Search history"
                     value="{Escape(search)}" autofocus>
              <a class="button" href="{Scheme}://history?clear=all">Clear all</a>
            </form>
            """);

        if (entries.Count == 0)
        {
            body.Append(search.Length > 0
                ? "<p class=\"empty\">Nothing matches that.</p>"
                : "<p class=\"empty\">Nowhere yet. Pages you visit will be listed here.</p>");
            return Document("History", body.ToString());
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
                  {(entry.Visits > 1 ? $"<span class=\"count\">{entry.Visits} visits</span>" : string.Empty)}
                </li>
                """);
        }

        body.Append("</ul>");
        return Document("History", body.ToString());
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
            <h1>Downloads</h1>
            <div class="bar">
              <a class="button" href="{Scheme}://downloads?clear=all">Clear finished</a>
            </div>
            """);

        if (records.Count == 0)
        {
            body.Append("<p class=\"empty\">Nothing downloaded yet.</p>");
            return Document("Downloads", body.ToString());
        }

        body.Append("<ul class=\"list\">");
        foreach (DownloadRecord record in records)
        {
            string state = record.State switch
            {
                DownloadState.Completed => Size(record.ReceivedBytes),
                DownloadState.InProgress => record.Progress is { } fraction
                    ? $"{fraction:P0} of {Size(record.TotalBytes)}"
                    : $"{Size(record.ReceivedBytes)} so far",
                DownloadState.Cancelled => "Cancelled",
                _ => "Interrupted",
            };

            string action = record.Path.Length > 0 && record.State == DownloadState.Completed
                ? $"<a class=\"link\" href=\"{Scheme}://downloads?reveal={Uri.EscapeDataString(record.Path)}\">Show in folder</a>"
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
        return Document("Downloads", body.ToString());
    }

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
        <= 0 => "unknown size",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
    };

    /// <summary>
    /// Titles come from web pages, so every one of them reaches the page through
    /// this. A page called <c>&lt;script&gt;</c> is a page somebody made to see
    /// whether anyone was paying attention.
    /// </summary>
    private static string Escape(string text) => WebUtility.HtmlEncode(text);

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
