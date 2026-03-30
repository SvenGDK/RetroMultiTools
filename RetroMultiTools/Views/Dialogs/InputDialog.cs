using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using RetroMultiTools.Localization;

namespace RetroMultiTools.Views.Dialogs;

/// <summary>
/// A simple dialog that prompts the user to enter a text value.
/// Returns the entered text on OK, or null on cancel.
/// </summary>
public class InputDialog : Window
{
    private readonly TextBox _inputTextBox;
    private string? _result;

    public InputDialog(string title, string defaultValue = "")
    {
        var loc = LocalizationManager.Instance;

        Title = title;
        Width = 420;
        Height = 170;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _inputTextBox = new TextBox
        {
            Text = defaultValue,
            Padding = new Thickness(8, 6),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var okButton = new Button
        {
            Content = loc["Common_OK"],
            Padding = new Thickness(16, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 8, 0)
        };
        okButton.Click += (_, _) =>
        {
            _result = _inputTextBox.Text?.Trim();
            Close();
        };

        var cancelButton = new Button
        {
            Content = loc["Common_Cancel"],
            Padding = new Thickness(16, 8),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        cancelButton.Click += (_, _) =>
        {
            _result = null;
            Close();
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { okButton, cancelButton }
        };

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children =
            {
                _inputTextBox,
                buttonPanel
            }
        };
    }

    public new async Task<string?> ShowDialog(Window owner)
    {
        await base.ShowDialog(owner);
        return _result;
    }
}
