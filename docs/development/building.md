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

## Run

```bash
dotnet run --project RetroMultiTools
```

## Publish

### Framework-Dependent (requires .NET 8 Runtime on target machine)

```bash
dotnet publish RetroMultiTools -c Release -r win-x64 --no-self-contained
dotnet publish RetroMultiTools -c Release -r linux-x64 --no-self-contained
dotnet publish RetroMultiTools -c Release -r osx-arm64 --no-self-contained
```

### Self-Contained (includes .NET Runtime)

```bash
dotnet publish RetroMultiTools -c Release -r win-x64 --self-contained
dotnet publish RetroMultiTools -c Release -r linux-x64 --self-contained
dotnet publish RetroMultiTools -c Release -r osx-arm64 --self-contained
```

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
