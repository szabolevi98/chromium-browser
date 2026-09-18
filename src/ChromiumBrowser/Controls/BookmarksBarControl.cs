using System.Drawing.Drawing2D;
using ChromiumBrowser.Core.Data;
using ChromiumBrowser.Ui;
using ChromiumBrowser.Core.Localisation;

namespace ChromiumBrowser.Controls;

/// <summary>What was asked of a bookmark.</summary>
public enum BookmarkAction
{
    Open,
    OpenInNewTab,
    Rename,
    Remove,
}

/// <summary>
/// The row of bookmarks under the toolbar.
///
/// Each one is as wide as its title needs and no wider, up to a limit, because a
/// bar of equal-width buttons wastes the space that lets eight bookmarks fit
/// where five would. What does not fit goes behind a chevron at the end rather
/// than being cut off mid-word: a bookmark that is half visible is a bookmark
/// nobody can click with confidence.
/// </summary>
public sealed class BookmarksBarControl : Control
{
    private const int MaximumItemWidth = 200;
    private const int Padding = 9;
    private const int IconSize = 16;
    private const int IconGap = 7;

    /// <summary>
    /// Measuring and drawing have to agree, and they only do when both are told
    /// to leave the same space around the text.
    /// </summary>
    private const TextFormatFlags TextFlags =
        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

    private readonly List<(Bookmark Bookmark, Rectangle Bounds)> _items = [];
    private IReadOnlyList<Bookmark> _bookmarks = [];
    private int _hover = -1;
    private bool _hoverOverflow;
    private int _firstHidden = -1;
    private bool _measured;

