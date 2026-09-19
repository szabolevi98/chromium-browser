using System.Reflection;
using System.Text.Json;
using ChromiumBrowser.Browser;
using ChromiumBrowser.Controls;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Core.Ui;

namespace ChromiumBrowser.UiTests;

internal static partial class Program
{
    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    private static object? Invoke(object owner, string name, params object?[] args) =>
        owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);

    private static void CheckAuditRegressions()
    {
        bool playing = false;
        using (AudioWatcher audio = new(value => playing = value))
        {
            using var properties = JsonDocument.Parse("""{"playerId":"one","properties":[{"name":"kAudioTracks","value":"[{\"codec\":\"opus\"}]"}]}""");
            using var play = JsonDocument.Parse("""{"playerId":"one","events":[{"value":"{\"event\":\"kPlay\"}"}]}""");
            using var pause = JsonDocument.Parse("""{"playerId":"one","events":[{"value":"{\"event\":\"kPause\"}"}]}""");
            audio.Observe("Media.playerPropertiesChanged", properties.RootElement);
            audio.Observe("Media.playerEventsAdded", play.RootElement);
            Check("audit: media playback activates audio indicator", playing);
            audio.Observe("Media.playerEventsAdded", pause.RootElement);
            Check("audit: pausing clears media activity", !playing);
        }
        using (TabStripControl strip = new() { Size = new(500, 40) })
        {
            for (int i = 0; i < 30; i++) strip.AddTab(new() { Title = $"Tab {i}" });
            strip.SelectedIndex = 0;
            Check("audit: crowded strip keeps a drag target", strip.DragArea.Width >= 72);
            Check("audit: crowded strip keeps plus visible", strip.ClientRectangle.Contains(strip.NewTabBounds));
            bool added = false;
            strip.NewTabRequested += (_, _) => added = true;
            Click(strip, strip.NewTabBounds.Left + 10, 20);
            Check("audit: visible plus is not intercepted by an offscreen tab", added);
            strip.SelectedIndex = 29;
            strip.Width = 360;
            TabStripBounds layout = (TabStripBounds)Invoke(strip, "CurrentLayout")!;
            Check("audit: resize keeps selected tab visible", layout.Tabs[29].X >= 0
                && layout.Tabs[29].X + layout.Tabs[29].Width <= strip.NewTabBounds.Left);
            for (int i = 29; i >= 8; i--) strip.RemoveTab(i);
            layout = (TabStripBounds)Invoke(strip, "CurrentLayout")!;
            TabBounds selected = layout.Tabs[strip.SelectedIndex];
            Check("audit: repeated close clamps scroll", selected.X >= 0 && selected.X + selected.Width <= strip.NewTabBounds.Left);
            int request = -1;
            strip.TabMenuRequested += (_, r) => request = r.Index;
            Click(strip, selected.X + 20, 20, MouseButtons.Right);
            Check("audit: right click identifies its tab", request == strip.SelectedIndex);
            TabItem kept = strip.Tabs[strip.SelectedIndex];
            strip.MoveTab(strip.SelectedIndex, 0);
            Check("audit: moving a tab preserves identity", strip.SelectedIndex == 0 && ReferenceEquals(strip.Tabs[0], kept));
            Check("audit: tabs expose accessible selected state",
                strip.AccessibilityObject.GetChild(0)!.State.HasFlag(AccessibleStates.Selected));
            strip.AccessibilityObject.GetChild(2)!.DoDefaultAction();
            Check("audit: accessible tab action selects it", strip.SelectedIndex == 1);
            int count = strip.Tabs.Count;
            Send(strip, "OnKeyDown", new KeyEventArgs(Keys.End));
            Check("audit: keyboard reaches last tab", strip.SelectedIndex == count - 1);
        }

        Check("audit: Ctrl+Shift+T reopens rather than creates",
            ShortcutHandler.Match((int)Keys.T, true, true) == BrowserCommand.ReopenTab);
        Check("audit: AltGr shortcuts are not intercepted",
            new[] { Keys.T, Keys.L, Keys.R, Keys.N, Keys.D0, Keys.Oemplus }
                .All(key => ShortcutHandler.Match((int)key, true, false, true) is null));
        Check("audit: Alt+Left/Right navigate", ShortcutHandler.Match((int)Keys.Left, false, false, true) == BrowserCommand.Back
            && ShortcutHandler.Match((int)Keys.Right, false, false, true) == BrowserCommand.Forward);
        Check("audit: hard refresh variants agree", ShortcutHandler.Match((int)Keys.F5, true, false) == BrowserCommand.HardReload
            && ShortcutHandler.Match((int)Keys.R, true, true) == BrowserCommand.HardReload);
        Check("audit: direct tab keys", ShortcutHandler.Match((int)Keys.D1, true, false) == BrowserCommand.Tab1
            && ShortcutHandler.Match((int)Keys.D8, true, false) == BrowserCommand.Tab8
            && ShortcutHandler.Match((int)Keys.D9, true, false) == BrowserCommand.LastTab);
        Check("audit: full screen and address focus keys", ShortcutHandler.Match((int)Keys.F11, false, false) == BrowserCommand.Fullscreen
            && ShortcutHandler.Match((int)Keys.F6, false, false) == BrowserCommand.FocusAddress);

        using (ToolbarControl toolbar = new() { Size = new(900, 44) })
        {
            TextBox address = toolbar.Controls.OfType<TextBox>().Single();
            toolbar.ShowAddress("https://example.test/original");
            Send(address, "OnGotFocus", EventArgs.Empty);
            Send(address, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
            Check("audit: first address click selects all", address.SelectionLength == address.TextLength);
            address.Select(5, 0);
            Send(address, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
            Check("audit: second address click preserves caret", address.SelectionStart == 5 && address.SelectionLength == 0);
            address.Text = "unsent edit";
            toolbar.CancelAddressEdit();
            Check("audit: cancelled editing restores URL", address.Text == "https://example.test/original");
            toolbar.SetSuggestions(["https://one.test", "https://two.test", "https://one.test"]);
            Check("audit: suggestions are deduplicated", address.AutoCompleteCustomSource.Count == 2);
            Check("audit: address field is accessible alongside buttons", toolbar.AccessibilityObject.GetChildCount() == 6
                && toolbar.AccessibilityObject.GetChild(5)!.Name!.Length > 0);
            bool reload = false, stop = false;
            toolbar.ReloadRequested += (_, _) => reload = true;
            toolbar.StopRequested += (_, _) => stop = true;
            toolbar.AccessibilityObject.GetChild(2)!.DoDefaultAction();
            toolbar.IsLoading = true;
            toolbar.AccessibilityObject.GetChild(2)!.DoDefaultAction();
            Check("audit: accessible reload action follows loading state", reload && stop);
        }

        string directory = Path.Combine(Path.GetTempPath(), $"cb-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            DownloadStore normal = new(Path.Combine(directory, "downloads.json"));
            DownloadStore secret = new(string.Empty);
            normal.Begin(1, "https://normal.test/file", "normal-file", 100);
            secret.Begin(2, "https://private.test/file", "private-file", 100);
            InternalPages pages = new(new HistoryStore(string.Empty), normal, new SettingsStore(string.Empty),
                new BookmarkStore(string.Empty), new SessionStore(string.Empty), _ => { });
            string publicHtml = pages.Render(new("browser://downloads"));
            string privateHtml = pages.WithDownloads(secret).Render(new("browser://downloads"));
            Check("audit: private downloads use their own list", privateHtml.Contains("private-file") && !privateHtml.Contains("normal-file"));
            Check("audit: normal downloads exclude private records", publicHtml.Contains("normal-file") && !publicHtml.Contains("private-file"));
            Check("audit: downloads poll canonical URL without replaying actions", privateHtml.Contains("setInterval")
                && privateHtml.Contains("fetch('browser://downloads'") && privateHtml.Contains("replaceState"));
            string? command = null;
            secret.SetControl(2, value => command = value, () => { });
            Guid key = secret.All[0].Key;
            pages.WithDownloads(secret).Render(new($"browser://downloads?id={key}&command=pause"));
            Check("audit: download action targets private store", command == "pause" && secret.All[0].IsPaused && !normal.All[0].IsPaused);
            secret.Finish(2, DownloadState.Interrupted, 12, "");
            string? retried = null;
            pages.WithDownloads(secret).Render(new($"browser://downloads?id={key}&command=retry"), url => retried = url);
            Check("audit: retry uses stored URL", retried == "https://private.test/file");
            Check("audit: persisted downloads exclude private records", !File.ReadAllText(Path.Combine(directory, "downloads.json")).Contains("private-file"));
        }
        finally { Directory.Delete(directory, recursive: true); }

        string channel = Path.Combine(Path.GetTempPath(), $"cb-private-handover-{Guid.NewGuid():N}");
        SingleInstance.OpenRequest? received = null;
        using ManualResetEventSlim arrived = new(false);
        SingleInstance.ListenRequests(channel, request => { received = request; arrived.Set(); });
        SingleInstance.SendRequest(channel, new("https://secret.test", true));
        Check("audit: private flag survives a real named pipe", arrived.Wait(TimeSpan.FromSeconds(3))
            && received is { IsPrivate: true, Url: "https://secret.test" });
        Check("audit: legacy handover remains compatible", SingleInstance.Decode("https://old.test") is { IsPrivate: false, Url: "https://old.test" });
    }
}
