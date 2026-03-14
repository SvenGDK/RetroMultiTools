# Settings & Integration

Application settings, RetroArch integration, and remote file transfer.

---

## Language Settings

The application supports 20 languages. Change the language from the **Settings** view using the language dropdown. The change takes effect immediately.

### Supported Languages

English, Spanish, French, German, Portuguese, Italian, Japanese, Chinese (Simplified), Korean, Russian, Dutch, Polish, Turkish, Arabic, Hindi, Thai, Swedish, Czech, Vietnamese, Indonesian

---

## RetroArch Configuration

The Settings view provides tools for configuring and managing RetroArch integration.

### Path Configuration

| Action | Description |
|---|---|
| **Browse** | Manually select the RetroArch executable |
| **Auto-Detect** | Automatically find RetroArch on the system PATH or common install locations |
| **Download** | Opens the official RetroArch download page in a browser |
| **Clear** | Removes the stored RetroArch path |

The configured path is stored in `settings.json` inside the application data directory and persists across sessions.

### RetroArch Integration

Once configured, RetroArch is used by:

- **ROM Browser** — launch ROMs directly in RetroArch with the appropriate libretro core.
- **Core Downloader** — install missing cores into the RetroArch cores directory.

---

## RetroArch Core Downloader

Downloads and installs libretro cores from the official RetroArch buildbot.

### How It Works

1. Click **Check Cores** to scan the RetroArch `cores/` directory and compare installed cores against the list of cores used by the application.
2. A list shows each core with its install status (✔ installed / ✘ missing).
3. Click **Download Missing Cores** to download and install all missing cores.
4. A **Cancel** button is available during downloads.

### Platform Support

Cores are downloaded from `buildbot.libretro.com` for the current platform:

| Platform | Architecture |
|---|---|
| Windows | x86_64 |
| Linux | x86_64 |
| macOS | x86_64 or arm64 (auto-detected) |

### Core Directory

- **Windows / macOS** — the `cores/` directory next to the RetroArch executable.
- **Linux** — if the directory next to the executable is not writable (e.g., system package install), the downloader uses `~/.config/retroarch/cores/`.

---

## Remote Transfer (Send to Remote)

Transfer ROM files to remote targets directly from the ROM Browser.

### Supported Protocols

| Protocol | Default Port | Description |
|---|---|---|
| **FTP** | 21 | Standard FTP with optional Explicit FTPS (TLS encryption) |
| **SFTP** | 22 | SSH File Transfer Protocol |
| **WebDAV** | 443 | HTTP-based file transfer with automatic directory creation |
| **S3** | 443 | Amazon S3 or S3-compatible services (MinIO, Wasabi, etc.) |

### Connection Parameters

**FTP / SFTP / WebDAV:**

- Host
- Port
- Username
- Password
- Remote path (default: `/`)
- Use FTPS (FTP only)

**S3-Compatible:**

- Bucket name
- Access key
- Secret key
- Region (default: `us-east-1`)
- Service URL (optional, for non-AWS providers)

### Limits

- Maximum file size: **2 GB**
- Default connection timeout: **30 seconds**

### Usage

1. In the ROM Browser, select one or more ROMs.
2. Click **Send to Remote**.
3. Choose the protocol and enter connection details.
4. Click **Send** to begin the transfer.
5. Progress is reported per file. Transfers can be cancelled.

See [Remote Transfer Protocols](../reference/remote-transfer.md) for a detailed protocol reference.
