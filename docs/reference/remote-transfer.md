# Remote Transfer Protocols

Reference for the remote file transfer protocols supported by the Send to Remote feature.

---

## FTP

Standard File Transfer Protocol with optional TLS encryption.

### Connection Parameters

| Parameter | Required | Description |
|---|---|---|
| Host | Yes | Server hostname or IP address |
| Port | Yes | Server port (default: 21) |
| Username | Yes | FTP username |
| Password | Yes | FTP password |
| Remote Path | No | Upload directory (default: `/`) |
| Use FTPS | No | Enable Explicit FTPS (TLS encryption) |

### FTPS (Explicit TLS)

When FTPS is enabled, the connection starts as plain FTP on port 21 and upgrades to TLS using the `AUTH TLS` command. This is also known as Explicit FTPS (FTPES).

### Implementation Notes

- Uses the [FluentFTP](https://github.com/robinrodricks/FluentFTP) library.
- The remote directory is created automatically if it does not exist.

---

## SFTP

SSH File Transfer Protocol — encrypted file transfer over SSH.

### Connection Parameters

| Parameter | Required | Description |
|---|---|---|
| Host | Yes | Server hostname or IP address |
| Port | Yes | Server port (default: 22) |
| Username | Yes | SSH username |
| Password | Yes | SSH password |
| Remote Path | No | Upload directory (default: `/`) |

### Implementation Notes

- Uses the [SSH.NET](https://github.com/sshnet/SSH.NET) library.
- The remote directory is created automatically if it does not exist.
- Password authentication only (key-based authentication is not currently supported).

---

## WebDAV

HTTP-based file transfer protocol. Compatible with NextCloud, ownCloud, Apache mod_dav, and other WebDAV servers.

### Connection Parameters

| Parameter | Required | Description |
|---|---|---|
| Host | Yes | Server URL (e.g., `https://cloud.example.com/remote.php/dav/files/user/`) |
| Port | Yes | Server port (default: 443) |
| Username | Yes | WebDAV username |
| Password | Yes | WebDAV password |
| Remote Path | No | Upload subdirectory (default: `/`) |

### Implementation Notes

- Uses the built-in `HttpClient`.
- Directories are created with the WebDAV `MKCOL` method before upload.
- The `PUT` method is used for file upload.

---

## S3-Compatible

Amazon S3 and S3-compatible object storage services (MinIO, Wasabi, Backblaze B2, DigitalOcean Spaces, etc.).

### Connection Parameters

| Parameter | Required | Description |
|---|---|---|
| Bucket Name | Yes | S3 bucket name |
| Access Key | Yes | AWS access key ID or equivalent |
| Secret Key | Yes | AWS secret access key or equivalent |
| Region | Yes | AWS region (default: `us-east-1`) |
| Service URL | No | Custom endpoint URL for non-AWS providers |
| Remote Path | No | Key prefix / folder path (default: `/`) |

### Non-AWS Providers

For S3-compatible services, set the **Service URL** to the provider's endpoint:

| Provider | Service URL Example |
|---|---|
| MinIO | `http://minio.local:9000` |
| Wasabi | `https://s3.wasabisys.com` |
| Backblaze B2 | `https://s3.us-west-002.backblazeb2.com` |
| DigitalOcean Spaces | `https://nyc3.digitaloceanspaces.com` |

### Implementation Notes

- Uses the [AWS SDK for .NET (AWSSDK.S3)](https://aws.amazon.com/sdk-for-net/).
- When a custom Service URL is provided, `ForcePathStyle` is enabled for compatibility with non-AWS services.

---

## Google Drive

Upload files to Google Drive using the Google Drive API v3.

### Connection Parameters

| Parameter | Required | Description |
|---|---|---|
| OAuth Access Token | Yes | A valid OAuth 2.0 access token with `drive.file` or `drive` scope |
| Folder ID | No | Google Drive folder ID to upload into (default: `root` — the user's top-level My Drive) |
| Remote Path | No | Not used directly; the folder ID determines the upload location |

### Obtaining an OAuth Token

Generate an access token through the [Google OAuth 2.0 Playground](https://developers.google.com/oauthplayground/) or your own OAuth application. The token must have the `https://www.googleapis.com/auth/drive.file` scope at minimum.

### Implementation Notes

- Uses the Google Drive REST API v3 multipart upload endpoint.
- File metadata (name and parent folder) is sent alongside the file content in a single multipart request.
- No additional NuGet packages are required; uploads use the built-in `HttpClient`.

---

## Dropbox

Upload files to Dropbox using the Dropbox API v2.

### Connection Parameters

| Parameter | Required | Description |
|---|---|---|
| OAuth Access Token | Yes | A valid Dropbox OAuth 2.0 access token |
| Remote Path | No | Upload directory path in Dropbox (default: `/`) |

### Obtaining an OAuth Token

Generate an access token from the [Dropbox App Console](https://www.dropbox.com/developers/apps) or use the Dropbox OAuth 2.0 flow.

### Implementation Notes

- Uses the Dropbox content upload endpoint (`https://content.dropboxapi.com/2/files/upload`).
- Upload metadata is passed via the `Dropbox-API-Arg` HTTP header.
- Existing files at the same path are overwritten.
- No additional NuGet packages are required; uploads use the built-in `HttpClient`.

---

## OneDrive

Upload files to Microsoft OneDrive using the Microsoft Graph API.

### Connection Parameters

| Parameter | Required | Description |
|---|---|---|
| OAuth Access Token | Yes | A valid Microsoft Graph OAuth 2.0 access token with `Files.ReadWrite` scope |
| Remote Path | No | Upload path relative to the root of the user's OneDrive (default: `/`) |

### Obtaining an OAuth Token

Generate an access token through the [Microsoft Graph Explorer](https://developer.microsoft.com/graph/graph-explorer) or register an application in the [Azure Portal](https://portal.azure.com). The token must have the `Files.ReadWrite` permission.

### Implementation Notes

- Uses the Microsoft Graph API simple upload endpoint (`PUT /me/drive/root:/{path}:/content`).
- Suitable for files up to the 2 GB limit. For larger files, the resumable upload session API would be required.
- Existing files at the same path are overwritten.
- No additional NuGet packages are required; uploads use the built-in `HttpClient`.

---

## Common Limits

| Setting | Value |
|---|---|
| Maximum file size | 2 GB |
| Connection timeout | 30 seconds |
| Progress reporting | Per-file status updates |
| Cancellation | Supported via cancel button |
