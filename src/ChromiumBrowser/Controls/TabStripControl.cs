using System.Drawing.Drawing2D;
using ChromiumBrowser.Core.Ui;
using ChromiumBrowser.Ui;

namespace ChromiumBrowser.Controls;

/// <summary>One tab, as far as the strip is concerned.</summary>
public sealed class TabItem
{
    public string Title { get; set; } = "New tab";

    public bool IsLoading { get; set; }

    /// <summary>The site's own icon, once it has arrived.</summary>
    public Image? Icon { get; set; }
}

/// <summary>
/// The row of tabs, drawn rather than assembled.
///
/// Windows Forms has a tab control, and every browser that used it looked like a
/// settings dialog: square tabs with a raised border, a system font, no room for
/// a close button and no idea what to do when thirty of them are open. So the
/// strip here is a single control that paints itself from
/// <see cref="TabStripLayout"/>, which keeps the arithmetic testable and leaves
/// this file to the part that has to look right.
/// </summary>
public sealed class TabStripControl : Control
{
    private readonly List<TabItem> _tabs = [];

    private int _selected;
    private int _hoverTab = -1;
    private bool _hoverClose;
    private bool _hoverNewTab;
    private int _scrollOffset;

    // A drag only starts once the pointer has moved far enough that it was
    // plainly a drag and not a click that wobbled.
    private const int DragThreshold = 6;
    private int _pressedTab = -1;
    private Point _pressedAt;
    private bool _dragging;
    private int _dragOffsetInTab;
    private int _dragX;

