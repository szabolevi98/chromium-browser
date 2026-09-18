using System.Text;
using CefSharp;

namespace ChromiumBrowser.Browser;

/// <summary>
/// Answers requests for <c>browser://</c> addresses with pages this program
/// draws itself.
///
/// The engine asks for these on one of its own threads, while the lists being
/// rendered belong to the window's. So the rendering is handed to the window and
/// waited for, which is safe here because the window is never itself waiting on
/// the engine, and because building a page out of a few hundred list entries is
/// over before it can be noticed.
/// </summary>
public sealed class InternalSchemeFactory : ISchemeHandlerFactory
{
    private readonly Func<Control?> _window;
    private readonly InternalPages _pages;

    /// <param name="window">
    /// Looked up rather than held, because the engine wants its schemes
    /// registered before it starts, and the window only exists afterwards.
    /// </param>
    public InternalSchemeFactory(Func<Control?> window, InternalPages pages)
    {
        _window = window;
        _pages = pages;
    }

    public IResourceHandler? Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? address))
        {
            return null;
        }

        Control? window = _window();
        if (window is null || window.IsDisposed || !window.IsHandleCreated)
        {
            return null;
        }

        string html = (string)window.Invoke(() => _pages.Render(address));

        return ResourceHandler.FromString(html, Encoding.UTF8, includePreamble: true, mimeType: "text/html");
    }
}
