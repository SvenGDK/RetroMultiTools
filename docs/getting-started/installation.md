# Installation

How to download and install Retro Multi Tools on Windows, Linux, and macOS.

---

## Requirements

- **Windows** — Windows 10 or later (x64 or ARM64)
- **Linux** — A 64-bit distribution (x64 or ARM64)
- **macOS** — macOS 10.15 (Catalina) or later (Intel or Apple Silicon)

Framework-dependent builds require the [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). Self-contained builds include the runtime and have no prerequisites.

---

## Downloading a Release

Download the latest release from the [Releases](https://github.com/SvenGDK/RetroMultiTools/releases) page.

### Portable ZIPs

| File | Description |
|---|---|
| `win-x64.zip` | Windows 64-bit (Intel/AMD) |
| `win-arm64.zip` | Windows ARM64 |
| `linux-x64.zip` | Linux 64-bit (Intel/AMD) |
| `linux-arm64.zip` | Linux ARM64 |
| `osx-x64.zip` | macOS Intel |
| `osx-arm64.zip` | macOS Apple Silicon |

Self-contained portable ZIPs (e.g. `win-x64-Selfcontained.zip`) are also available for each platform.

### Installers

| File | Description |
|---|---|
| `win-x64-Installer.exe` | Windows 64-bit (Intel/AMD) |
| `win-arm64-Installer.exe` | Windows ARM64 |
| `linux-x64-Installer.deb` | Linux 64-bit (Intel/AMD) |
| `linux-arm64-Installer.deb` | Linux ARM64 |
| `osx-x64-Installer.pkg` | macOS Intel |
| `osx-arm64-Installer.pkg` | macOS Apple Silicon |

Self-contained installers (e.g. `win-x64-Selfcontained-Installer.exe`) are also available.

---

## Windows

1. Install the [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (skip for self-contained builds).
2. Extract the ZIP and run `RetroMultiTools.exe`.

---

## Linux

### Installing the .NET 8 Runtime

**Ubuntu / Debian:**

```bash
sudo apt update
sudo apt install dotnet-runtime-8.0
```

**Fedora:**

```bash
sudo dnf install dotnet-runtime-8.0
```

**Arch Linux:**

```bash
sudo pacman -S dotnet-runtime-8.0
```

**openSUSE:**

```bash
sudo zypper install dotnet-runtime-8.0
```

**Manual installation:** Download the binary from <https://dotnet.microsoft.com/download/dotnet/8.0>, extract it, and add the directory to your `PATH`:

```bash
mkdir -p $HOME/.dotnet
tar -xzf dotnet-runtime-8.0.*-linux-x64.tar.gz -C $HOME/.dotnet
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$HOME/.dotnet
```

Add the `export` lines to your `~/.bashrc` or `~/.zshrc` to make them permanent.

> **Note:** Self-contained builds do not require the .NET runtime.

### Required System Packages

Retro Multi Tools uses [Avalonia UI](https://avaloniaui.net/) which requires a few system libraries.

**Ubuntu / Debian:**

```bash
sudo apt install libicu-dev libfontconfig1 libx11-6 libice6 libsm6
```

**Fedora:**

```bash
sudo dnf install libicu fontconfig libX11 libICE libSM
```

**Arch Linux:**

```bash
sudo pacman -S icu fontconfig libx11 libice libsm
```

**openSUSE:**

```bash
sudo zypper install libicu-devel fontconfig libX11-6 libICE6 libSM6
```

### Running the Application

```bash
unzip linux-x64.zip -d RetroMultiTools
cd RetroMultiTools
chmod +x RetroMultiTools
./RetroMultiTools
```

---

## macOS

### Installing the .NET 8 Runtime

**Using the installer:**

1. Go to <https://dotnet.microsoft.com/download/dotnet/8.0>
2. Download the **macOS** installer for your Mac:
   - **Apple Silicon** (M1 / M2 / M3 / M4) — Arm64
   - **Intel** — x64
3. Open the `.pkg` file and follow the installation steps

**Using Homebrew:**

```bash
brew install dotnet@8
```

Verify the installation:

```bash
dotnet --info
```

> **Note:** Self-contained builds do not require the .NET runtime.

### Running the Application

```bash
unzip osx-arm64.zip -d RetroMultiTools
cd RetroMultiTools
chmod +x RetroMultiTools
./RetroMultiTools
```

### Gatekeeper Warning

macOS may block the application because it was not downloaded from the App Store. If you see a **"cannot be opened because the developer cannot be verified"** dialog:

1. Open **System Settings** → **Privacy & Security**
2. Scroll down and click **Open Anyway** next to the Retro Multi Tools message
3. Alternatively, right-click the application and select **Open**

Or remove the quarantine attribute from the terminal:

```bash
xattr -rd com.apple.quarantine RetroMultiTools
```

---

## Next Steps

Once installed, see [First Steps](first-steps.md) to get started with the application.
