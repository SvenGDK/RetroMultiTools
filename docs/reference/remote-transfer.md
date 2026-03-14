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

## Common Limits

| Setting | Value |
|---|---|
| Maximum file size | 2 GB |
| Connection timeout | 30 seconds |
| Progress reporting | Per-file status updates |
| Cancellation | Supported via cancel button |
