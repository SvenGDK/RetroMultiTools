using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using RetroMultiTools.Detection;
using RetroMultiTools.Models;
using RetroMultiTools.Services;

namespace RetroMultiTools.Views;

public partial class RomInspectorView : UserControl
{
    public RomInspectorView()
    {
        InitializeComponent();
    }

    private async void OpenRomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open ROM File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ROM Files")
                {
                    Patterns = [ "*.nes","*.smc","*.sfc","*.z64","*.n64","*.v64",
                                       "*.gb","*.gbc","*.gba","*.vb","*.vboy",
                                       "*.sms","*.md","*.gen",
                                       "*.bin","*.32x","*.gg","*.a26","*.a52","*.a78",
                                       "*.j64","*.jag","*.lnx","*.lyx",
                                       "*.pce","*.tg16","*.iso","*.cue","*.3do",
                                       "*.chd","*.rvz","*.gcm",
                                       "*.ngp","*.ngc",
                                       "*.col","*.cv","*.int",
                                       "*.mx1","*.mx2",
                                       "*.dsk","*.cdt","*.sna",
                                       "*.tap",
                                       "*.mo5","*.k7","*.fd",
                                       "*.sv","*.ccc" ]
                },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        string path = files[0].Path.LocalPath;
        RomFilePathText.Text = path;

        // Show progress
        ProgressPanel.IsVisible = true;
        LoadProgressBar.IsVisible = true;
        OpenRomButton.IsEnabled = false;
        ArtworkPanel.IsVisible = false;
        ResetArtwork();

        try
        {
            // Step 1: Detect ROM
            ProgressText.Text = "Reading ROM header...";
            var info = await Task.Run(() => RomDetector.Detect(path));

            // Step 2: Display ROM info
            ProgressText.Text = "Displaying ROM information...";
            SystemNameText.Text = info.SystemName;
            FileSizeText.Text = info.FileSizeFormatted;
            IsValidText.Text = info.IsValid ? "✔ Valid" : "✘ Invalid";
            IsValidText.Foreground = info.IsValid
                ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3A1"))
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F38BA8"));
            ErrorMessageText.Text = info.ErrorMessage ?? string.Empty;

            HeaderItemsControl.ItemsSource = info.HeaderInfo
                .Select(kv => new { Key = kv.Key + ":", kv.Value })
                .ToList();

            // Step 3: Fetch artwork
            ProgressText.Text = "Fetching artwork from database...";
            var artProgress = new Progress<string>(msg => ProgressText.Text = msg);
            var artwork = await ArtworkService.FetchArtworkAsync(info, artProgress);

            // Step 4: Display artwork
            DisplayArtwork(artwork);

            ProgressText.Text = artwork.HasAnyArtwork
                ? "Done — artwork loaded."
                : "Done — no artwork found for this title.";
        }
        catch (IOException ex)
        {
            ProgressText.Text = $"Error: {ex.Message}";
        }
        catch (HttpRequestException ex)
        {
            ProgressText.Text = $"Error: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ProgressText.Text = "Operation cancelled.";
        }
        catch (UnauthorizedAccessException ex)
        {
            ProgressText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ProgressPanel.IsVisible = false;
            LoadProgressBar.IsVisible = false;
            OpenRomButton.IsEnabled = true;
        }
    }

    private void DisplayArtwork(ArtworkInfo artwork)
    {
        if (!artwork.HasAnyArtwork)
        {
            ArtworkPanel.IsVisible = false;
            return;
        }

        ArtworkPanel.IsVisible = true;

        if (artwork.BoxArt != null)
        {
            var newBitmap = LoadBitmap(artwork.BoxArt);
            var old = BoxArtImage.Source as Bitmap;
            BoxArtImage.Source = newBitmap;
            old?.Dispose();
            BoxArtPanel.IsVisible = true;
        }

        if (artwork.Snap != null)
        {
            var newBitmap = LoadBitmap(artwork.Snap);
            var old = SnapImage.Source as Bitmap;
            SnapImage.Source = newBitmap;
            old?.Dispose();
            SnapPanel.IsVisible = true;
        }

        if (artwork.TitleScreen != null)
        {
            var newBitmap = LoadBitmap(artwork.TitleScreen);
            var old = TitleScreenImage.Source as Bitmap;
            TitleScreenImage.Source = newBitmap;
            old?.Dispose();
            TitleScreenPanel.IsVisible = true;
        }
    }

    private void ResetArtwork()
    {
        BoxArtPanel.IsVisible = false;
        SnapPanel.IsVisible = false;
        TitleScreenPanel.IsVisible = false;
        (BoxArtImage.Source as Bitmap)?.Dispose();
        (SnapImage.Source as Bitmap)?.Dispose();
        (TitleScreenImage.Source as Bitmap)?.Dispose();
        BoxArtImage.Source = null;
        SnapImage.Source = null;
        TitleScreenImage.Source = null;
    }

    private static Bitmap? LoadBitmap(byte[] data)
    {
        if (data.Length == 0)
            return null;

        try
        {
            using var stream = new MemoryStream(data);
            return new Bitmap(stream);
        }
        catch (InvalidOperationException)
        {
            // Invalid or corrupt image data that Avalonia cannot decode
            return null;
        }
        catch (IOException)
        {
            // I/O error during stream read
            return null;
        }
    }
}
