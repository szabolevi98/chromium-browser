using System.Text.Json;
using CefSharp;
using CefSharp.Callback;

namespace ChromiumBrowser.Browser;

/// <summary>Observe media activity without capturing (and thereby diverting) audio output.</summary>
internal sealed class AudioWatcher(Action<bool> changed) : IDevToolsMessageObserver, IDisposable
{
    private readonly Dictionary<string, (bool Playing, bool HasAudio)> _players = [];
    private readonly HashSet<string> _contexts = [];
    private readonly object _gate = new();
    private IRegistration? _registration;
    private bool _disposed;
    private bool _active;

    public void Attach(IBrowser browser)
    {
        lock (_gate)
        {
            if (_disposed || _registration is not null) return;
            IBrowserHost host = browser.GetHost();
            _registration = host.AddDevToolsMessageObserver(this);
            foreach (string domain in new[] { "Media", "WebAudio", "Page" })
                host.ExecuteDevToolsMethod(0, domain + ".enable", "{}");
        }
    }

    public bool OnDevToolsMessage(IBrowser browser, Stream message) => false;
    public void OnDevToolsMethodResult(IBrowser browser, int messageId, bool success, Stream result) { }
    public void OnDevToolsAgentAttached(IBrowser browser) { }
    public void OnDevToolsAgentDetached(IBrowser browser)
    {
        lock (_gate) { _players.Clear(); _contexts.Clear(); Notify(); }
    }

    public void OnDevToolsEvent(IBrowser browser, string method, Stream parameters)
    {
        if (!method.StartsWith("Media.") && !method.StartsWith("WebAudio.context") && method != "Page.frameNavigated") return;
        try
        {
            using JsonDocument document = JsonDocument.Parse(parameters);
            Observe(method, document.RootElement);
        }
        catch (JsonException) { /* Ignore protocol additions this version does not understand. */ }
    }

    internal void Observe(string method, JsonElement data)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (method == "Page.frameNavigated" && data.TryGetProperty("frame", out var frame)
                && !frame.TryGetProperty("parentId", out _))
            {
                _players.Clear();
                _contexts.Clear();
            }
            else if (method is "WebAudio.contextCreated" or "WebAudio.contextChanged"
                && data.TryGetProperty("context", out var context))
            {
                string id = context.GetProperty("contextId").GetString()!;
                if (context.GetProperty("contextState").GetString() == "running"
                    && context.GetProperty("contextType").GetString() == "realtime") _contexts.Add(id);
                else _contexts.Remove(id);
            }
            else if (method == "WebAudio.contextWillBeDestroyed" && data.TryGetProperty("contextId", out var contextId))
                _contexts.Remove(contextId.GetString()!);
            else if (data.TryGetProperty("playerId", out var playerId))
            {
                string id = playerId.GetString()!;
                var player = _players.GetValueOrDefault(id);
                if (method == "Media.playerPropertiesChanged" && data.TryGetProperty("properties", out var properties))
                    foreach (var property in properties.EnumerateArray())
                        if (property.GetProperty("name").GetString() == "kAudioTracks")
                        {
                            string tracks = property.GetProperty("value").GetString() ?? "[]";
                            player.HasAudio = tracks.Trim() is not ("[]" or "" or "null");
                        }
                if (method == "Media.playerEventsAdded" && data.TryGetProperty("events", out var events))
                    foreach (var entry in events.EnumerateArray())
                    {
                        using var value = JsonDocument.Parse(entry.GetProperty("value").GetString() ?? "{}");
                        if (!value.RootElement.TryGetProperty("event", out var name)) continue;
                        if (name.GetString() == "kPlay") player.Playing = true;
                        if (name.GetString() is "kPause" or "kEnded" or "kWebMediaPlayerDestroyed") player.Playing = false;
                    }
                _players[id] = player;
            }
            Notify();
        }
    }

    private void Notify()
    {
        bool active = _contexts.Count > 0 || _players.Values.Any(p => p.Playing && p.HasAudio);
        if (active == _active || _disposed) return;
        _active = active;
        changed(active);
    }

    public void Dispose()
    {
        IRegistration? registration;
        lock (_gate)
        {
            _disposed = true;
            registration = _registration;
            _registration = null;
        }
        registration?.Dispose();
    }
}