    public BookmarksBarControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9f);
    }

    /// <summary>Asked to do something with one of them.</summary>
    public event EventHandler<(Bookmark Bookmark, BookmarkAction Action)>? Requested;

    /// <summary>The ones that did not fit, for the window to put in a menu.</summary>
    public event EventHandler<(Point At, IReadOnlyList<Bookmark> Hidden)>? OverflowRequested;

    /// <summary>The icon for a bookmark, once something has fetched it.</summary>
    public Func<Bookmark, Image?>? IconFor { get; set; }

    public void Show(IReadOnlyList<Bookmark> bookmarks)
    {
        _bookmarks = bookmarks;
        _hover = -1;
        _measured = false;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _measured = false;
    }

    /// <summary>
    /// Works the layout out if it has not been. The mouse can arrive before the
    /// first paint does, and a bar that ignores a click until it has been drawn
    /// once is a bar that ignores the first click.
    /// </summary>
    private void EnsureMeasured()
    {
        if (_measured)
        {
            return;
        }

        using Graphics graphics = CreateGraphics();
        Measure(graphics);
    }

    private float UiScale => DeviceDpi / 96f;

    private int OverflowWidth => (int)(26 * UiScale);

    /// <summary>Works out where each bookmark goes, and which are left over.</summary>
    private void Measure(Graphics graphics)
    {
        _measured = true;
        _items.Clear();
        _firstHidden = -1;

        int x = (int)(6 * UiScale);
        int height = Height - (int)(6 * UiScale);
        int top = (Height - height) / 2;
        int room = Width - OverflowWidth;

        for (int index = 0; index < _bookmarks.Count; index++)
        {
            Bookmark bookmark = _bookmarks[index];
            int text = TextRenderer.MeasureText(
                graphics, Title(bookmark), Font, new Size(int.MaxValue, Height), TextFlags).Width;

            // The room a bookmark needs is the text plus everything around it:
            // the padding at each end, the icon, and the gap after the icon.
            // Leaving any of that out is what turns "Wikipedia" into "Wikipe...".
            int furniture = (int)((Padding + IconSize + IconGap + Padding) * UiScale);
            int width = Math.Min((int)(MaximumItemWidth * UiScale), text + furniture);

            if (x + width > room)
            {
                _firstHidden = index;
                break;
            }

            _items.Add((bookmark, new Rectangle(x, top, width, height)));
            x += width + (int)(2 * UiScale);
        }
    }

    private static string Title(Bookmark bookmark) =>
        string.IsNullOrWhiteSpace(bookmark.Title) ? bookmark.Url : bookmark.Title;

    private IReadOnlyList<Bookmark> Hidden =>
        _firstHidden < 0 ? [] : _bookmarks.Skip(_firstHidden).ToList();

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        EnsureMeasured();
        int hover = _items.FindIndex(item => item.Bounds.Contains(e.Location));
        bool overflow = _firstHidden >= 0 && e.X >= Width - OverflowWidth;

        if (hover != _hover || overflow != _hoverOverflow)
        {
            _hover = hover;
            _hoverOverflow = overflow;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover >= 0 || _hoverOverflow)
        {
            _hover = -1;
            _hoverOverflow = false;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        EnsureMeasured();

        if (_firstHidden >= 0 && e.X >= Width - OverflowWidth && e.Button == MouseButtons.Left)
        {
            OverflowRequested?.Invoke(this, (new Point(Width - OverflowWidth, Height), Hidden));
            return;
        }

        int at = _items.FindIndex(item => item.Bounds.Contains(e.Location));
        if (at < 0)
        {
            return;
        }

        Bookmark bookmark = _items[at].Bookmark;
        switch (e.Button)
        {
            case MouseButtons.Left:
                Requested?.Invoke(this, (bookmark, BookmarkAction.Open));
                break;

            case MouseButtons.Middle:
                Requested?.Invoke(this, (bookmark, BookmarkAction.OpenInNewTab));
                break;

            case MouseButtons.Right:
                ShowItemMenu(bookmark, e.Location);
                break;
        }
    }

    private void ShowItemMenu(Bookmark bookmark, Point at)
    {
        ContextMenuStrip menu = new()
        {
            Renderer = new MenuRenderer(),
            BackColor = Theme.Current.Surface,
            ForeColor = Theme.Current.Text,
            ShowImageMargin = false,
            Font = Font,
        };

        void Item(string text, BookmarkAction action) =>
            menu.Items.Add(new ToolStripMenuItem(text, null, (_, _) =>
                Requested?.Invoke(this, (bookmark, action))));

        Item(Strings.Of("bookmark.open"), BookmarkAction.Open);
        Item(Strings.Of("bookmark.openNewTab"), BookmarkAction.OpenInNewTab);
        menu.Items.Add(new ToolStripSeparator());
        Item(Strings.Of("bookmark.rename"), BookmarkAction.Rename);
        Item(Strings.Of("bookmark.remove"), BookmarkAction.Remove);

        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(this, at);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Palette palette = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(palette.Chrome);

        Measure(g);

        for (int index = 0; index < _items.Count; index++)
        {
            (Bookmark bookmark, Rectangle bounds) = _items[index];

            if (index == _hover)
            {
                using SolidBrush brush = new(palette.Hover);
                using GraphicsPath path = Rounded(bounds, (int)(7 * UiScale));
                g.FillPath(brush, path);
            }

            int iconSize = (int)(IconSize * UiScale);
            int left = bounds.X + (int)(Padding * UiScale);
            Image? icon = IconFor?.Invoke(bookmark);

            if (icon is not null)
            {
                g.DrawImage(icon, new Rectangle(left, bounds.Y + ((bounds.Height - iconSize) / 2), iconSize, iconSize));
            }

            left += iconSize + (int)(IconGap * UiScale);

            TextRenderer.DrawText(
                g,
                Title(bookmark),
                Font,
                new Rectangle(left, bounds.Y, bounds.Right - left - (int)(Padding * UiScale), bounds.Height),
                palette.Text,
                TextFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        if (_firstHidden >= 0)
        {
            DrawOverflow(g, palette);
        }
    }

    /// <summary>A chevron for what did not fit.</summary>
    private void DrawOverflow(Graphics g, Palette palette)
    {
        Rectangle rect = new(Width - OverflowWidth, (Height - OverflowWidth) / 2, OverflowWidth, OverflowWidth);

        if (_hoverOverflow)
        {
            using SolidBrush brush = new(palette.Hover);
            using GraphicsPath path = Rounded(rect, (int)(7 * UiScale));
            g.FillPath(brush, path);
        }

        using Pen pen = new(palette.TextMuted, 1.4f * UiScale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        int cx = rect.X + (rect.Width / 2);
        int cy = rect.Y + (rect.Height / 2);
        int arm = (int)(3.5f * UiScale);
        g.DrawLine(pen, cx - arm, cy - arm, cx, cy);
        g.DrawLine(pen, cx, cy, cx - arm, cy + arm);
        g.DrawLine(pen, cx + arm, cy - arm, cx + (2 * arm), cy);
        g.DrawLine(pen, cx + (2 * arm), cy, cx + arm, cy + arm);
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
