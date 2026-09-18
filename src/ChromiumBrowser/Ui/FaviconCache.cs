using System.Collections.Concurrent;
using ChromiumBrowser.Core;

namespace ChromiumBrowser.Ui;

/// <summary>
/// Fetches the little icon a site names for itself, and remembers it.
///
/// Sites name the icon by address, so every tab showing the same site would ask
/// for the same file: the cache is per address and shared by the whole window.
/// The download is deliberately tight-fisted — a short timeout and a small
/// ceiling on the size — because an icon is never important enough to hold
/// anything up or to be worth megabytes, and the address comes from the page
/// rather than from this program.
///
/// The bytes may be a PNG, a GIF, or a Windows icon file with several sizes in
/// it, so both decoders are tried. What cannot be drawn is usually an SVG —
/// GitHub and plenty of others name one now — and for those the site's own
/// <c>/favicon.ico</c> is asked for instead, which those same sites still keep.
/// Anything that decodes to neither is simply a tab without an icon.
/// </summary>
public static class FaviconCache
{
    private static readonly ConcurrentDictionary<string, Image?> Icons = new();

    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromSeconds(8),
        DefaultRequestHeaders =
        {
            // Wikipedia and others refuse a request that does not say who is
            // asking, with a 403 and no explanation. An icon quietly failing to
            // appear is exactly the kind of fault nobody ever tracks down, so
            // this browser names itself.
            { "User-Agent", $"{Branding.Name}/{BrowserVersion} (+https://github.com/szabolevi98/chromium-browser)" },
        },
    };

    private static string BrowserVersion =>
        typeof(FaviconCache).Assembly.GetName().Version?.ToString(2) ?? "2.0";

    private const int MaximumBytes = 512 * 1024;

    /// <summary>The icon for an address, downloading it the first time it is asked for.</summary>
    public static async Task<Image?> GetAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Icons.TryGetValue(url, out Image? cached))
        {
            return cached;
        }

        Image? icon = await DownloadAsync(url).ConfigureAwait(false);

        // The page named something this program cannot draw. Nearly every site
        // that does that still answers the old address, so it is worth one more
        // request before giving up on the icon.
        if (icon is null && RootIcon(url) is { } fallback)
        {
            icon = await DownloadAsync(fallback).ConfigureAwait(false);
        }

        Icons[url] = icon;
        return icon;
    }

    /// <summary>
    /// The address every site used to keep its icon at, for a site whose named
    /// icon could not be used. Nothing for an address that is already it, so a
    /// site without one is asked once rather than twice.
    /// </summary>
    public static string? RootIcon(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? address)
            || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        string root = $"{address.Scheme}://{address.Authority}/favicon.ico";
        return string.Equals(root, url, StringComparison.OrdinalIgnoreCase) ? null : root;
    }

    private static async Task<Image?> DownloadAsync(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            using HttpResponseMessage response = await Client
                .GetAsync(address, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode
                || response.Content.Headers.ContentLength > MaximumBytes)
            {
                return null;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using MemoryStream buffer = new();
            await CopyAtMostAsync(stream, buffer, MaximumBytes).ConfigureAwait(false);
            buffer.Position = 0;

            return Decode(buffer);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    /// <summary>Copies up to a limit, and gives up rather than reading a page pretending to be an icon.</summary>
    private static async Task CopyAtMostAsync(Stream source, Stream target, int limit)
    {
        byte[] chunk = new byte[8192];
        int copied = 0;
        while (copied < limit)
        {
            int read = await source.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, limit - copied)))
                .ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            await target.WriteAsync(chunk.AsMemory(0, read)).ConfigureAwait(false);
            copied += read;
        }
    }

    private static Image? Decode(MemoryStream bytes)
    {
        try
        {
            // An icon file holds several sizes; asking for 16 by 16 picks the one
            // drawn for this size rather than a shrunken larger one.
            using Icon icon = new(bytes, new Size(16, 16));
            return icon.ToBitmap();
        }
        catch (ArgumentException)
        {
            // Not an icon file, so it is one of the image formats.
        }

        try
        {
            bytes.Position = 0;
            using Image image = Image.FromStream(bytes);
            return new Bitmap(image, new Size(16, 16));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
