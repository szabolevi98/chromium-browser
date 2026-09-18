namespace ChromiumBrowser.Core.Web;

/// <summary>
/// What to do with what somebody typed in the one bar that takes both.
///
/// A browser's address bar is the only text field people trust to accept either
/// an address or a question, and the rule for telling them apart is guesswork
/// dressed up as logic. The guesses here are the ones that match what people
/// expect:
///
/// something with a scheme is an address; something with a space in it is a
/// search, because host names cannot have spaces; something with a dot and no
/// space is an address, which is what makes <c>levente.net</c> work; a name with
/// a port after it is an address, which is what makes <c>localhost:3000</c>
/// work; and everything else is a search.
///
/// The awkward case is a single word with no dot, like <c>wiki</c>, which could
/// be a machine on a company network. It is treated as a search, because for
/// everyone not on such a network it nearly always is one.
/// </summary>
public static class AddressParser
{
    /// <summary>The address to load for what was typed.</summary>
    /// <param name="typed">What was in the bar.</param>
    /// <param name="searchTemplate">Where a search goes, with <c>{0}</c> for the words.</param>
    /// <param name="internalScheme">The browser's own scheme, which is an address like any other.</param>
    public static string Parse(string typed, string searchTemplate, string internalScheme = "browser")
    {
        string text = typed.Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (HasScheme(text, "http") || HasScheme(text, "https") || HasScheme(text, "file")
            || HasScheme(text, internalScheme))
        {
            return text;
        }

        return LooksLikeAddress(text) ? "https://" + text : Search(text, searchTemplate);
    }

    /// <summary>Whether this would be loaded rather than searched for.</summary>
    public static bool LooksLikeAddress(string text)
    {
        if (text.Contains(' ') || text.Length == 0)
        {
            return false;
        }

        // A host and port, which is how a local server is usually reached.
        int colon = text.IndexOf(':');
        if (colon > 0 && colon < text.Length - 1
            && int.TryParse(text.AsSpan(colon + 1).ToString().Split('/')[0], out int port)
            && port is > 0 and <= 65535)
        {
            return true;
        }

        if (text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // A dot with something on both sides of it. The something after it has
        // to look like the end of a name rather than the start of a sentence, so
        // a trailing dot or a dot followed by a slash is not enough.
        int dot = text.IndexOf('.');
        return dot > 0
            && dot < text.Length - 1
            && text[dot + 1] != '/'
            && !text.StartsWith('.');
    }

    private static string Search(string text, string template) =>
        string.Format(
            template.Contains("{0}", StringComparison.Ordinal) ? template : "https://www.google.com/search?q={0}",
            Uri.EscapeDataString(text));

    private static bool HasScheme(string text, string scheme) =>
        text.StartsWith(scheme + "://", StringComparison.OrdinalIgnoreCase);
}
