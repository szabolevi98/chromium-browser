using CefSharp;
using CefSharp.Enums;
using CefSharp.Structs;

namespace ChromiumBrowser.Browser;

/// <summary>
/// Listens for the one thing this browser wants from the engine's display
/// notifications: the address of the page's icon.
///
/// The interface covers a dozen things a browser could react to — the cursor
/// shape, tooltips, the status message, console output — and this window reacts
/// to none of them yet. They are here as empty methods rather than as an
/// abstract base class because the engine requires the whole interface, and an
/// empty method that says so is clearer than one inherited from somewhere else.
/// </summary>
public sealed class FaviconWatcher : IDisplayHandler
{
    private readonly Action<string> _onFaviconUrl;

    public FaviconWatcher(Action<string> onFaviconUrl) => _onFaviconUrl = onFaviconUrl;

    public void OnFaviconUrlChange(IWebBrowser browser, IBrowser cef, IList<string> urls)
    {
        // The page may name several; the first is the one it prefers.
        if (urls.Count > 0)
        {
            _onFaviconUrl(urls[0]);
        }
    }

    public void OnAddressChanged(IWebBrowser browser, AddressChangedEventArgs args)
    {
    }

    public bool OnAutoResize(IWebBrowser browser, IBrowser cef, CefSharp.Structs.Size size) => false;

    public bool OnCursorChange(IWebBrowser browser, IBrowser cef, IntPtr cursor, CursorType type, CursorInfo info) =>
        false;

    public void OnTitleChanged(IWebBrowser browser, TitleChangedEventArgs args)
    {
    }

    public void OnFullscreenModeChange(IWebBrowser browser, IBrowser cef, bool fullscreen)
    {
    }

    public void OnLoadingProgressChange(IWebBrowser browser, IBrowser cef, double progress)
    {
    }

    public bool OnTooltipChanged(IWebBrowser browser, ref string text) => false;

    public void OnStatusMessage(IWebBrowser browser, StatusMessageEventArgs args)
    {
    }

    public bool OnConsoleMessage(IWebBrowser browser, ConsoleMessageEventArgs args) => false;
}
