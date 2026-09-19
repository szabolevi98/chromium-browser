using CefSharp;
using CefSharp.Handler;

namespace ChromiumBrowser.Browser;

/// <summary>Keep internal requests tied to the owning browser, including incognito contexts.</summary>
internal sealed class InternalRequestHandler(ISchemeHandlerFactory factory) : RequestHandler
{
    protected override IResourceRequestHandler? GetResourceRequestHandler(IWebBrowser webBrowser,
        IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload,
        string requestInitiator, ref bool disableDefaultHandling)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? uri) || uri.Scheme != InternalPages.Scheme)
            return null;
        disableDefaultHandling = true;
        return new InternalResourceRequest(factory);
    }

    private sealed class InternalResourceRequest(ISchemeHandlerFactory factory) : ResourceRequestHandler
    {
        protected override IResourceHandler? GetResourceHandler(IWebBrowser webBrowser, IBrowser browser,
            IFrame frame, IRequest request)
        {
            return factory.Create(browser, frame, InternalPages.Scheme, request);
        }
    }
}
