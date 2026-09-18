using ChromiumBrowser.Ui;

namespace ChromiumBrowser;

/// <summary>
/// One line of text and two buttons, in the browser's colours.
///
/// Windows Forms has no input box of its own, and the one in the Visual Basic
/// library is a system dialog that would arrive in system colours, white in the
/// middle of a dark window.
/// </summary>
public sealed class PromptForm : Form
{
    private readonly TextBox _input = new() { BorderStyle = BorderStyle.FixedSingle };

    public PromptForm(string title, string question, string value)
    {
        Palette palette = Theme.Current;

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        BackColor = palette.Chrome;
        ForeColor = palette.Text;
        ClientSize = new Size(420, 140);
        Font = new Font("Segoe UI", 9f);

        Label label = new()
        {
            Text = question,
            AutoSize = false,
            Location = new Point(20, 18),
            Size = new Size(ClientSize.Width - 40, 20),
            ForeColor = palette.TextMuted,
        };

        _input.Text = value;
        _input.Location = new Point(20, 46);
        _input.Size = new Size(ClientSize.Width - 40, 26);
        _input.BackColor = palette.Surface;
        _input.ForeColor = palette.Text;

        Button ok = Action("OK", DialogResult.OK, ClientSize.Width - 200);
        Button cancel = Action("Cancel", DialogResult.Cancel, ClientSize.Width - 100);

        Controls.AddRange([label, _input, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        Load += (_, _) => _input.SelectAll();
    }

    /// <summary>What was typed.</summary>
    public string Value => _input.Text.Trim();

    private Button Action(string text, DialogResult result, int left)
    {
        Palette palette = Theme.Current;
        Button button = new()
        {
            Text = text,
            DialogResult = result,
            FlatStyle = FlatStyle.Flat,
            BackColor = palette.Hover,
            ForeColor = palette.Text,
            Size = new Size(88, 30),
            Location = new Point(left, ClientSize.Height - 46),
        };

        button.FlatAppearance.BorderColor = palette.Line;
        return button;
    }
}
