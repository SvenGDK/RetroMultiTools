using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using RetroMultiTools.Detection;
using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Services;

namespace RetroMultiTools.Views;

public partial class RomInspectorView : UserControl
{
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.Parse("#F38BA8"));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.Parse("#A6E3A1"));
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
            Title = LocalizationManager.Instance["Inspector_OpenRomFile"],
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

        // Show progress and clear previous results
        ProgressPanel.IsVisible = true;
        LoadProgressBar.IsVisible = true;
        OpenRomButton.IsEnabled = false;
        ArtworkPanel.IsVisible = false;
        ResetArtwork();
        SystemNameText.Text = string.Empty;
        FileSizeText.Text = string.Empty;
        IsValidText.Text = string.Empty;
        ErrorMessageText.Text = string.Empty;
        HeaderItemsControl.ItemsSource = null;

        try
        {
            // Step 1: Detect ROM
            ProgressText.Text = LocalizationManager.Instance["Inspector_ReadingHeader"];
            var info = await Task.Run(() => RomDetector.Detect(path));

            // Step 2: Display ROM info
            ProgressText.Text = LocalizationManager.Instance["Inspector_DisplayingInfo"];
            SystemNameText.Text = info.SystemName;
            FileSizeText.Text = info.FileSizeFormatted;
            IsValidText.Text = info.IsValid ? LocalizationManager.Instance["Inspector_ValidYes"] : LocalizationManager.Instance["Inspector_ValidNo"];
            IsValidText.Foreground = info.IsValid ? StatusSuccessBrush : StatusErrorBrush;
            ErrorMessageText.Text = info.ErrorMessage ?? string.Empty;

            HeaderItemsControl.ItemsSource = info.HeaderInfo
                .Select(kv => new { Key = kv.Key + ":", kv.Value })
                .ToList();

            // Step 3: Fetch artwork
            ProgressText.Text = LocalizationManager.Instance["Inspector_FetchingArtwork"];
            var artProgress = new Progress<string>(msg => ProgressText.Text = msg);
            var artwork = await ArtworkService.FetchArtworkAsync(info, artProgress);

            // Step 4: Display artwork
            DisplayArtwork(artwork);

            ProgressText.Text = artwork.HasAnyArtwork
                ? LocalizationManager.Instance["Inspector_DoneArtwork"]
                : LocalizationManager.Instance["Inspector_DoneNoArtwork"];
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            ProgressText.Text = string.Format(LocalizationManager.Instance["Common_ErrorFormat"], ex.Message);
        }
        catch (TaskCanceledException)
        {
            ProgressText.Text = LocalizationManager.Instance["Inspector_Cancelled"];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[RomInspector] OpenRomButton_Click failed: {ex.Message}");
            ProgressText.Text = string.Format(LocalizationManager.Instance["Common_ErrorFormat"], ex.Message);
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
        // Detach bitmaps from Image controls before disposing to prevent
        // Avalonia from rendering an already-disposed bitmap.
        var boxArt = BoxArtImage.Source as Bitmap;
        var snap = SnapImage.Source as Bitmap;
        var titleScreen = TitleScreenImage.Source as Bitmap;
        BoxArtImage.Source = null;
        SnapImage.Source = null;
        TitleScreenImage.Source = null;
        boxArt?.Dispose();
        snap?.Dispose();
        titleScreen?.Dispose();
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
