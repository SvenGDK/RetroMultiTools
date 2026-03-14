using Avalonia.Controls;
using RetroMultiTools.Models;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class SendToRemoteWindow : Window
{
    private readonly List<RomInfo> _romFiles;
    private CancellationTokenSource? _transferCts;

    public SendToRemoteWindow()
    {
        InitializeComponent();
        _romFiles = new List<RomInfo>();
    }

    public SendToRemoteWindow(List<RomInfo> romFiles)
    {
        InitializeComponent();
        _romFiles = romFiles;

        long totalSize = _romFiles.Sum(r => r.FileSize);
        FileInfoText.Text = $"{_romFiles.Count} ROM(s) selected — {FileUtils.FormatFileSize(totalSize)} total";
        UpdateProtocolUI();
    }

    private void ProtocolCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateProtocolUI();
    }

    private void UpdateProtocolUI()
    {
        if (ProtocolCombo?.SelectedItem is not ComboBoxItem selected) return;

        var protocol = ParseProtocol(selected.Tag?.ToString());

        UseFtpsCheck.IsVisible = protocol == TransferProtocol.Ftp;
        CredentialsPanel.IsVisible = protocol != TransferProtocol.S3Compatible;
        S3Panel.IsVisible = protocol == TransferProtocol.S3Compatible;

        PortTextBox.Watermark = RemoteTarget.DefaultPort(protocol).ToString();
    }

    private async void SendButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_romFiles.Count == 0) return;

        var target = BuildTarget();
        var validation = ValidateTarget(target);
        if (validation != null)
        {
            StatusText.Text = validation;
            return;
        }

        SendButton.IsEnabled = false;
        CancelButton.Content = "Stop";
        TransferProgressBar.IsVisible = true;

        _transferCts?.Dispose();
        _transferCts = new CancellationTokenSource();
        var token = _transferCts.Token;

        int sent = 0;
        int failed = 0;

        try
        {
            foreach (var rom in _romFiles)
            {
                if (token.IsCancellationRequested) break;

                var progress = new Progress<string>(msg => StatusText.Text = msg);

                try
                {
                    await SendFileAsync(rom.FilePath, target, progress, token);
                    sent++;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    failed++;
                    StatusText.Text = $"Failed: {rom.FileName} — {ex.Message}";
                }
                catch (HttpRequestException ex)
                {
                    failed++;
                    StatusText.Text = $"Failed: {rom.FileName} — {ex.Message}";
                }
                catch (InvalidOperationException ex)
                {
                    failed++;
                    StatusText.Text = $"Failed: {rom.FileName} — {ex.Message}";
                }
            }

            if (token.IsCancellationRequested)
            {
                StatusText.Text = $"Transfer cancelled. Sent {sent} ROM(s)" +
                                  (failed > 0 ? $", {failed} failed." : ".");
            }
            else
            {
                StatusText.Text = $"Transfer complete. Sent {sent} ROM(s)" +
                                  (failed > 0 ? $", {failed} failed." : " successfully.");
            }
        }
        finally
        {
            TransferProgressBar.IsVisible = false;
            SendButton.IsEnabled = true;
            CancelButton.Content = "Close";
            _transferCts?.Dispose();
            _transferCts = null;
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_transferCts != null)
        {
            _transferCts.Cancel();
            StatusText.Text = "Cancelling...";
        }
        else
        {
            Close();
        }
    }

    private static async Task SendFileAsync(
        string filePath,
        RemoteTarget target,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        switch (target.Protocol)
        {
            case TransferProtocol.Ftp:
                await RemoteTransferService.SendViaFtpAsync(
                    filePath, target.Host, target.Port, target.Username, target.Password,
                    target.RemotePath, target.UseFtps, progress, cancellationToken);
                break;

            case TransferProtocol.Sftp:
                await RemoteTransferService.SendViaSftpAsync(
                    filePath, target.Host, target.Port, target.Username, target.Password,
                    target.RemotePath, progress, cancellationToken);
                break;

            case TransferProtocol.WebDav:
                await RemoteTransferService.SendViaWebDavAsync(
                    filePath, target.Host, target.Port, target.Username, target.Password,
                    target.RemotePath, progress, cancellationToken);
                break;

            case TransferProtocol.S3Compatible:
                await RemoteTransferService.SendViaS3Async(
                    filePath, target.BucketName, target.AccessKey, target.SecretKey,
                    target.Region, target.ServiceUrl, target.RemotePath,
                    progress, cancellationToken);
                break;
        }
    }

    private RemoteTarget BuildTarget()
    {
        var protocol = GetSelectedProtocol();
        int port = int.TryParse(PortTextBox.Text, out int p) ? p : RemoteTarget.DefaultPort(protocol);

        return new RemoteTarget
        {
            Protocol = protocol,
            Host = HostTextBox.Text?.Trim() ?? string.Empty,
            Port = port,
            Username = UsernameTextBox.Text?.Trim() ?? string.Empty,
            Password = PasswordTextBox.Text ?? string.Empty,
            RemotePath = RemotePathTextBox.Text?.Trim() ?? "/",
            UseFtps = UseFtpsCheck.IsChecked == true,
            BucketName = BucketTextBox.Text?.Trim() ?? string.Empty,
            AccessKey = AccessKeyTextBox.Text?.Trim() ?? string.Empty,
            SecretKey = SecretKeyTextBox.Text ?? string.Empty,
            Region = RegionTextBox.Text?.Trim() ?? "us-east-1",
            ServiceUrl = string.IsNullOrWhiteSpace(ServiceUrlTextBox.Text) ? null : ServiceUrlTextBox.Text.Trim()
        };
    }

    private TransferProtocol GetSelectedProtocol()
    {
        if (ProtocolCombo.SelectedItem is ComboBoxItem item)
            return ParseProtocol(item.Tag?.ToString());
        return TransferProtocol.Ftp;
    }

    private static TransferProtocol ParseProtocol(string? tag) => tag switch
    {
        "Ftp" => TransferProtocol.Ftp,
        "Sftp" => TransferProtocol.Sftp,
        "WebDav" => TransferProtocol.WebDav,
        "S3Compatible" => TransferProtocol.S3Compatible,
        _ => TransferProtocol.Ftp
    };

    private static string? ValidateTarget(RemoteTarget target)
    {
        if (target.Protocol == TransferProtocol.S3Compatible)
        {
            if (string.IsNullOrWhiteSpace(target.BucketName))
                return "Bucket name is required.";
            if (string.IsNullOrWhiteSpace(target.AccessKey))
                return "Access key is required.";
            if (string.IsNullOrWhiteSpace(target.SecretKey))
                return "Secret key is required.";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(target.Host))
                return "Host is required.";
            if (string.IsNullOrWhiteSpace(target.Username))
                return "Username is required.";
        }

        if (target.Port <= 0 || target.Port > 65535)
            return "Port must be between 1 and 65535.";

        return null;
    }
}
