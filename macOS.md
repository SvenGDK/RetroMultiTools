<div align="center">

# Running Retro Multi Tools on macOS

Step-by-step guide for installing and running Retro Multi Tools on macOS.

</div>

## Prerequisites

- macOS 10.15 (Catalina) or later

## Downloading a Release

Download the release that matches your Mac from the [Releases](https://github.com/SvenGDK/RetroMultiTools/releases) page.

### Portable ZIPs

| File | Description |
|---|---|
| `osx-x64.zip` | macOS Intel |
| `osx-arm64.zip` | macOS Apple Silicon |

### PKG Installers

| File | Description |
|---|---|
| `osx-x64-Installer.pkg` | macOS Intel |
| `osx-arm64-Installer.pkg` | macOS Apple Silicon |

### DMG Disk Images

| File | Description |
|---|---|
| `osx-x64.dmg` | macOS Intel |
| `osx-arm64.dmg` | macOS Apple Silicon |

## Installing from a Portable ZIP

1. Extract the downloaded ZIP — double-click the file in Finder or use the terminal:

```bash
unzip osx-arm64.zip -d RetroMultiTools
```

2. Navigate to the extracted directory:

```bash
cd RetroMultiTools
```

3. Make the binary executable:

```bash
chmod +x RetroMultiTools
```

4. Run the application:

```bash
./RetroMultiTools
```

## Installing from a PKG Installer

1. Double-click the `.pkg` file to open the installer.
2. Follow the on-screen instructions to install **Retro Multi Tools** to `/Applications`.
3. Launch the application from **Applications** or Spotlight.

## Installing from a DMG Disk Image

1. Double-click the `.dmg` file to mount the disk image.
2. Drag **Retro Multi Tools.app** to the **Applications** folder.
3. Eject the disk image.
4. Launch the application from **Applications** or Spotlight.

### Gatekeeper Warning

macOS may block the application because it was not downloaded from the App Store. If you see a **"cannot be opened because the developer cannot be verified"** dialog:

1. Open **System Settings** → **Privacy & Security**
2. Scroll down and click **Open Anyway** next to the Retro Multi Tools message
3. Alternatively, right-click the application and select **Open**

Or remove the quarantine attribute from the terminal:

```bash
xattr -rd com.apple.quarantine RetroMultiTools
```

## Building from Source

If you prefer to build from source, install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) instead of the runtime and then:

```bash
git clone https://github.com/SvenGDK/RetroMultiTools.git
cd RetroMultiTools
dotnet build
dotnet run --project RetroMultiTools
```

## Troubleshooting

### Fonts look incorrect

Install a common font package via Homebrew:

```bash
brew install font-liberation
```
