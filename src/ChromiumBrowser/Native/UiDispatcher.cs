using System.Collections.Concurrent;

namespace ChromiumBrowser.Native;

/// <summary>A message-only window whose lifetime is independent of every browser form.</summary>
public sealed class UiDispatcher : NativeWindow, IDisposable
{
    private const int Dispatch = 0x8001;
    private readonly ConcurrentQueue<Action> _pending = new();
    private readonly object _gate = new();
    public bool IsDisposed { get; private set; }

    public UiDispatcher() => CreateHandle(new CreateParams { Parent = new IntPtr(-3), Caption = "Browser dispatcher" });

    public bool Post(Action action)
    {
        lock (_gate)
        {
            if (IsDisposed) return false;
            _pending.Enqueue(action);
            return Win32.PostMessageW(Handle, Dispatch, IntPtr.Zero, IntPtr.Zero);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Dispatch)
        {
            while (_pending.TryDequeue(out Action? action)) action();
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (IsDisposed) return;
            IsDisposed = true;
        }
        // Let pending resource callbacks see disposal and cancel themselves.
        while (_pending.TryDequeue(out Action? action)) action();
        DestroyHandle();
    }
}
