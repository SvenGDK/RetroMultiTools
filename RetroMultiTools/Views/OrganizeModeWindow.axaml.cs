using Avalonia.Controls;

namespace RetroMultiTools.Views;

public partial class OrganizeModeWindow : Window
{
    public OrganizeModeWindow()
    {
        InitializeComponent();
    }

    private void CopyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void MoveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
