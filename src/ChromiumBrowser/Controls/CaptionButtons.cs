using ChromiumBrowser.Ui;
using ChromiumBrowser.Native;
using ChromiumBrowser.Core.Localisation;

namespace ChromiumBrowser.Controls;

/// <summary>
/// Minimise, maximise and close, drawn here because the window no longer has a
/// title bar for Windows to draw them on.
///
/// The shapes are lines rather than a glyph font: Segoe's icon fonts changed
/// between Windows versions and the glyphs land on different baselines, whereas
/// a one-pixel line is the same everywhere and stays crisp at any scaling.
/// </summary>
public sealed class CaptionButtons : Control
{
    private int _hover = -1;
    private int _keyboardButton;
    private readonly ToolTip _tip = new();

    public CaptionButtons()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        TabStop = true;
    }

    public event EventHandler? MinimiseClicked;

    public event EventHandler? MaximiseClicked;

    public event EventHandler? CloseClicked;

    /// <summary>Whether to draw the restore shape instead of the maximise one.</summary>
    public bool IsMaximised { get; set; }

    private float UiScale => DeviceDpi / 96f;

    private int ButtonWidth => (int)(46 * UiScale);

    /// <summary>The width the three buttons need, which the window uses to lay itself out.</summary>
    public int PreferredWidth => ButtonWidth * 3;
    public Rectangle MaximiseBounds => new(ButtonWidth, 0, ButtonWidth, Height);

    private string ButtonName(int index) => Strings.Of(index switch
    {
        0 => "window.minimise", 1 => IsMaximised ? "window.restore" : "window.maximise", _ => "window.close",
    });

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_NCHITTEST && MaximiseBounds.Contains(PointToClient(Win32.ScreenPoint(m.LParam))))
        {
            m.Result = Win32.HTTRANSPARENT;
            return;
        }
        base.WndProc(ref m);
    }

    public void SetNativeHover(bool hovered)
    {
        int next = hovered ? 1 : -1;
        if (_hover == next) return;
        _hover = next;
        Invalidate();
    }

    private int ButtonAt(int x)
    {
        if (x < 0) return -1;
        int index = x / ButtonWidth;
        return index is >= 0 and < 3 ? index : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hover = ButtonAt(e.X);
        if (hover != _hover)
        {
            _hover = hover;
            _tip.SetToolTip(this, hover >= 0 ? ButtonName(hover) : string.Empty);
            Invalidate();
        }
    }

    private void InvokeButton(int index)
    {
        switch (index)
        {
            case 0: MinimiseClicked?.Invoke(this, EventArgs.Empty); break;
            case 1: MaximiseClicked?.Invoke(this, EventArgs.Empty); break;
            case 2: CloseClicked?.Invoke(this, EventArgs.Empty); break;
        }
    }

    protected override bool IsInputKey(Keys keys) =>
        (keys & Keys.KeyCode) is Keys.Left or Keys.Right || base.IsInputKey(keys);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left) _keyboardButton = Math.Max(0, _keyboardButton - 1);
        else if (e.KeyCode == Keys.Right) _keyboardButton = Math.Min(2, _keyboardButton + 1);
        else if (e.KeyCode is Keys.Enter or Keys.Space) InvokeButton(_keyboardButton);
        else return;
        e.Handled = e.SuppressKeyPress = true;
        Invalidate();
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new AccessibleActions(this,
        () => Enumerable.Range(0, 3).Select(i => new AccessibleActions.Item(ButtonName(i),
            new Rectangle(i * ButtonWidth, 0, ButtonWidth, Height), () => InvokeButton(i))).ToArray());

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != -1)
        {
            _hover = -1;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        switch (ButtonAt(e.X))
        {
            case 0: MinimiseClicked?.Invoke(this, EventArgs.Empty); break;
            case 1: MaximiseClicked?.Invoke(this, EventArgs.Empty); break;
            case 2: CloseClicked?.Invoke(this, EventArgs.Empty); break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Palette palette = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(palette.Chrome);

        for (int index = 0; index < 3; index++)
        {
            Rectangle rect = new(index * ButtonWidth, 0, ButtonWidth, Height);
            bool hot = index == _hover;
            if (Focused && index == _keyboardButton) ControlPaint.DrawFocusRectangle(g, rect);

            if (hot)
            {
                using SolidBrush brush = new(index == 2 ? palette.CloseHover : palette.Hover);
                g.FillRectangle(brush, rect);
            }

            Color ink = hot && index == 2 ? Color.White : palette.Text;
            int size = (int)(10 * UiScale);
            int left = rect.X + ((rect.Width - size) / 2);
            int top = (rect.Height - size) / 2;

            using Pen pen = new(ink, 1f * UiScale);
            switch (index)
            {
                case 0:
                    g.DrawLine(pen, left, top + (size / 2), left + size, top + (size / 2));
                    break;

                case 1 when IsMaximised:
                    // Restore: the window behind, and the one in front of it.
                    int step = (int)(2 * UiScale);
                    g.DrawRectangle(pen, left, top + step, size - step, size - step);
                    g.DrawLine(pen, left + step, top + step, left + step, top);
                    g.DrawLine(pen, left + step, top, left + size, top);
                    g.DrawLine(pen, left + size, top, left + size, top + size - step);
                    break;

                case 1:
                    g.DrawRectangle(pen, left, top, size, size);
                    break;

                case 2:
                    g.DrawLine(pen, left, top, left + size, top + size);
                    g.DrawLine(pen, left + size, top, left, top + size);
                    break;
            }
        }
    }
}
