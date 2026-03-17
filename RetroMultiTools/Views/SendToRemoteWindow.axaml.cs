using Avalonia.Controls;
using RetroMultiTools.Localization;
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
        FileInfoText.Text = string.Format(LocalizationManager.Instance["Send_RomsSelected"], _romFiles.Count, FileUtils.FormatFileSize(totalSize));
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

        bool isCloud = protocol is TransferProtocol.GoogleDrive
                    or TransferProtocol.Dropbox
                    or TransferProtocol.OneDrive;

        HostPanel.IsVisible = !isCloud;
        PortPanel.IsVisible = !isCloud;
        UseFtpsCheck.IsVisible = protocol == TransferProtocol.Ftp;
        CredentialsPanel.IsVisible = !isCloud && protocol != TransferProtocol.S3Compatible;
        S3Panel.IsVisible = protocol == TransferProtocol.S3Compatible;
        CloudPanel.IsVisible = isCloud;

        // Google Drive uses folder ID; Dropbox/OneDrive use remote path only
        CloudFolderIdLabel.IsVisible = protocol == TransferProtocol.GoogleDrive;
        CloudFolderIdTextBox.IsVisible = protocol == TransferProtocol.GoogleDrive;

        if (!isCloud)
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
        CancelButton.Content = LocalizationManager.Instance["Send_Stop"];
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
                catch (Exception ex) when (ex is IOException or HttpRequestException or InvalidOperationException)
                {
                    failed++;
                    StatusText.Text = string.Format(LocalizationManager.Instance["Send_FailedFile"], rom.FileName, ex.Message);
                }
            }

            if (token.IsCancellationRequested)
            {
                string suffix = failed > 0 ? string.Format(LocalizationManager.Instance["Send_FailedSuffix"], failed) : ".";
                StatusText.Text = string.Format(LocalizationManager.Instance["Send_TransferCancelled"], sent, suffix);
            }
            else
            {
                string suffix = failed > 0 ? string.Format(LocalizationManager.Instance["Send_FailedSuffix"], failed) : LocalizationManager.Instance["Send_SuccessSuffix"];
                StatusText.Text = string.Format(LocalizationManager.Instance["Send_TransferComplete"], sent, suffix);
            }
        }
        finally
        {
            TransferProgressBar.IsVisible = false;
            SendButton.IsEnabled = true;
            CancelButton.Content = LocalizationManager.Instance["Send_Close"];
            _transferCts?.Dispose();
            _transferCts = null;
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_transferCts != null)
        {
            _transferCts.Cancel();
            StatusText.Text = LocalizationManager.Instance["Send_Cancelling"];
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

            case TransferProtocol.GoogleDrive:
                await RemoteTransferService.SendViaGoogleDriveAsync(
                    filePath, target.OAuthToken, target.CloudFolderId,
                    target.RemotePath, progress, cancellationToken);
                break;

            case TransferProtocol.Dropbox:
                await RemoteTransferService.SendViaDropboxAsync(
                    filePath, target.OAuthToken, target.RemotePath,
                    progress, cancellationToken);
                break;

            case TransferProtocol.OneDrive:
                await RemoteTransferService.SendViaOneDriveAsync(
                    filePath, target.OAuthToken, target.RemotePath,
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
            ServiceUrl = string.IsNullOrWhiteSpace(ServiceUrlTextBox.Text) ? null : ServiceUrlTextBox.Text.Trim(),
            OAuthToken = OAuthTokenTextBox.Text ?? string.Empty,
            CloudFolderId = CloudFolderIdTextBox.Text?.Trim() ?? string.Empty
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
        "GoogleDrive" => TransferProtocol.GoogleDrive,
        "Dropbox" => TransferProtocol.Dropbox,
        "OneDrive" => TransferProtocol.OneDrive,
        _ => TransferProtocol.Ftp
    };

    private static string? ValidateTarget(RemoteTarget target)
    {
        if (target.Protocol == TransferProtocol.S3Compatible)
        {
            if (string.IsNullOrWhiteSpace(target.BucketName))
                return LocalizationManager.Instance["Send_BucketRequired"];
            if (string.IsNullOrWhiteSpace(target.AccessKey))
                return LocalizationManager.Instance["Send_AccessKeyRequired"];
            if (string.IsNullOrWhiteSpace(target.SecretKey))
                return LocalizationManager.Instance["Send_SecretKeyRequired"];
        }
        else if (target.Protocol is TransferProtocol.GoogleDrive
                 or TransferProtocol.Dropbox
                 or TransferProtocol.OneDrive)
        {
            if (string.IsNullOrWhiteSpace(target.OAuthToken))
                return LocalizationManager.Instance["Send_OAuthRequired"];
        }
        else
        {
            if (string.IsNullOrWhiteSpace(target.Host))
                return LocalizationManager.Instance["Send_HostRequired"];
            if (string.IsNullOrWhiteSpace(target.Username))
                return LocalizationManager.Instance["Send_UsernameRequired"];
        }

        bool isCloud = target.Protocol is TransferProtocol.GoogleDrive
                    or TransferProtocol.Dropbox
                    or TransferProtocol.OneDrive;

        if (!isCloud && (target.Port <= 0 || target.Port > 65535))
            return LocalizationManager.Instance["Send_InvalidPort"];

        return null;
    }
}
