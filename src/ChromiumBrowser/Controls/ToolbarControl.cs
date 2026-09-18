using System.Drawing.Drawing2D;
using ChromiumBrowser.Ui;

namespace ChromiumBrowser.Controls;

/// <summary>
/// Back, forward, reload, home, and the bar the address goes in.
///
/// The buttons are drawn from arcs and lines for the same reason the caption
/// buttons are, and the address field is a plain text box with its border taken
/// off, sitting inside a rounded shape this control paints. Windows Forms text
/// boxes cannot be rounded and cannot be given a colour for their border, so the
/// control draws the frame and lets the text box do only what it is good at.
/// </summary>
public sealed class ToolbarControl : Control
{
    private readonly TextBox _address = new()
    {
        BorderStyle = BorderStyle.None,
        AutoCompleteMode = AutoCompleteMode.None,
    };

    private int _hover = -1;

    public ToolbarControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9f);

        _address.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            string text = _address.Text.Trim();
            if (text.Length > 0)
            {
                Navigated?.Invoke(this, text);
            }
        };

        // Clicking into the bar selects what is there, so typing replaces the
        // address rather than appending to it.
        _address.GotFocus += (_, _) => _address.SelectAll();
        _address.MouseUp += (_, _) =>
        {
            if (_address.SelectionLength == 0 && !_userEdited)
            {
                _address.SelectAll();
            }
        };
        _address.TextChanged += (_, _) => _userEdited = true;

        Controls.Add(_address);
        ApplyTheme();
    }

    private bool _userEdited;

    public event EventHandler? BackRequested;

    public event EventHandler? ForwardRequested;

    /// <summary>Reload, or stop while the page is still coming in.</summary>
    public event EventHandler? ReloadRequested;

    public event EventHandler? StopRequested;

    public event EventHandler? HomeRequested;

    /// <summary>Something was typed into the bar and entered.</summary>
    public event EventHandler<string>? Navigated;

    /// <summary>The menu button was pressed; the point is where to open the menu.</summary>
    public event EventHandler<Point>? MenuRequested;

    public bool CanGoBack { get; set; }

    public bool CanGoForward { get; set; }

    public bool IsLoading { get; set; }

    /// <summary>Shows an address without treating it as something the user typed.</summary>
    public void ShowAddress(string address)
    {
        if (_address.Focused)
        {
            return; // never pull the text out from under someone typing
        }

        _address.Text = address;
        _userEdited = false;
    }

    public void FocusAddress()
    {
        _address.Focus();
        _address.SelectAll();
    }

    public void ApplyTheme()
    {
        Palette palette = Theme.Current;
        BackColor = palette.Chrome;
        _address.BackColor = Theme.IsDark ? palette.Hover : palette.Surface;
        _address.ForeColor = palette.Text;
        Invalidate();
    }

    private float UiScale => DeviceDpi / 96f;

    private int ButtonSize => (int)(34 * UiScale);

    private Rectangle ButtonRect(int index) =>
        new((int)(6 * UiScale) + (index * (ButtonSize + (int)(2 * UiScale))),
            (Height - ButtonSize) / 2,
            ButtonSize,
            ButtonSize);

    /// <summary>The menu sits at the far right, where every browser keeps it.</summary>
    private Rectangle MenuRect
    {
        get
        {
            int size = ButtonSize;
            return new Rectangle(Width - size - (int)(6 * UiScale), (Height - size) / 2, size, size);
        }
    }

    private Rectangle AddressRect
    {
        get
        {
            int left = ButtonRect(3).Right + (int)(8 * UiScale);
            int right = MenuRect.Left - (int)(8 * UiScale);
            int height = (int)(30 * UiScale);
            return new Rectangle(left, (Height - height) / 2, Math.Max(0, right - left), height);
        }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Rectangle bar = AddressRect;
        int pad = (int)(12 * UiScale);
        _address.SetBounds(
            bar.X + pad,
            bar.Y + ((bar.Height - _address.PreferredHeight) / 2),
            Math.Max(0, bar.Width - (2 * pad)),
            _address.PreferredHeight);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hover = -1;
        for (int index = 0; index < 4; index++)
        {
            if (ButtonRect(index).Contains(e.Location))
            {
                hover = index;
                break;
            }
        }

        if (hover < 0 && MenuRect.Contains(e.Location))
        {
            hover = MenuButton;
        }

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

        switch (_hover)
        {
            case 0 when CanGoBack: BackRequested?.Invoke(this, EventArgs.Empty); break;
            case 1 when CanGoForward: ForwardRequested?.Invoke(this, EventArgs.Empty); break;
            case 2 when IsLoading: StopRequested?.Invoke(this, EventArgs.Empty); break;
            case 2: ReloadRequested?.Invoke(this, EventArgs.Empty); break;
            case 3: HomeRequested?.Invoke(this, EventArgs.Empty); break;
            case MenuButton:
                MenuRequested?.Invoke(this, new Point(MenuRect.Left, MenuRect.Bottom));
                break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Palette palette = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(palette.Chrome);

        DrawButton(g, palette, 0, CanGoBack);
        DrawButton(g, palette, 1, CanGoForward);
        DrawButton(g, palette, 2, enabled: true);
        DrawButton(g, palette, 3, enabled: true);
        DrawMenuButton(g, palette);

        Rectangle bar = AddressRect;
        using GraphicsPath path = Rounded(bar, bar.Height / 2);
        using SolidBrush fill = new(_address.BackColor);
        g.FillPath(fill, path);
        using Pen line = new(_address.Focused ? palette.Accent : palette.Line, _address.Focused ? 1.4f * UiScale : 1f);
        g.DrawPath(line, path);
    }

    /// <summary>The index the menu button answers to, kept away from the other four.</summary>
    private const int MenuButton = 9;

    /// <summary>Three dots, which is what a browser menu looks like everywhere.</summary>
    private void DrawMenuButton(Graphics g, Palette palette)
    {
        Rectangle rect = MenuRect;
        if (_hover == MenuButton)
        {
            using SolidBrush hover = new(palette.Hover);
            using GraphicsPath path = Rounded(rect, (int)(8 * UiScale));
            g.FillPath(hover, path);
        }

        using SolidBrush brush = new(palette.Text);
        int dot = Math.Max(2, (int)(3 * UiScale));
        int x = rect.X + ((rect.Width - dot) / 2);
        int gap = (int)(5 * UiScale);
        int y = rect.Y + (rect.Height / 2) - gap - (dot / 2);

        for (int index = 0; index < 3; index++)
        {
            g.FillEllipse(brush, x, y + (index * gap), dot, dot);
        }
    }

    private void DrawButton(Graphics g, Palette palette, int index, bool enabled)
    {
        Rectangle rect = ButtonRect(index);

        if (_hover == index && enabled)
        {
            using SolidBrush brush = new(palette.Hover);
            using GraphicsPath path = Rounded(rect, (int)(8 * UiScale));
            g.FillPath(brush, path);
        }

        Color ink = enabled ? palette.Text : Color.FromArgb(90, palette.TextMuted);
        using Pen pen = new(ink, 1.5f * UiScale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        int cx = rect.X + (rect.Width / 2);
        int cy = rect.Y + (rect.Height / 2);
        int arm = (int)(5 * UiScale);

        switch (index)
        {
            case 0: // back
                g.DrawLine(pen, cx + arm, cy - arm - arm, cx - arm, cy);
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy + arm + arm);
                break;

            case 1: // forward
                g.DrawLine(pen, cx - arm, cy - arm - arm, cx + arm, cy);
                g.DrawLine(pen, cx + arm, cy, cx - arm, cy + arm + arm);
                break;

            case 2 when IsLoading: // stop
                g.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm);
                g.DrawLine(pen, cx + arm, cy - arm, cx - arm, cy + arm);
                break;

            case 2: // reload: an almost closed circle with an arrow head
                Rectangle circle = new(cx - arm - 1, cy - arm - 1, (arm * 2) + 2, (arm * 2) + 2);
                g.DrawArc(pen, circle, 30, 300);
                g.DrawLine(pen, circle.Right - 1, circle.Top + 1, circle.Right - 1, circle.Top + (int)(4 * UiScale));
                g.DrawLine(pen, circle.Right - 1, circle.Top + 1, circle.Right - (int)(4 * UiScale), circle.Top + 1);
                break;

            case 3: // home: a roof and a wall
                g.DrawLine(pen, cx - arm - 1, cy, cx, cy - arm - 1);
                g.DrawLine(pen, cx, cy - arm - 1, cx + arm + 1, cy);
                g.DrawLine(pen, cx - arm + 1, cy, cx - arm + 1, cy + arm + 1);
                g.DrawLine(pen, cx + arm - 1, cy, cx + arm - 1, cy + arm + 1);
                g.DrawLine(pen, cx - arm + 1, cy + arm + 1, cx + arm - 1, cy + arm + 1);
                break;
        }
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
