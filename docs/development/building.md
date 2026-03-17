# Building from Source

Instructions for building and running Retro Multi Tools from source code.

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

## Clone and Build

```bash
git clone https://github.com/SvenGDK/RetroMultiTools.git
cd RetroMultiTools
dotnet build
```

The solution contains two projects:

| Project | Type | Description |
|---|---|---|
| `RetroMultiTools` | Avalonia Desktop (WinExe) | Main application |
| `RetroMultiTools.Updater` | Console (Exe) | External updater that applies downloaded updates |

`dotnet build` builds both projects.

## Run

```bash
dotnet run --project RetroMultiTools
```

## Platform-Specific Notes

### Linux

Avalonia UI requires several system libraries for rendering. Install them before building:

```bash
# Ubuntu / Debian
sudo apt install libicu-dev libfontconfig1 libx11-6 libice6 libsm6

# Fedora
sudo dnf install libicu fontconfig libX11 libICE libSM
```

If you encounter a `libSkiaSharp` error at runtime, install the OpenGL library:

```bash
# Ubuntu / Debian
sudo apt install libgl1-mesa-glx
```

See [LINUX.md](../../LINUX.md) for full Linux installation details.

### macOS

- Requires macOS 10.15 (Catalina) or later.
- On Apple Silicon Macs, ensure you install the ARM64 version of the .NET SDK.
- macOS Gatekeeper may block unsigned builds. Use `xattr -rd com.apple.quarantine RetroMultiTools` to remove the quarantine flag.

See [macOS.md](../../macOS.md) for full macOS installation details.

## Publish

### Framework-Dependent (requires .NET 8 Runtime on target machine)

```bash
dotnet publish RetroMultiTools -c Release -r win-x64 --no-self-contained
dotnet publish RetroMultiTools.Updater -c Release -r win-x64 --no-self-contained
```

### Self-Contained (includes .NET Runtime)

```bash
dotnet publish RetroMultiTools -c Release -r win-x64 --self-contained
dotnet publish RetroMultiTools.Updater -c Release -r win-x64 --self-contained
```

After publishing, copy the updater executable into the main application's output directory so it is bundled with the release.

### Supported Runtime Identifiers

| RID | Platform |
|---|---|
| `win-x64` | Windows 64-bit (Intel/AMD) |
| `win-arm64` | Windows ARM64 |
| `linux-x64` | Linux 64-bit (Intel/AMD) |
| `linux-arm64` | Linux ARM64 |
| `osx-x64` | macOS Intel |
| `osx-arm64` | macOS Apple Silicon |

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| Avalonia | 11.3.12 | Cross-platform UI framework |
| Avalonia.Controls.DataGrid | 11.3.12 | DataGrid control |
| Avalonia.Desktop | 11.3.12 | Desktop platform support |
| Avalonia.Themes.Fluent | 11.3.12 | Fluent design theme |
| Avalonia.Fonts.Inter | 11.3.12 | Inter font family |
| Avalonia.Diagnostics | 11.3.12 | Debug diagnostics (Debug builds only) |
| AWSSDK.S3 | 4.0.19 | Amazon S3 file transfers |
| FluentFTP | 53.0.2 | FTP / FTPS file transfers |
| SSH.NET | 2025.1.0 | SFTP file transfers |
