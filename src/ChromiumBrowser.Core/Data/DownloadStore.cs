namespace ChromiumBrowser.Core.Data;

/// <summary>How a download ended, or that it has not.</summary>
public enum DownloadState
{
    InProgress,
    Completed,
    Cancelled,

    /// <summary>Stopped without finishing, which includes the browser being closed mid-download.</summary>
    Interrupted,
}

/// <summary>One download, as the list shows it.</summary>
public sealed record DownloadRecord
{
    public required string Url { get; init; }

    public required string FileName { get; init; }

    /// <summary>Where it was saved; empty until the engine has decided.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>What the server said the size would be, or zero when it did not say.</summary>
    public long TotalBytes { get; init; }

    public long ReceivedBytes { get; init; }

    public DownloadState State { get; init; } = DownloadState.InProgress;

    public DateTimeOffset Started { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset? Finished { get; init; }

    /// <summary>How far along, between nothing and one, or null when the size is unknown.</summary>
    public double? Progress => TotalBytes > 0 ? Math.Clamp((double)ReceivedBytes / TotalBytes, 0, 1) : null;
}

/// <summary>
/// What has been downloaded, newest first.
///
/// The engine gives each download a number for the life of the process, so that
/// number is the key while it runs and is not written down: what survives a
/// restart is the list of files, not the engine's bookkeeping.
///
/// A download that was still running when the browser closed is loaded back as
/// interrupted rather than as still running. Anything else would show a
/// progress bar creeping nowhere for a transfer that stopped when the process
/// did, which is exactly the sort of lie a download list must not tell.
/// </summary>
public sealed class DownloadStore
{
    private readonly string _path;
    private readonly List<DownloadRecord> _records;
    private readonly Dictionary<int, DownloadRecord> _live = [];

    public DownloadStore(string path)
    {
        _path = path;
        _records = JsonStore.Load<DownloadRecord>(path);

        for (int index = 0; index < _records.Count; index++)
        {
            if (_records[index].State == DownloadState.InProgress)
            {
                _records[index] = _records[index] with { State = DownloadState.Interrupted };
            }
        }
    }

    public IReadOnlyList<DownloadRecord> All => _records;

    /// <summary>Notes a download the engine has just begun.</summary>
    public DownloadRecord Begin(int id, string url, string fileName, long totalBytes)
    {
        DownloadRecord record = new()
        {
            Url = url,
            FileName = fileName,
            TotalBytes = totalBytes,
        };

        _live[id] = record;
        _records.Insert(0, record);
        Save();
        return record;
    }

    /// <summary>Moves one along. Nothing is written to disk for progress alone.</summary>
    public void Progressed(int id, long receivedBytes, long totalBytes, string path)
    {
        if (!_live.TryGetValue(id, out DownloadRecord? record))
        {
            return;
        }

        Replace(id, record with
        {
            ReceivedBytes = receivedBytes,
            TotalBytes = totalBytes > 0 ? totalBytes : record.TotalBytes,
            Path = string.IsNullOrEmpty(path) ? record.Path : path,
        });
    }

    /// <summary>Ends one, which is worth writing down.</summary>
    public void Finish(int id, DownloadState state, long receivedBytes, string path)
    {
        if (!_live.TryGetValue(id, out DownloadRecord? record))
        {
            return;
        }

        Replace(id, record with
        {
            State = state,
            ReceivedBytes = receivedBytes,
            Path = string.IsNullOrEmpty(path) ? record.Path : path,
            Finished = DateTimeOffset.Now,
        });

        _live.Remove(id);
        Save();
    }

    public void Clear()
    {
        if (_records.Count == 0)
        {
            return;
        }

        _records.RemoveAll(r => r.State != DownloadState.InProgress);
        Save();
    }

    private void Replace(int id, DownloadRecord updated)
    {
        int at = _records.IndexOf(_live[id]);
        if (at >= 0)
        {
            _records[at] = updated;
        }

        _live[id] = updated;
    }

    private void Save() => JsonStore.Save(_path, _records);
}
