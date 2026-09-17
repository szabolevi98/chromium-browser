namespace ChromiumBrowser.Core.Profile;

/// <summary>Where a run of the browser keeps everything it remembers.</summary>
/// <param name="Path">The folder itself.</param>
/// <param name="IsPortable">
/// Whether it sits beside the executable. A portable copy carries its history,
/// bookmarks and settings with it on the stick it was unpacked onto; a copy in a
/// folder it may not write to has to fall back to the user profile instead.
/// </param>
public readonly record struct ProfileLocation(string Path, bool IsPortable);

/// <summary>
/// Decides where that folder is.
///
/// Portable means portable: nothing is installed, nothing is written to the
/// registry, and by default nothing leaves the folder the program was unpacked
/// into. Program Files and read-only media are the exception rather than the
/// rule, and the fallback exists so that a copy dropped there still runs.
/// </summary>
public static class ProfileLocator
{
    /// <summary>The folder holding the data, beside the executable where possible.</summary>
    /// <param name="executableDirectory">Where the running program lives.</param>
    /// <param name="localApplicationData">The user's own application data folder.</param>
    /// <param name="canWrite">Whether a folder accepts a write; separated out so it can be tested.</param>
    public static ProfileLocation Resolve(
        string executableDirectory,
        string localApplicationData,
        Func<string, bool> canWrite)
    {
        return canWrite(executableDirectory)
            ? new ProfileLocation(Path.Combine(executableDirectory, "Data"), true)
            : new ProfileLocation(Path.Combine(localApplicationData, Branding.FolderName), false);
    }

    /// <summary>Whether a folder really accepts a write, answered by trying one.</summary>
    public static bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $".write-{Guid.NewGuid():N}");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
