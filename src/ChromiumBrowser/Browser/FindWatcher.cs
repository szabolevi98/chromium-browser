using CefSharp;
using CefSharp.Structs;

namespace ChromiumBrowser.Browser;

/// <summary>What the engine says about a search in progress.</summary>
/// <param name="Count">How many matches have been found so far.</param>
/// <param name="Active">Which match is highlighted, counting from one.</param>
/// <param name="Final">Whether the engine has finished counting.</param>
public readonly record struct FindResult(int Count, int Active, bool Final);

/// <summary>
/// Listens for the engine's answers to a search.
///
/// The count arrives in instalments rather than once: the engine reports what it
/// has found while it is still looking, and says which report is the last. The
/// bar can show every instalment, because a counter that climbs while the page
/// is searched is how a browser tells you it is working.
///
/// Called on the engine's thread, so the window is handed the result rather than
/// touched from here.
/// </summary>
public sealed class FindWatcher : IFindHandler
{
    private readonly Action<FindResult> _report;

    public FindWatcher(Action<FindResult> report) => _report = report;

    public void OnFindResult(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        int identifier,
        int count,
        Rect selectionRect,
        int activeMatchOrdinal,
        bool finalUpdate) =>
        _report(new FindResult(count, activeMatchOrdinal, finalUpdate));
}
