# Remote Transfer & Sharing Guide

How to send ROM files to remote targets and share them on the local network.

---

## Sending ROMs to a Remote Target

Transfer ROM files directly from the ROM Browser to a remote server or cloud storage.

### Supported Protocols

| Protocol | Default Port | Description |
|---|---|---|
| **FTP** | 21 | Standard FTP with optional Explicit FTPS (TLS encryption) |
| **SFTP** | 22 | SSH File Transfer Protocol |
| **WebDAV** | 443 | HTTP-based file transfer with automatic directory creation |
| **S3** | 443 | Amazon S3 or S3-compatible services (MinIO, Wasabi, etc.) |
| **Google Drive** | — | Google Drive API v3 upload via OAuth token |
| **Dropbox** | — | Dropbox API v2 upload via OAuth token |
| **OneDrive** | — | Microsoft Graph API upload via OAuth token |

### How to Send

1. In the ROM Browser, select one or more ROMs.
2. Click **Send to Remote**.
3. Choose the protocol and enter connection details.
4. Click **Send** to begin the transfer.
5. Progress is reported per file. Transfers can be cancelled.

### Connection Parameters

**FTP / SFTP / WebDAV:**

- Host, Port, Username, Password
- Remote path (default: `/`)
- Use FTPS (FTP only)

**S3-Compatible:**

- Bucket name, Access key, Secret key
- Region (default: `us-east-1`)
- Service URL (optional, for non-AWS providers)

**Cloud Storage (Google Drive, Dropbox, OneDrive):**

- OAuth access token
- Folder ID (Google Drive, optional)

### Limits

- Maximum file size: **2 GB**
- Default connection timeout: **30 seconds**

For detailed protocol specifications, see the [Remote Transfer Reference](../reference/remote-transfer.md).

---

## Host & Share

Share ROM files with other devices on the local network via a built-in HTTP server.

### Hosting Modes

| Mode | Description |
|---|---|
| **Directory** | Serves all ROM files in the browsed directory (when no ROMs are selected) |
| **Selected ROMs** | Serves only the selected ROM(s) from the ROM Browser list |

### How to Host

1. In the ROM Browser, optionally select ROMs to share (or leave none selected for directory mode).
2. Click **Host & Share** in the toolbar or context menu.
3. Set the port (default: **8080**) and click **Start**.
4. Share the displayed URL with others on your network.
5. Recipients open the URL in any web browser to browse and download the shared files.

### Features

- Resumable downloads via HTTP range requests
- Concurrent connections from multiple clients
- Path-traversal protection (only specified files are accessible)
- Real-time connection log
- Cross-platform (Windows, Linux, macOS) with no extra dependencies
