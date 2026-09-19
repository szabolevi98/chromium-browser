using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChromiumBrowser;

/// <summary>
/// Keeps one copy of the browser per set of data, and hands a second launch's
/// address to the copy already running.
///
/// Two copies over one profile folder would both write the same history file and
/// both hold the same engine cache, and the one that closed last would win. The
/// visible half is worse: double-clicking a link while the browser is open would
/// start a second browser rather than opening a tab.
///
/// The name is taken from the profile folder rather than from the program, which
/// is what keeps the portable promise. A copy on a memory stick and a copy in
/// the user's profile are two browsers with two sets of data, and neither
/// should be handing addresses to the other.
/// </summary>
public static class SingleInstance
{
    public sealed record OpenRequest(string? Url, bool IsPrivate = false);
    /// <summary>
    /// Claims the right to be the one running copy, or returns nothing when
    /// another already has it.
    /// </summary>
    public static Mutex? Claim(string profilePath)
    {
        Mutex mutex = new(initiallyOwned: true, $"Local\\ChromiumBrowser.{KeyFor(profilePath)}", out bool mine);

        if (mine)
        {
            return mutex;
        }

        mutex.Dispose();
        return null;
    }

    /// <summary>
    /// Hands an address to the copy already running. Says whether it was taken:
    /// a copy that is in the middle of closing has its mutex but no longer
    /// answers, and the launch that could not reach it is better off starting a
    /// browser than doing nothing at all.
    /// </summary>
    public static bool Send(string profilePath, string? url) => SendRequest(profilePath, new(url));

    public static bool SendRequest(string profilePath, OpenRequest request)
    {
        try
        {
            using NamedPipeClientStream pipe = new(".", PipeFor(profilePath), PipeDirection.Out);

            // Long enough for a busy machine, short enough that nobody watches a
            // window that is not coming.
            pipe.Connect(2000);

            using StreamWriter writer = new(pipe, new UTF8Encoding(false));
            writer.WriteLine(JsonSerializer.Serialize(request));
            writer.Flush();
            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Listens for later launches. Each one is a single line: an address, or
    /// nothing at all when the program was started with no argument, which still
    /// means "show me the browser".
    /// </summary>
    public static void Listen(string profilePath, Action<string?> handed) =>
        ListenRequests(profilePath, request => handed(request.Url));

    public static OpenRequest Decode(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return new(null);
        if (!line.StartsWith('{')) return new(line.Trim()); // older copies send a plain URL
        try { return JsonSerializer.Deserialize<OpenRequest>(line) ?? new(null); }
        catch (JsonException) { return new(null); }
    }

    public static void ListenRequests(string profilePath, Action<OpenRequest> handed)
    {
        Thread thread = new(() =>
        {
            while (true)
            {
                try
                {
                    // A fresh server for each caller, rather than one server
                    // reused: a pipe that has been read to the end cannot be
                    // listened on again.
                    using NamedPipeServerStream pipe = new(
                        PipeFor(profilePath),
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1);

                    pipe.WaitForConnection();

                    using StreamReader reader = new(pipe, new UTF8Encoding(false));
                    string? line = reader.ReadLine();
                    handed(Decode(line));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // One caller going away is not a reason to stop listening
                    // for the next.
                }
            }
        })
        {
            IsBackground = true,
            Name = "handover",
        };

        thread.Start();
    }

    private static string PipeFor(string profilePath) => $"ChromiumBrowser.{KeyFor(profilePath)}";

    /// <summary>
    /// A short name for a folder. Hashed rather than used directly because a
    /// path contains backslashes and colons, which a pipe name may not, and
    /// because the name would otherwise tell anyone who looked where somebody's
    /// data is kept.
    /// </summary>
    private static string KeyFor(string profilePath)
    {
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(profilePath)).ToLowerInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
