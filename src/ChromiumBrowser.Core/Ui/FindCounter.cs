using ChromiumBrowser.Core.Localisation;

namespace ChromiumBrowser.Core.Ui;

/// <summary>
/// What the find bar writes beside the box: "3/17", or that there is nothing.
///
/// It is here rather than in the control because it is the one part of finding
/// that can be wrong in a way nobody notices — an engine reports its matches as
/// they are counted, so a search in progress arrives as a count with no active
/// match yet, and a search that found nothing arrives as two zeros that must not
/// be shown as "0/0" in one language and left blank in the other.
/// </summary>
public static class FindCounter
{
    /// <summary>
    /// The text for a result, or nothing at all while the box is empty.
    /// </summary>
    /// <param name="searched">What was typed. Nothing typed means nothing to say.</param>
    /// <param name="count">How many matches the engine has found so far.</param>
    /// <param name="active">Which of them is highlighted, counting from one.</param>
    public static string Text(string searched, int count, int active)
    {
        if (string.IsNullOrEmpty(searched))
        {
            return string.Empty;
        }

        if (count <= 0)
        {
            return Strings.Of("find.none");
        }

        // The engine counts matches before it decides which one is current, so
        // for a moment there are matches and no active one. Showing "0/17" then
        // reads as a bug; the first match is what the engine is about to settle
        // on anyway.
        int at = Math.Clamp(active, 1, count);
        return $"{at}/{count}";
    }
}
