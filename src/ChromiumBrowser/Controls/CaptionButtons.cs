using ChromiumBrowser.Ui;

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

    public CaptionButtons()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
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

    private int ButtonAt(int x)
    {
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
            Invalidate();
        }
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
