namespace ChromiumBrowser.Ui;

/// <summary>
/// Paints the menu in the browser's own colours.
///
/// A menu is one of the few places Windows Forms still draws from the system
/// theme, so a dark window opening a white menu is the giveaway that the dark
/// parts were painted by hand. The renderer only has to answer for the
/// background, the hover strip, the separator and the border; the rest is
/// already text in the colour it is told.
/// </summary>
public sealed class MenuRenderer : ToolStripProfessionalRenderer
{
    public MenuRenderer()
        : base(new MenuColours())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        Palette palette = Theme.Current;
        e.TextColor = e.Item?.Enabled == true ? palette.Text : palette.TextMuted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using Pen pen = new(Theme.Current.Line);
        Rectangle rect = new(0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
    }

    private sealed class MenuColours : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Current.Surface;

        public override Color ImageMarginGradientBegin => Theme.Current.Surface;

        public override Color ImageMarginGradientMiddle => Theme.Current.Surface;

        public override Color ImageMarginGradientEnd => Theme.Current.Surface;

        public override Color MenuItemSelected => Theme.Current.Hover;

        public override Color MenuItemSelectedGradientBegin => Theme.Current.Hover;

        public override Color MenuItemSelectedGradientEnd => Theme.Current.Hover;

        public override Color MenuItemBorder => Theme.Current.Hover;

        public override Color MenuBorder => Theme.Current.Line;

        public override Color SeparatorDark => Theme.Current.Line;

        public override Color SeparatorLight => Theme.Current.Line;
    }
}