    public TabStripControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9f);
    }

    public IReadOnlyList<TabItem> Tabs => _tabs;

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(0, _tabs.Count - 1));
            if (clamped == _selected)
            {
                return;
            }

            _selected = clamped;
            EnsureVisible(clamped);
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedIndexChanged;

    public event EventHandler<int>? TabCloseRequested;

    public event EventHandler? NewTabRequested;

    /// <summary>Raised after a tab has been dragged to a new position.</summary>
    public event EventHandler<(int From, int To)>? TabMoved;

    /// <summary>
    /// Pressed on the empty part of the strip. That area belongs to the window
    /// rather than to the tabs: it is where the window is dragged from, now that
    /// there is no title bar to drag.
    /// </summary>
    public event EventHandler? EmptyAreaPressed;

    /// <summary>Double-clicked on that same empty part, which maximises.</summary>
    public event EventHandler? EmptyAreaDoubleClicked;

    /// <summary>Where the window may be dragged from: the empty part of the strip.</summary>
    public Rectangle DragArea
    {
        get
        {
            TabStripBounds strip = CurrentLayout();
            int used = strip.NewTabX + NewTabWidth;
            return used >= Width ? Rectangle.Empty : new Rectangle(used, 0, Width - used, Height);
        }
    }

    private float UiScale => DeviceDpi / 96f;

    private int NewTabWidth => (int)(36 * UiScale);

    public void AddTab(TabItem tab, bool select = true)
    {
        _tabs.Add(tab);
        if (select)
        {
            _selected = _tabs.Count - 1;
            EnsureVisible(_selected);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }

        Invalidate();
    }

    public void RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
        {
            return;
        }

        _tabs.RemoveAt(index);
        if (_tabs.Count == 0)
        {
            _selected = 0;
        }
        else if (_selected > index || _selected == _tabs.Count)
        {
            _selected = Math.Max(0, _selected - 1);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }

        _hoverTab = -1;
        Invalidate();
    }

    /// <summary>Redraws one tab after its title, icon or loading state changed.</summary>
    public void Refresh(int index)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            Invalidate();
        }
    }

    /// <summary>A little air before the first tab, so it does not touch the window edge.</summary>
    private int LeftInset => (int)(6 * UiScale);

    private TabStripBounds CurrentLayout()
    {
        TabStripBounds strip = TabStripLayout.Compute(
            Math.Max(1, _tabs.Count), Width - LeftInset, NewTabWidth, _scrollOffset);

        TabBounds[] shifted = new TabBounds[strip.Tabs.Length];
        for (int index = 0; index < shifted.Length; index++)
        {
            shifted[index] = strip.Tabs[index] with { X = strip.Tabs[index].X + LeftInset };
        }

        return strip with { Tabs = shifted, NewTabX = strip.NewTabX + LeftInset };
    }

    private void EnsureVisible(int index)
    {
        _scrollOffset = TabStripLayout.ScrollToShow(
            index, Math.Max(1, _tabs.Count), Width, NewTabWidth, _scrollOffset);
    }

    // ------------------------------------------------------------------ input

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_pressedTab >= 0 && !_dragging
            && Math.Abs(e.X - _pressedAt.X) > DragThreshold * UiScale)
        {
            _dragging = true;
        }

        if (_dragging)
        {
            _dragX = e.X - _dragOffsetInTab;
            TabStripBounds strip = CurrentLayout();
            int centre = _dragX + (strip.Tabs[_pressedTab].Width / 2);
            int target = TabStripLayout.DropIndex(strip, _pressedTab, centre);
            if (target != _pressedTab)
            {
                TabItem moved = _tabs[_pressedTab];
                _tabs.RemoveAt(_pressedTab);
                _tabs.Insert(target, moved);
                TabMoved?.Invoke(this, (_pressedTab, target));
                if (_selected == _pressedTab)
                {
                    _selected = target;
                }

                _pressedTab = target;
            }

            Invalidate();
            return;
        }

        TabStripBounds bounds = CurrentLayout();
        int tab = TabStripLayout.HitTest(bounds, e.X);
        bool close = tab >= 0 && CloseRect(bounds.Tabs[tab]).Contains(e.Location);
        bool newTab = e.X >= bounds.NewTabX && e.X < bounds.NewTabX + NewTabWidth;

        if (tab != _hoverTab || close != _hoverClose || newTab != _hoverNewTab)
        {
            _hoverTab = tab;
            _hoverClose = close;
            _hoverNewTab = newTab;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverTab >= 0 || _hoverNewTab)
        {
            _hoverTab = -1;
            _hoverClose = false;
            _hoverNewTab = false;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        TabStripBounds strip = CurrentLayout();
        int tab = TabStripLayout.HitTest(strip, e.X);

        if (e.Button == MouseButtons.Middle)
        {
            if (tab >= 0)
            {
                TabCloseRequested?.Invoke(this, tab);
            }

            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (e.X >= strip.NewTabX && e.X < strip.NewTabX + NewTabWidth)
        {
            NewTabRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (tab < 0)
        {
            EmptyAreaPressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (CloseRect(strip.Tabs[tab]).Contains(e.Location))
        {
            TabCloseRequested?.Invoke(this, tab);
            return;
        }

        SelectedIndex = tab;
        _pressedTab = tab;
        _pressedAt = e.Location;
        _dragOffsetInTab = e.X - strip.Tabs[tab].X;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressedTab = -1;
        if (_dragging)
        {
            _dragging = false;
            Invalidate();
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && TabStripLayout.HitTest(CurrentLayout(), e.X) < 0)
        {
            EmptyAreaDoubleClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        // The wheel walks the tabs, which is what it does in every browser, and
        // is the only way to reach a tab when thirty of them are open.
        if (_tabs.Count > 1)
        {
            SelectedIndex = Math.Clamp(
                _selected + (e.Delta > 0 ? -1 : 1), 0, _tabs.Count - 1);
        }
    }

    // ----------------------------------------------------------------- drawing

    private Rectangle CloseRect(TabBounds tab)
    {
        int size = (int)(16 * UiScale);
        int margin = (int)(8 * UiScale);
        return new Rectangle(
            tab.X + tab.Width - size - margin,
            (Height - size) / 2,
            size,
            size);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Palette palette = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(palette.Chrome);

        TabStripBounds strip = CurrentLayout();

        for (int index = 0; index < _tabs.Count; index++)
        {
            if (_dragging && index == _pressedTab)
            {
                continue; // drawn last, on top of the others
            }

            DrawTab(g, palette, strip.Tabs[index], index);
        }

        if (_dragging && _pressedTab >= 0)
        {
            TabBounds held = strip.Tabs[_pressedTab] with { X = _dragX };
            DrawTab(g, palette, held, _pressedTab, lifted: true);
        }

        DrawNewTabButton(g, palette, strip.NewTabX);
    }

    private void DrawTab(Graphics g, Palette palette, TabBounds bounds, int index, bool lifted = false)
    {
        bool active = index == _selected;
        bool hovered = index == _hoverTab && !_dragging;
        int inset = (int)(2 * UiScale);
        Rectangle rect = new(bounds.X + inset, inset, bounds.Width - (2 * inset), Height - inset);

        if (rect.Width <= 0)
        {
            return;
        }

        int radius = (int)(9 * UiScale);

        if (active || hovered || lifted)
        {
            using GraphicsPath path = RoundedTop(rect, radius);
            using SolidBrush brush = new(active || lifted ? palette.Surface : palette.Hover);
            g.FillPath(brush, path);
        }
        else if (index > 0 && index - 1 != _hoverTab && index - 1 != _selected)
        {
            // A hairline between neighbouring inactive tabs, which is what tells
            // them apart once they are narrow. It is dropped next to the active
            // or hovered tab, whose own shape already separates them.
            using Pen pen = new(palette.Line);
            int x = bounds.X;
            g.DrawLine(pen, x, (int)(8 * UiScale), x, Height - (int)(8 * UiScale));
        }

        int left = rect.X + (int)(10 * UiScale);
        int iconSize = (int)(16 * UiScale);
        int iconTop = (Height - iconSize) / 2;

        TabItem tab = _tabs[index];
        if (tab.IsLoading)
        {
            DrawSpinner(g, palette, new Rectangle(left, iconTop, iconSize, iconSize));
            left += iconSize + (int)(8 * UiScale);
        }
        else if (tab.Icon is not null)
        {
            g.DrawImage(tab.Icon, new Rectangle(left, iconTop, iconSize, iconSize));
            left += iconSize + (int)(8 * UiScale);
        }
        else
        {
            // A site with no icon of its own still gets the space, so titles do
            // not jump sideways the moment an icon arrives.
            DrawBlankPage(g, palette, new Rectangle(left, iconTop, iconSize, iconSize));
            left += iconSize + (int)(8 * UiScale);
        }

        Rectangle closeRect = CloseRect(bounds);
        int textRight = (active || hovered) ? closeRect.X - (int)(4 * UiScale) : rect.Right - (int)(8 * UiScale);
        Rectangle textRect = new(left, 0, Math.Max(0, textRight - left), Height);

        if (textRect.Width > (int)(12 * UiScale))
        {
            TextRenderer.DrawText(
                g,
                tab.Title,
                Font,
                textRect,
                active ? palette.Text : palette.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPrefix);
        }

        // The close button appears on the current tab and on whichever tab the
        // pointer is over: showing it on all of them turns the strip into a row
        // of crosses, and hiding it on all of them means aiming at a tab twice.
        if ((active || hovered) && rect.Width > (int)(48 * UiScale))
        {
            DrawClose(g, palette, closeRect, hovered && _hoverClose);
        }
    }

    private void DrawClose(Graphics g, Palette palette, Rectangle rect, bool hot)
    {
        if (hot)
        {
            using SolidBrush brush = new(palette.Pressed);
            g.FillEllipse(brush, rect);
        }

        int pad = (int)(4.5f * UiScale);
        using Pen pen = new(hot ? palette.Text : palette.TextMuted, 1.3f * UiScale);
        g.DrawLine(pen, rect.Left + pad, rect.Top + pad, rect.Right - pad, rect.Bottom - pad);
        g.DrawLine(pen, rect.Right - pad, rect.Top + pad, rect.Left + pad, rect.Bottom - pad);
    }

    private void DrawNewTabButton(Graphics g, Palette palette, int x)
    {
        int size = (int)(26 * UiScale);
        Rectangle rect = new(x + (int)(4 * UiScale), (Height - size) / 2, size, size);

        if (_hoverNewTab)
        {
            using SolidBrush brush = new(palette.Hover);
            using GraphicsPath path = Rounded(rect, (int)(7 * UiScale));
            g.FillPath(brush, path);
        }

        int pad = (int)(8 * UiScale);
        using Pen pen = new(palette.TextMuted, 1.4f * UiScale);
        g.DrawLine(pen, rect.Left + pad, rect.Top + (rect.Height / 2), rect.Right - pad, rect.Top + (rect.Height / 2));
        g.DrawLine(pen, rect.Left + (rect.Width / 2), rect.Top + pad, rect.Left + (rect.Width / 2), rect.Bottom - pad);
    }

    /// <summary>A sheet of paper with its corner turned, for a site with no icon.</summary>
    private void DrawBlankPage(Graphics g, Palette palette, Rectangle rect)
    {
        int fold = (int)(5 * UiScale);
        rect.Inflate(-(int)(2 * UiScale), -(int)(1 * UiScale));

        using Pen pen = new(palette.TextMuted, 1.2f * UiScale);
        Point[] outline =
        [
            new(rect.Left, rect.Top),
            new(rect.Right - fold, rect.Top),
            new(rect.Right, rect.Top + fold),
            new(rect.Right, rect.Bottom),
            new(rect.Left, rect.Bottom),
        ];

        g.DrawPolygon(pen, outline);
        g.DrawLine(pen, rect.Right - fold, rect.Top, rect.Right - fold, rect.Top + fold);
        g.DrawLine(pen, rect.Right - fold, rect.Top + fold, rect.Right, rect.Top + fold);
    }

    /// <summary>A quarter-circle arc that turns while a page is loading.</summary>
    private void DrawSpinner(Graphics g, Palette palette, Rectangle rect)
    {
        int angle = (int)(Environment.TickCount / 3 % 360);
        using Pen pen = new(palette.Accent, 1.8f * UiScale);
        rect.Inflate(-(int)(2 * UiScale), -(int)(2 * UiScale));
        g.DrawArc(pen, rect, angle, 100);
    }

    private static GraphicsPath RoundedTop(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - (radius * 2), rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - (radius * 2), rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - (radius * 2), rect.Bottom - (radius * 2), radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - (radius * 2), radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        return path;
    }
}
