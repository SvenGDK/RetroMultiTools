using Avalonia.Controls;
using Avalonia.Threading;
using RetroMultiTools.Localization;
using RetroMultiTools.Models;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class HostRomsWindow : Window
{
    private readonly string? _directoryPath;
    private readonly List<RomInfo>? _selectedRoms;
    private RomHostingService? _hostingService;

    public HostRomsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Host an entire directory of ROMs.
    /// </summary>
    public HostRomsWindow(string directoryPath)
    {
        InitializeComponent();
        _directoryPath = directoryPath;

        int fileCount = 0;
        try
        {
            fileCount = Directory.EnumerateFiles(directoryPath)
                .Count(RomHostingService.IsRomFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"HostRomsWindow: Failed to enumerate directory: {ex.Message}");
        }

        FileInfoText.Text = string.Format(LocalizationManager.Instance["HostShare_DirectoryInfo"], directoryPath, fileCount);
    }

    /// <summary>
    /// Host specific selected ROMs.
    /// </summary>
    public HostRomsWindow(List<RomInfo> selectedRoms)
    {
        InitializeComponent();
        _selectedRoms = selectedRoms;

        long totalSize = selectedRoms.Sum(r => r.FileSize);
        FileInfoText.Text = string.Format(LocalizationManager.Instance["HostShare_SelectedInfo"],
            selectedRoms.Count, FileUtils.FormatFileSize(totalSize));
    }

    private void StartStopButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_hostingService is { IsRunning: true })
        {
            StopServer();
        }
        else
        {
            StartServer();
        }
    }

    private void StartServer()
    {
        if (!int.TryParse(PortTextBox.Text, out int port) || port is < 1 or > 65535)
        {
            StatusText.Text = LocalizationManager.Instance["HostShare_PortInvalid"];
            return;
        }

        try
        {
            if (_hostingService != null)
            {
                _hostingService.LogMessage -= OnLogMessage;
                _hostingService.Dispose();
            }
            _hostingService = new RomHostingService();
            _hostingService.LogMessage += OnLogMessage;

            if (_directoryPath != null)
            {
                _hostingService.StartDirectory(_directoryPath, port);
            }
            else if (_selectedRoms != null)
            {
                var paths = _selectedRoms.Select(r => r.FilePath).ToList();
                _hostingService.StartSelectedFiles(paths, port);
            }
            else
            {
                StatusText.Text = LocalizationManager.Instance["HostShare_NoFilesToHost"];
                return;
            }

            PortTextBox.IsEnabled = false;
            StartStopButton.Content = LocalizationManager.Instance["HostShare_Stop"];

            UrlPanel.IsVisible = true;
            LogPanel.IsVisible = true;

            var addresses = RomHostingService.GetLocalIPAddresses();
            var urls = addresses.Select(ip => $"http://{ip}:{port}/").ToList();
            UrlsText.Text = string.Join("\n", urls);

            StatusText.Text = LocalizationManager.Instance["HostShare_ServerRunning"];
        }
        catch (Exception ex) when (ex is System.Net.HttpListenerException or System.Net.Sockets.SocketException)
        {
            StatusText.Text = string.Format(LocalizationManager.Instance["HostShare_StartFailed"], ex.Message);
            if (_hostingService != null)
            {
                _hostingService.LogMessage -= OnLogMessage;
                _hostingService.Dispose();
                _hostingService = null;
            }
        }
    }

    private void StopServer()
    {
        if (_hostingService != null)
        {
            _hostingService.LogMessage -= OnLogMessage;
            _hostingService.Stop();
            _hostingService.Dispose();
            _hostingService = null;
        }

        PortTextBox.IsEnabled = true;
        StartStopButton.Content = LocalizationManager.Instance["HostShare_Start"];
        UrlPanel.IsVisible = false;
        StatusText.Text = LocalizationManager.Instance["HostShare_ServerStopped"];
    }

    private void OnLogMessage(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{timestamp}] {message}";

            string current = LogText.Text ?? string.Empty;
            if (current.Length > 0)
                current += "\n";
            current += entry;

            // Keep log at a reasonable size, trimming at a line boundary
            const int maxLogLength = 8000;
            if (current.Length > maxLogLength)
            {
                int cutIndex = current.Length - maxLogLength;
                int newlineIndex = current.IndexOf('\n', cutIndex);
                current = newlineIndex >= 0 ? current[(newlineIndex + 1)..] : current[cutIndex..];
            }

            LogText.Text = current;
            LogScrollViewer.ScrollToEnd();
        });
    }

    private async void CopyUrlButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string? text = UrlsText.Text;
        if (string.IsNullOrEmpty(text)) return;

        // Copy the first URL
        string firstUrl = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            try
            {
                await clipboard.SetTextAsync(firstUrl);
                StatusText.Text = LocalizationManager.Instance["HostShare_UrlCopied"];
            }
            catch (Exception ex)
            {
                // Clipboard access can fail on some platforms (e.g. Wayland without focus)
                System.Diagnostics.Trace.WriteLine($"[HostRomsWindow] Clipboard write failed: {ex.Message}");
            }
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        StopServer();
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        StopServer();
        base.OnClosing(e);
    }
}
