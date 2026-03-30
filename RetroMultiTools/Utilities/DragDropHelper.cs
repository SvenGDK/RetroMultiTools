using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Provides reusable drag-and-drop file support for TextBox controls.
/// Call <see cref="EnableFileDrop"/> in a view constructor to let users
/// drop files or folders directly onto a path TextBox.
/// </summary>
public static class DragDropHelper
{
    private static readonly IBrush DropHighlightBrush = new SolidColorBrush(Color.Parse("#3389B4FA"));
    private static readonly IBrush TransparentBrush = Brushes.Transparent;

    /// <summary>
    /// Enables file/folder drag-and-drop on a <see cref="TextBox"/>.
    /// When a single file or folder is dropped, its path is set as the TextBox text
    /// and the optional <paramref name="onDropped"/> callback is invoked.
    /// </summary>
    /// <param name="textBox">The target TextBox control.</param>
    /// <param name="onDropped">
    /// Optional callback invoked after a path is dropped, receiving the dropped path.
    /// Use this to trigger validation, button state updates, or auto-detection logic.
    /// </param>
    /// <param name="acceptDirectories">
    /// When true, dropped directories are also accepted. When false (default), only files are accepted.
    /// </param>
    public static void EnableFileDrop(TextBox textBox, Action<string>? onDropped = null, bool acceptDirectories = false)
    {
        DragDrop.SetAllowDrop(textBox, true);

        textBox.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            if (e.DataTransfer.Contains(DataFormat.File))
            {
                e.DragEffects = DragDropEffects.Copy;
                textBox.Background = DropHighlightBrush;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        });

        textBox.AddHandler(DragDrop.DragLeaveEvent, (_, _) =>
        {
            textBox.Background = TransparentBrush;
        });

        textBox.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            textBox.Background = TransparentBrush;

            IStorageItem[]? items = e.DataTransfer.TryGetFiles();
            if (items == null) return;

            foreach (var item in items)
            {
                try
                {
                    string? path = item.Path?.LocalPath;
                    if (string.IsNullOrEmpty(path)) continue;

                    bool isDirectory = Directory.Exists(path);

                    if (isDirectory && !acceptDirectories) continue;
                    if (!isDirectory && !File.Exists(path)) continue;

                    textBox.Text = path;
                    onDropped?.Invoke(path);
                    return; // Accept first valid item
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or UriFormatException)
                {
                    // Skip items with inaccessible or invalid paths
                }
            }
        });
    }
}
