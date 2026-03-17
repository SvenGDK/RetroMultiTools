namespace RetroMultiTools.Models;

public enum TransferProtocol
{
    Ftp,
    Sftp,
    WebDav,
    S3Compatible,
    GoogleDrive,
    Dropbox,
    OneDrive
}

public class RemoteTarget
{
    public TransferProtocol Protocol { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RemotePath { get; set; } = "/";
    public bool UseFtps { get; set; }

    // S3-specific
    public string BucketName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string? ServiceUrl { get; set; }

    // Cloud storage (Google Drive, Dropbox, OneDrive)
    public string OAuthToken { get; set; } = string.Empty;
    public string CloudFolderId { get; set; } = string.Empty;

    public static int DefaultPort(TransferProtocol protocol) => protocol switch
    {
        TransferProtocol.Ftp => 21,
        TransferProtocol.Sftp => 22,
        TransferProtocol.WebDav => 443,
        TransferProtocol.S3Compatible => 443,
        TransferProtocol.GoogleDrive => 443,
        TransferProtocol.Dropbox => 443,
        TransferProtocol.OneDrive => 443,
        _ => 0
    };
}
