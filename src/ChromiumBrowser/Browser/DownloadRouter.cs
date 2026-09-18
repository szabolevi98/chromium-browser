using CefSharp;
using ChromiumBrowser.Core.Data;

namespace ChromiumBrowser.Browser;

/// <summary>
/// Takes what the engine reports about downloads and puts it in the list.
///
/// Without a handler at all the engine refuses downloads silently, which is the
/// single most confusing thing a browser can do: the link is clicked and nothing
/// whatsoever happens.
///
/// The Save As dialog is Windows', not this program's, because the one place a
/// browser must not be clever is deciding where somebody's file goes. The engine
/// reports progress from its own thread, so everything here hands the work to
/// the window's thread before touching the list a control is drawing from.
/// </summary>
public sealed class DownloadRouter : IDownloadHandler
{
    private readonly DownloadStore _downloads;
    private readonly Action<Action> _onUiThread;
    private readonly Action _changed;

    public DownloadRouter(DownloadStore downloads, Action<Action> onUiThread, Action changed)
    {
        _downloads = downloads;
        _onUiThread = onUiThread;
        _changed = changed;
    }

    public bool CanDownload(IWebBrowser browser, IBrowser cef, string url, string requestMethod) => true;

    public bool OnBeforeDownload(
        IWebBrowser browser,
        IBrowser cef,
        DownloadItem item,
        IBeforeDownloadCallback callback)
    {
        if (!callback.IsDisposed)
        {
            using (callback)
            {
                // An empty path with the dialog asked for lets Windows suggest
                // the name and the folder the user last used.
                callback.Continue(string.Empty, showDialog: true);
            }
        }

        _onUiThread(() =>
        {
            _downloads.Begin(item.Id, item.Url, item.SuggestedFileName, item.TotalBytes);
            _changed();
        });

        return true;
    }

    public void OnDownloadUpdated(
        IWebBrowser browser,
        IBrowser cef,
        DownloadItem item,
        IDownloadItemCallback callback)
    {
        DownloadState? finished =
            item.IsComplete ? DownloadState.Completed
            : item.IsCancelled ? DownloadState.Cancelled
            : item.IsInterrupted ? DownloadState.Interrupted
            : null;

        int id = item.Id;
        long received = item.ReceivedBytes;
        long total = item.TotalBytes;
        string path = item.FullPath ?? string.Empty;

        _onUiThread(() =>
        {
            if (finished is null)
            {
                _downloads.Progressed(id, received, total, path);
            }
            else
            {
                _downloads.Finish(id, finished.Value, received, path);
            }

            _changed();
        });
    }
}
