using System.Text;
using CefSharp;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Native;

namespace ChromiumBrowser.Browser;

/// <summary>
/// Answers requests for <c>browser://</c> addresses with pages this program
/// draws itself.
///
/// The engine asks for these on one of its own threads, while the lists being
/// rendered belong to the window's. Rendering is posted asynchronously so a
/// simultaneous browser creation or shutdown cannot deadlock CEF's IO thread.
/// </summary>
public sealed class InternalSchemeFactory : ISchemeHandlerFactory
{
    private readonly Func<UiDispatcher?> _window;
    private readonly InternalPages _pages;

    /// <param name="window">
    /// Looked up rather than held, because the engine wants its schemes
    /// registered before it starts, and the window only exists afterwards.
    /// </param>
    public InternalSchemeFactory(Func<UiDispatcher?> window, InternalPages pages)
    {
        _window = window;
        _pages = pages;
    }

    public InternalSchemeFactory WithDownloads(DownloadStore downloads) => new(_window, _pages.WithDownloads(downloads));

    public IResourceHandler? Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? address))
        {
            return null;
        }

        UiDispatcher? window = _window();
        if (window is null || window.IsDisposed)
        {
            return null;
        }

        return new PageResponse(window, () => _pages.Render(address, url => browser.GetHost().StartDownload(url)));
    }

    private sealed class PageResponse(UiDispatcher dispatcher, Func<string> render) : ResourceHandler
    {
        public override CefReturnValue ProcessRequestAsync(IRequest request, ICallback callback)
        {
            try
            {
                bool posted = dispatcher.Post(() =>
                {
                    using (callback)
                    {
                        if (callback.IsDisposed) return;
                        if (dispatcher.IsDisposed) { callback.Cancel(); return; }
                        byte[] bytes = Encoding.UTF8.GetBytes(render());
                        MimeType = "text/html";
                        Charset = "utf-8";
                        Stream = new MemoryStream(bytes, writable: false);
                        AutoDisposeStream = true;
                        ResponseLength = bytes.Length;
                        callback.Continue();
                    }
                });
                if (posted) return CefReturnValue.ContinueAsync;
                callback.Dispose();
                return CefReturnValue.Cancel;
            }
            catch (InvalidOperationException)
            {
                callback.Dispose();
                return CefReturnValue.Cancel;
            }
        }
    }
}
