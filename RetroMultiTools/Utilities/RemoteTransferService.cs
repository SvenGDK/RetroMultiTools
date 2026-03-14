using System.Net;
using System.Net.Http.Headers;
using Amazon.S3;
using Amazon.S3.Transfer;
using FluentFTP;
using Renci.SshNet;

namespace RetroMultiTools.Utilities;

public static class RemoteTransferService
{
    private const int DefaultTimeoutSeconds = 30;
    private const long MaxFileSizeBytes = 2L * 1024 * 1024 * 1024; // 2 GB

    public static async Task SendViaFtpAsync(
        string filePath,
        string host,
        int port,
        string username,
        string password,
        string remotePath,
        bool useFtps,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ValidateFilePath(filePath);

        var config = new FtpConfig
        {
            ConnectTimeout = DefaultTimeoutSeconds * 1000,
            DataConnectionConnectTimeout = DefaultTimeoutSeconds * 1000,
            ReadTimeout = DefaultTimeoutSeconds * 1000
        };

        if (useFtps)
        {
            config.EncryptionMode = FtpEncryptionMode.Explicit;
        }

        using var client = new AsyncFtpClient(host, username, password, port, config);
        await client.Connect(cancellationToken).ConfigureAwait(false);

        string fileName = Path.GetFileName(filePath);
        string remoteFilePath = CombineRemotePath(remotePath, fileName);

        progress?.Report($"Uploading {fileName} via FTP...");

        await client.UploadFile(
            filePath,
            remoteFilePath,
            FtpRemoteExists.Overwrite,
            createRemoteDir: true,
            token: cancellationToken).ConfigureAwait(false);

        progress?.Report($"Uploaded {fileName} successfully.");
    }

    public static async Task SendViaSftpAsync(
        string filePath,
        string host,
        int port,
        string username,
        string password,
        string remotePath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ValidateFilePath(filePath);

        string fileName = Path.GetFileName(filePath);
        string remoteFilePath = CombineRemotePath(remotePath, fileName);

        progress?.Report($"Uploading {fileName} via SFTP...");

        await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();

            EnsureSftpDirectoryExists(client, remotePath);

            // Allow cancellation during upload by disconnecting on cancel
            using var registration = cancellationToken.Register(() =>
            {
                try { client.Disconnect(); } catch { /* connection teardown */ }
            });

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            client.UploadFile(fileStream, remoteFilePath, canOverride: true);

            client.Disconnect();
        }, cancellationToken).ConfigureAwait(false);

        progress?.Report($"Uploaded {fileName} successfully.");
    }

    public static async Task SendViaWebDavAsync(
        string filePath,
        string host,
        int port,
        string username,
        string password,
        string remotePath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ValidateFilePath(filePath);

        string fileName = Path.GetFileName(filePath);
        string remoteFilePath = CombineRemotePath(remotePath, fileName);

        var scheme = port == 443 ? "https" : "http";
        var baseUri = new Uri($"{scheme}://{host}:{port}");
        var fileUri = new Uri(baseUri, remoteFilePath);

        progress?.Report($"Uploading {fileName} via WebDAV...");

        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(username, password),
            PreAuthenticate = true
        };

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        // Ensure remote directory exists via MKCOL
        await EnsureWebDavDirectoryAsync(httpClient, baseUri, remotePath, cancellationToken).ConfigureAwait(false);

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var request = new HttpRequestMessage(HttpMethod.Put, fileUri) { Content = content };
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Created
            && response.StatusCode != HttpStatusCode.NoContent)
        {
            throw new HttpRequestException(
                $"WebDAV upload failed with status {(int)response.StatusCode} {response.StatusCode}.");
        }

        progress?.Report($"Uploaded {fileName} successfully.");
    }

    public static async Task SendViaS3Async(
        string filePath,
        string bucketName,
        string accessKey,
        string secretKey,
        string region,
        string? serviceUrl,
        string remotePath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ValidateFilePath(filePath);

        string fileName = Path.GetFileName(filePath);
        string objectKey = CombineRemotePath(remotePath, fileName).TrimStart('/');

        progress?.Report($"Uploading {fileName} to S3...");

        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
            Timeout = TimeSpan.FromMinutes(30)
        };

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            s3Config.ServiceURL = serviceUrl;
            s3Config.ForcePathStyle = true;
        }

        var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
        using var s3Client = new AmazonS3Client(credentials, s3Config);

        var transferUtility = new TransferUtility(s3Client);
        var uploadRequest = new TransferUtilityUploadRequest
        {
            FilePath = filePath,
            BucketName = bucketName,
            Key = objectKey
        };

        await transferUtility.UploadAsync(uploadRequest, cancellationToken).ConfigureAwait(false);

        progress?.Report($"Uploaded {fileName} successfully.");
    }

    private static void ValidateFilePath(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("ROM file not found.", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"File exceeds the maximum allowed size of {MaxFileSizeBytes / (1024.0 * 1024 * 1024):F0} GB.");
    }

    private static string CombineRemotePath(string directory, string fileName)
    {
        string dir = directory.TrimEnd('/');
        if (string.IsNullOrEmpty(dir))
            dir = "/";
        return dir + "/" + fileName;
    }

    private static void EnsureSftpDirectoryExists(SftpClient client, string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath) || remotePath == "/")
            return;

        string[] parts = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "";

        foreach (string part in parts)
        {
            current += "/" + part;
            if (!client.Exists(current))
                client.CreateDirectory(current);
        }
    }

    private static async Task EnsureWebDavDirectoryAsync(
        HttpClient httpClient,
        Uri baseUri,
        string remotePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remotePath) || remotePath == "/")
            return;

        string[] parts = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "";

        foreach (string part in parts)
        {
            current += "/" + part + "/";
            var dirUri = new Uri(baseUri, current);

            using var request = new HttpRequestMessage(new HttpMethod("MKCOL"), dirUri);
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // 201 Created, 405 Method Not Allowed (already exists), or 301/409 are acceptable
            // Only throw on unexpected server errors
            if ((int)response.StatusCode >= 500)
            {
                throw new HttpRequestException(
                    $"WebDAV MKCOL failed for '{current}' with status {(int)response.StatusCode}.");
            }
        }
    }
}
