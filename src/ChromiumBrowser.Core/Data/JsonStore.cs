using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromiumBrowser.Core.Data;

/// <summary>
/// Reads and writes the small lists a browser keeps: bookmarks, history, what
/// has been downloaded.
///
/// Two rules, both of which exist because this file lives on whatever the
/// program was unpacked onto and may be a memory stick pulled out mid-write.
///
/// A save writes a new file beside the old one and then replaces it in one
/// step, so a copy interrupted halfway leaves the previous list intact rather
/// than a truncated one. And a load never throws: a file that is missing,
/// empty, half-written or edited by hand gives an empty list, because losing
/// the bookmarks is bad but refusing to start the browser is worse.
/// </summary>
public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Whatever is in the file, or nothing at all.</summary>
    public static List<T> Load<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<List<T>>(stream, Options) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Writes the list, replacing what was there in a single step.</summary>
    public static void Save<T>(string path, IEnumerable<T> items)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporary = path + ".writing";
            using (FileStream stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, items, Options);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A browser that cannot save its history is still a browser. The
            // alternative is a dialog nobody can act on, every few minutes.
        }
    }
}
