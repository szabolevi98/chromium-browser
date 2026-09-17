namespace ChromiumBrowser.Core;

/// <summary>
/// Every place the product names itself, in one file.
///
/// The name is the likeliest thing about this project to change, so nothing else
/// spells it out: window titles, the about box, the user agent suffix and the
/// folder beside the executable all read it from here.
/// </summary>
public static class Branding
{
    /// <summary>What the window and the about box call it.</summary>
    public const string Name = "Chromium Browser";

    /// <summary>One line, for the about box and the installer-less download page.</summary>
    public const string Tagline = "A portable browser on the Chromium engine.";

    /// <summary>The folder name used beside the executable, and in the user profile.</summary>
    public const string FolderName = "ChromiumBrowser";
}
