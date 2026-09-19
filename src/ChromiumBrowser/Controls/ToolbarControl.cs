using System.Drawing.Drawing2D;
using ChromiumBrowser.Ui;
using ChromiumBrowser.Core.Localisation;

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
        AutoCompleteMode = AutoCompleteMode.SuggestAppend,
        AutoCompleteSource = AutoCompleteSource.CustomSource,
    };

    private int _hover = -1;
    private bool _isPrivate;
    private readonly ToolTip _tip = new();
    private string _currentAddress = string.Empty;
    private bool _selectOnMouseUp;
    private int _keyboardButton;

    public ToolbarControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9f);
        TabStop = true;

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
        _address.GotFocus += (_, _) =>
        {
            _selectOnMouseUp = Control.MouseButtons.HasFlag(MouseButtons.Left);
            AddressFocused?.Invoke(this, EventArgs.Empty);
            _address.SelectAll();
            Invalidate();
        };
        _address.MouseUp += (_, _) =>
        {
            if (_selectOnMouseUp)
            {
                _address.SelectAll();
                _selectOnMouseUp = false;
            }
        };
        _address.LostFocus += (_, _) => { _selectOnMouseUp = false; _address.Text = _currentAddress; Invalidate(); };

        Controls.Add(_address);
        ApplyTheme();
    }

    public event EventHandler? AddressFocused;

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

    /// <summary>
    /// Marks the window as one that remembers nothing. A private window that
    /// looks exactly like a normal one is how people end up typing into the
    /// wrong window in both directions.
    ///
    /// Setting it lays the toolbar out again, because the address box is a real
    /// text box whose width comes from where the badge starts. Without that, the
    /// box keeps its old width and covers the badge — the badge is painted, and
    /// a child control is drawn over it.
    /// </summary>
    public bool IsPrivate
    {
        get => _isPrivate;

        set
        {
            if (_isPrivate == value)
            {
                return;
            }

            _isPrivate = value;
            PerformLayout();
            Invalidate();
        }
    }

    /// <summary>Shows an address without treating it as something the user typed.</summary>
    public void ShowAddress(string address, bool force = false)
    {
        _currentAddress = address;
        if (_address.Focused && !force)
        {
            return; // never pull the text out from under someone typing
        }

        _address.Text = address;
    }

    public bool AddressHasFocus => _address.Focused;

    public void CancelAddressEdit()
    {
        _address.Text = _currentAddress;
        _address.SelectAll();
    }

    public void SetSuggestions(IEnumerable<string> addresses)
    {
        AutoCompleteStringCollection source = new();
        source.AddRange(addresses.Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.Ordinal).Take(500).ToArray());
        _address.AutoCompleteCustomSource = source;
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
        _address.AccessibleName = Strings.Of("toolbar.address");
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

    /// <summary>The pill saying "Private", or nothing wide at all in a normal window.</summary>
    private Rectangle BadgeRect
    {
        get
        {
            if (!IsPrivate)
            {
                return Rectangle.Empty;
            }

            int width = (int)(74 * UiScale);
            int height = (int)(22 * UiScale);
            return new Rectangle(MenuRect.Left - (int)(8 * UiScale) - width, (Height - height) / 2, width, height);
        }
    }

    private Rectangle AddressRect
    {
        get
        {
            int left = ButtonRect(3).Right + (int)(8 * UiScale);
            int right = (IsPrivate ? BadgeRect.Left : MenuRect.Left) - (int)(8 * UiScale);
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
            _tip.SetToolTip(this, hover >= 0 ? ButtonName(hover) : string.Empty);
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

        int hit = Enumerable.Range(0, 4).FirstOrDefault(i => ButtonRect(i).Contains(e.Location), -1);
        if (MenuRect.Contains(e.Location)) hit = MenuButton;
        InvokeButton(hit);
    }

    private void InvokeButton(int button)
    {
        switch (button)
        {
            case 0 when CanGoBack: BackRequested?.Invoke(this, EventArgs.Empty); break;
            case 1 when CanGoForward: ForwardRequested?.Invoke(this, EventArgs.Empty); break;
            case 2 when IsLoading: StopRequested?.Invoke(this, EventArgs.Empty); break;
            case 2: ReloadRequested?.Invoke(this, EventArgs.Empty); break;
            case 3: HomeRequested?.Invoke(this, EventArgs.Empty); break;
            case MenuButton:
                // The top-right corner of the menu that is about to open, which
                // is the button's own right edge: the menu hangs down and to
                // the left from there.
                MenuRequested?.Invoke(this, new Point(MenuRect.Right, MenuRect.Bottom));
                break;
        }
    }

    private string ButtonName(int button) => Strings.Of(button switch
    {
        0 => "toolbar.back", 1 => "toolbar.forward", 2 => IsLoading ? "toolbar.stop" : "toolbar.reload",
        3 => "toolbar.home", _ => "toolbar.menu",
    });

    protected override bool IsInputKey(Keys keys) =>
        (keys & Keys.KeyCode) is Keys.Left or Keys.Right || base.IsInputKey(keys);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left) _keyboardButton = Math.Max(0, _keyboardButton - 1);
        else if (e.KeyCode == Keys.Right) _keyboardButton = Math.Min(4, _keyboardButton + 1);
        else if (e.KeyCode is Keys.Enter or Keys.Space) InvokeButton(_keyboardButton == 4 ? MenuButton : _keyboardButton);
        else return;
        e.Handled = e.SuppressKeyPress = true;
        Invalidate();
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new AccessibleActions(this, () =>
        Enumerable.Range(0, 5).Select(i =>
        {
            int button = i == 4 ? MenuButton : i;
            return new AccessibleActions.Item(ButtonName(button), i == 4 ? MenuRect : ButtonRect(i),
                () => InvokeButton(button), State: (i == 0 && !CanGoBack) || (i == 1 && !CanGoForward)
                    ? AccessibleStates.Unavailable : AccessibleStates.None);
        }).ToArray());

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
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
        if (Focused) ControlPaint.DrawFocusRectangle(g, _keyboardButton == 4 ? MenuRect : ButtonRect(_keyboardButton));

        DrawBadge(g, palette);


        Rectangle bar = AddressRect;
        using GraphicsPath path = Rounded(bar, bar.Height / 2);
        using SolidBrush fill = new(_address.BackColor);
        g.FillPath(fill, path);
        using Pen line = new(_address.Focused ? palette.Accent : palette.Line, _address.Focused ? 1.4f * UiScale : 1f);
        g.DrawPath(line, path);
    }

    private void DrawBadge(Graphics g, Palette palette)
    {
        Rectangle badge = BadgeRect;
        if (badge.IsEmpty)
        {
            return;
        }

        using GraphicsPath path = Rounded(badge, badge.Height / 2);
        using SolidBrush fill = new(palette.Hover);
        using Pen edge = new(palette.Line);
        g.FillPath(fill, path);
        g.DrawPath(edge, path);

        // TextRenderer rather than Graphics.DrawString: the same GDI path
        // WinForms uses for its own labels, so the badge matches the text in
        // the box beside it rather than being hinted differently.
        TextRenderer.DrawText(
            g,
            Core.Localisation.Strings.Of("private.badge"),
            Font,
            badge,
            palette.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
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
