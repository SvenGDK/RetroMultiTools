<div align="center">

# Running Retro Multi Tools on Linux

Step-by-step guide for installing and running Retro Multi Tools on Linux.

</div>

## Prerequisites

- A 64-bit Linux distribution (x64 or ARM64)

## Required System Packages

Retro Multi Tools uses [Avalonia UI](https://avaloniaui.net/) which requires a few system libraries for rendering.

### Ubuntu / Debian

```bash
sudo apt install libicu-dev libfontconfig1 libx11-6 libice6 libsm6
```

### Fedora / CentOS Stream / RHEL / Rocky Linux / AlmaLinux / Oracle Linux

```bash
sudo dnf install libicu fontconfig libX11 libICE libSM
```

### Arch Linux

```bash
sudo pacman -S icu fontconfig libx11 libice libsm
```

### Alpine Linux

```bash
sudo apk add icu-libs fontconfig libx11 libice libsm
```

### openSUSE / SUSE Linux Enterprise Server (SLES)

```bash
sudo zypper install libicu-devel fontconfig libX11-6 libICE6 libSM6
```

### Linux Mint

Linux Mint is based on Ubuntu, so the same packages apply:

```bash
sudo apt install libicu-dev libfontconfig1 libx11-6 libice6 libsm6
```

## Downloading a Release

Download the release that matches your architecture from the [Releases](https://github.com/SvenGDK/RetroMultiTools/releases) page.

### Portable ZIPs

| File | Description |
|---|---|
| `linux-x64.zip` | Linux 64-bit (Intel/AMD) |
| `linux-arm64.zip` | Linux ARM64 |

### DEB Installers

| File | Description |
|---|---|
| `linux-x64-Installer.deb` | Linux 64-bit (Intel/AMD) |
| `linux-arm64-Installer.deb` | Linux ARM64 |

### RPM Packages

| File | Description |
|---|---|
| `linux-x64.rpm` | Linux 64-bit (Intel/AMD) |
| `linux-arm64.rpm` | Linux ARM64 |

### APK Packages (Alpine Linux)

| File | Description |
|---|---|
| `linux-x64.apk` | Linux 64-bit (Intel/AMD) |
| `linux-arm64.apk` | Linux ARM64 |

### Pacman Packages (Arch Linux)

| File | Description |
|---|---|
| `linux-x64.pkg.tar.zst` | Linux 64-bit (Intel/AMD) |
| `linux-arm64.pkg.tar.zst` | Linux ARM64 |

### AppImage

| File | Description |
|---|---|
| `linux-x64.AppImage` | Linux 64-bit (Intel/AMD) |
| `linux-arm64.AppImage` | Linux ARM64 |

### Snap

| File | Description |
|---|---|
| `linux-x64.snap` | Linux 64-bit (Intel/AMD) |
| `linux-arm64.snap` | Linux ARM64 |

### Flatpak

| File | Description |
|---|---|
| `linux-x64.flatpak` | Linux 64-bit (Intel/AMD) |
| `linux-arm64.flatpak` | Linux ARM64 |

## Installing from a Portable ZIP

1. Extract the downloaded ZIP:

```bash
unzip linux-x64.zip -d RetroMultiTools
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

## Installing from a DEB Package

```bash
sudo dpkg -i linux-x64-Installer.deb
```

Then launch from the application menu or run:

```bash
retromultitools
```

## Installing from an RPM Package

### Fedora / CentOS Stream / RHEL / Rocky Linux / AlmaLinux / Oracle Linux

```bash
sudo dnf install linux-x64.rpm
```

### openSUSE / SUSE Linux Enterprise Server (SLES)

```bash
sudo zypper install linux-x64.rpm
```

Then launch from the application menu or run:

```bash
retromultitools
```

## Installing from an APK Package (Alpine Linux)

```bash
sudo apk add --allow-untrusted linux-x64.apk
```

Then launch from the application menu or run:

```bash
retromultitools
```

## Installing from a Pacman Package (Arch Linux)

```bash
sudo pacman -U linux-x64.pkg.tar.zst
```

Then launch from the application menu or run:

```bash
retromultitools
```

## Installing from an AppImage

1. Make the AppImage executable:

```bash
chmod +x linux-x64.AppImage
```

2. Run the AppImage:

```bash
./linux-x64.AppImage
```

No installation is required — AppImage bundles everything into a single file.

## Installing from a Snap

```bash
sudo snap install --dangerous linux-x64.snap
```

Then launch from the application menu or run:

```bash
snap run retromultitools
```

## Installing from a Flatpak

```bash
flatpak install --user linux-x64.flatpak
```

Then launch from the application menu or run:

```bash
flatpak run io.github.svengdk.RetroMultiTools
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

### Application does not start

Make sure all required system packages are installed (see [Required System Packages](#required-system-packages)).

### Missing libSkiaSharp

If you see an error about `libSkiaSharp`, install the OpenGL library for your distribution:

```bash
# Ubuntu / Debian / Linux Mint
sudo apt install libgl1-mesa-glx

# Fedora / CentOS Stream / RHEL / Rocky Linux / AlmaLinux / Oracle Linux
sudo dnf install mesa-libGL

# Arch Linux
sudo pacman -S mesa

# Alpine Linux
sudo apk add mesa-gl

# openSUSE / SLES
sudo zypper install Mesa-libGL1
```

### Fonts look incorrect

Install a common font package:

```bash
# Ubuntu / Debian / Linux Mint
sudo apt install fonts-liberation

# Fedora / CentOS Stream / RHEL / Rocky Linux / AlmaLinux / Oracle Linux
sudo dnf install liberation-fonts

# Arch Linux
sudo pacman -S ttf-liberation

# Alpine Linux
sudo apk add font-liberation

# openSUSE / SLES
sudo zypper install liberation-fonts
```
