using System.Drawing.Drawing2D;
using ChromiumBrowser.Core.Localisation;
using ChromiumBrowser.Ui;

namespace ChromiumBrowser.Controls;

/// <summary>
/// The strip that finds words on the page: a box, how many matches there are,
/// and the three buttons a search needs.
///
/// It is a bar across the chrome rather than a card floating over the page,
/// which is what Chrome does. The page is a window of the engine's own, and a
/// control drawn over it is at the mercy of which window Windows puts on top —
/// the sort of thing that works on one machine and flickers on another. A bar
/// in the chrome cannot be covered, and it takes its height off the page
/// honestly instead of hiding a strip of it.
/// </summary>
public sealed class FindBarControl : Control
{
    /// <summary>The box, and the buttons after it, in device-independent pixels.</summary>
    public const int Inset = 8;

    public const int FieldWidth = 300;

    public const int ButtonSize = 28;

    private readonly TextBox _search = new()
    {
        BorderStyle = BorderStyle.None,
    };

    private string _counter = string.Empty;
    private int _hover = -1;
    private readonly ToolTip _tip = new();

    public FindBarControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9f);
        Visible = false;

        _search.TextChanged += (_, _) => SearchChanged?.Invoke(this, _search.Text);
        _search.KeyDown += (_, e) =>
        {
            switch (e.KeyCode)
            {
                // Enter walks the matches rather than submitting anything, and
                // with Shift it walks back — the two keys every find box answers
                // to, and the reason the bar does not need to be clicked at all.
                case Keys.Enter:
                    e.SuppressKeyPress = true;
                    Step(forward: !e.Shift);
                    break;

                case Keys.Escape:
                    e.SuppressKeyPress = true;
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        };

        Controls.Add(_search);
        ApplyTheme();
    }

    /// <summary>What is in the box, as it is typed.</summary>
    public event EventHandler<string>? SearchChanged;

    /// <summary>The next or the previous match was asked for.</summary>
    public event EventHandler<bool>? StepRequested;

    public event EventHandler? CloseRequested;

    public string SearchText => _search.Text;

    /// <summary>Shows the bar, with whatever was last searched for selected.</summary>
    public void Open()
    {
        Visible = true;
        _search.Focus();
        _search.SelectAll();
    }

    /// <summary>What the engine reported, already turned into text.</summary>
    public void ShowCounter(string text)
    {
        if (text == _counter)
        {
            return;
        }

        _counter = text;
        Invalidate();
    }

    public void ApplyTheme()
    {
        Palette palette = Theme.Current;
        BackColor = palette.Chrome;
        _search.BackColor = Theme.IsDark ? palette.Hover : palette.Surface;
        _search.ForeColor = palette.Text;
        _search.PlaceholderText = Strings.Of("find.placeholder");
        _search.AccessibleName = Strings.Of("find.placeholder");
        Invalidate();
    }

    private void Step(bool forward) => StepRequested?.Invoke(this, forward);

    private float UiScale => DeviceDpi / 96f;

    private int Dip(int value) => (int)(value * UiScale);

    private Rectangle FieldRect
    {
        get
        {
            int height = Dip(26);
            return new Rectangle(Dip(Inset), (Height - height) / 2, Dip(FieldWidth), height);
        }
    }

    /// <summary>Previous, next and close, in that order, after the box.</summary>
    public Rectangle ButtonRect(int index)
    {
        int size = Dip(ButtonSize);
        int left = FieldRect.Right + Dip(Inset) + (index * (size + Dip(2)));
        return new Rectangle(left, (Height - size) / 2, size, size);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        Rectangle field = FieldRect;
        int pad = Dip(10);

        // The counter is drawn inside the right-hand end of the box, so the text
        // has to stop before it rather than run underneath.
        int counter = Dip(58);
        _search.SetBounds(
            field.X + pad,
            field.Y + ((field.Height - _search.PreferredHeight) / 2),
            Math.Max(0, field.Width - pad - counter),
            _search.PreferredHeight);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int hover = -1;
        for (int index = 0; index < 3; index++)
        {
            if (ButtonRect(index).Contains(e.Location))
            {
                hover = index;
                break;
            }
        }

        if (hover != _hover)
        {
            _hover = hover;
            _tip.SetToolTip(this, hover >= 0 ? ButtonName(hover) : string.Empty);
            Invalidate();
        }
    }

    private static string ButtonName(int index) => Strings.Of(index switch
    {
        0 => "find.previous", 1 => "find.next", _ => "find.close",
    });

    protected override AccessibleObject CreateAccessibilityInstance() => new AccessibleActions(this, () =>
        Enumerable.Range(0, 3).Select(i => new AccessibleActions.Item(ButtonName(i), ButtonRect(i), () =>
        {
            if (i == 2) CloseRequested?.Invoke(this, EventArgs.Empty);
            else Step(i == 1);
        })).ToArray());

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

        for (int index = 0; index < 3; index++)
        {
            if (!ButtonRect(index).Contains(e.Location))
            {
                continue;
            }

            switch (index)
            {
                case 0: Step(forward: false); break;
                case 1: Step(forward: true); break;
                case 2: CloseRequested?.Invoke(this, EventArgs.Empty); break;
            }

            return;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Palette palette = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(palette.Chrome);

        Rectangle field = FieldRect;
        using GraphicsPath path = Rounded(field, Dip(6));
        using SolidBrush fill = new(_search.BackColor);
        g.FillPath(fill, path);
        using Pen line = new(_search.Focused ? palette.Accent : palette.Line, _search.Focused ? 1.4f * UiScale : 1f);
        g.DrawPath(line, path);

        if (_counter.Length > 0)
        {
            using SolidBrush text = new(palette.TextMuted);
            SizeF size = g.MeasureString(_counter, Font);
            g.DrawString(
                _counter,
                Font,
                text,
                field.Right - Dip(10) - size.Width,
                field.Y + ((field.Height - size.Height) / 2));
        }

        bool matches = _counter.Length == 0 || _counter.Contains('/');
        DrawChevron(g, palette, ButtonRect(0), up: true, enabled: matches);
        DrawChevron(g, palette, ButtonRect(1), up: false, enabled: matches);
        DrawCross(g, palette, ButtonRect(2));

        // The same hairline the window draws under its chrome, so the bar reads
        // as part of it rather than as something laid on top.
        using Pen edge = new(palette.Line);
        g.DrawLine(edge, 0, Height - 1, Width, Height - 1);
    }

    private void DrawChevron(Graphics g, Palette palette, Rectangle rect, bool up, bool enabled)
    {
        DrawHover(g, palette, rect);

        int arm = Dip(4);
        Point centre = new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        int lift = up ? arm / 2 : -arm / 2;

        using Pen pen = new(enabled ? palette.Text : palette.TextMuted, 1.5f * UiScale)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        if (up)
        {
            g.DrawLines(pen,
            [
                new Point(centre.X - arm, centre.Y + lift),
                new Point(centre.X, centre.Y - arm + lift),
                new Point(centre.X + arm, centre.Y + lift),
            ]);
            return;
        }

        g.DrawLines(pen,
        [
            new Point(centre.X - arm, centre.Y + lift),
            new Point(centre.X, centre.Y + arm + lift),
            new Point(centre.X + arm, centre.Y + lift),
        ]);
    }

    private void DrawCross(Graphics g, Palette palette, Rectangle rect)
    {
        DrawHover(g, palette, rect);

        int arm = Dip(4);
        Point centre = new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));

        using Pen pen = new(palette.Text, 1.5f * UiScale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, centre.X - arm, centre.Y - arm, centre.X + arm, centre.Y + arm);
        g.DrawLine(pen, centre.X + arm, centre.Y - arm, centre.X - arm, centre.Y + arm);
    }

    private void DrawHover(Graphics g, Palette palette, Rectangle rect)
    {
        if (_hover < 0 || ButtonRect(_hover) != rect)
        {
            return;
        }

        using SolidBrush brush = new(palette.Hover);
        using GraphicsPath path = Rounded(rect, Dip(6));
        g.FillPath(brush, path);
    }

    private static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        int diameter = radius * 2;

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
