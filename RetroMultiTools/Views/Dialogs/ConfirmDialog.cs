using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using RetroMultiTools.Localization;

namespace RetroMultiTools.Views.Dialogs;

/// <summary>
/// A simple Yes/No confirmation dialog.
/// Returns true if the user confirmed, false otherwise.
/// </summary>
public class ConfirmDialog : Window
{
    private bool _result;

    public ConfirmDialog(string title, string message)
    {
        var loc = LocalizationManager.Instance;

        Title = title;
        Width = 450;
        Height = 180;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var yesButton = new Button
        {
            Content = loc["Common_Yes"],
            Padding = new Thickness(16, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 8, 0)
        };
        yesButton.Click += (_, _) =>
        {
            _result = true;
            Close();
        };

        var noButton = new Button
        {
            Content = loc["Common_No"],
            Padding = new Thickness(16, 8),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        noButton.Click += (_, _) =>
        {
            _result = false;
            Close();
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { yesButton, noButton }
        };

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                buttonPanel
            }
        };
    }

    public new async Task<bool> ShowDialog(Window owner)
    {
        await base.ShowDialog(owner);
        return _result;
    }
}
