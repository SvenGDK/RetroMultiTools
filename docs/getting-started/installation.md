# Installation

How to download and install Retro Multi Tools on Windows, Linux, macOS, and FreeBSD.

---

## Requirements

- **Windows** — Windows 10 or later (x64 or ARM64)
- **Linux** — A 64-bit distribution (x64 or ARM64)
- **macOS** — macOS 10.15 (Catalina) or later (Intel or Apple Silicon)
- **FreeBSD** — FreeBSD 13.0 or later (x64 or ARM64) with Linux binary compatibility enabled

All release builds are self-contained and include the .NET runtime — no separate runtime installation is required.

---

## Downloading a Release

Download the latest release from the [Releases](https://github.com/SvenGDK/RetroMultiTools/releases) page.

| Platform | Architectures | Formats |
|---|---|---|
| **Windows** | x64, ARM64 | ZIP, Installer (`.exe`) |
| **Linux** | x64, ARM64 | ZIP, DEB, RPM, APK, Pacman, AppImage, Snap, Flatpak |
| **macOS** | Intel, Apple Silicon | ZIP, PKG, DMG |
| **FreeBSD** | x64, ARM64 | ZIP, PKG |

---

## Windows

### Portable ZIP

Extract the ZIP and run `RetroMultiTools.exe`.

### Installer

Run the `.exe` installer and follow the on-screen instructions. A desktop shortcut can optionally be created during installation.

---

## Linux

### Required System Packages

Retro Multi Tools uses [Avalonia UI](https://avaloniaui.net/) which requires a few system libraries.

**Ubuntu / Debian / Linux Mint:**

```bash
sudo apt install libicu-dev libfontconfig1 libx11-6 libice6 libsm6
```

**Fedora / CentOS Stream / RHEL / Rocky Linux / AlmaLinux / Oracle Linux:**

```bash
sudo dnf install libicu fontconfig libX11 libICE libSM
```

**Arch Linux:**

```bash
sudo pacman -S icu fontconfig libx11 libice libsm
```

**Alpine Linux:**

```bash
sudo apk add icu-libs fontconfig libx11 libice libsm
```

**openSUSE / SLES:**

```bash
sudo zypper install libicu-devel fontconfig libX11-6 libICE6 libSM6
```

### Portable ZIP

```bash
unzip linux-x64.zip -d RetroMultiTools
cd RetroMultiTools
chmod +x RetroMultiTools
./RetroMultiTools
```

### DEB Installer

```bash
sudo dpkg -i linux-x64-Installer.deb
retromultitools
```

### RPM Package

```bash
# Fedora / CentOS Stream / RHEL / Rocky Linux / AlmaLinux / Oracle Linux
sudo dnf install linux-x64.rpm

# openSUSE / SLES
sudo zypper install linux-x64.rpm
```

Then launch from the application menu or run `retromultitools`.

### APK Package (Alpine Linux)

```bash
sudo apk add --allow-untrusted linux-x64.apk
retromultitools
```

### AppImage

```bash
chmod +x linux-x64.AppImage
./linux-x64.AppImage
```

No installation is required — AppImage bundles everything into a single file.

### Pacman (Arch Linux)

```bash
sudo pacman -U linux-x64.pkg.tar.zst
retromultitools
```

### Snap

```bash
sudo snap install --dangerous linux-x64.snap
snap run retromultitools
```

### Flatpak

```bash
flatpak install --user linux-x64.flatpak
flatpak run io.github.svengdk.RetroMultiTools
```

See [LINUX.md](../../LINUX.md) for full details and troubleshooting.

---

## macOS

### Portable ZIP

```bash
unzip osx-arm64.zip -d RetroMultiTools
cd RetroMultiTools
chmod +x RetroMultiTools
./RetroMultiTools
```

### PKG Installer

Double-click the `.pkg` file and follow the on-screen instructions to install to `/Applications`.

### DMG Disk Image

1. Double-click the `.dmg` file to mount the disk image.
2. Drag **Retro Multi Tools.app** to the **Applications** folder.
3. Eject the disk image.

### Gatekeeper Warning

macOS may block the application because it was not downloaded from the App Store. If you see a **"cannot be opened because the developer cannot be verified"** dialog:

1. Open **System Settings** → **Privacy & Security**
2. Scroll down and click **Open Anyway** next to the Retro Multi Tools message
3. Alternatively, right-click the application and select **Open**

Or remove the quarantine attribute from the terminal:

```bash
xattr -rd com.apple.quarantine RetroMultiTools
```

See [macOS.md](../../macOS.md) for full details and troubleshooting.

---

## FreeBSD

FreeBSD runs Retro Multi Tools through its built-in Linux binary compatibility layer (Linuxulator). Building from source directly on FreeBSD is **not** supported.

### Prerequisites

Enable Linux binary compatibility and install the Rocky Linux 9 base packages:

```bash
sudo sysrc linux_enable="YES"
sudo service linux start
sudo pkg install linux_base-rl9 linux-rl9-icu linux-rl9-fontconfig linux-rl9-freetype
```

### Portable ZIP

```bash
unzip freebsd-x64.zip -d RetroMultiTools
cd RetroMultiTools
chmod +x RetroMultiTools
./RetroMultiTools
```

### PKG Installer

```bash
sudo pkg add freebsd-x64-Installer.pkg
retromultitools
```

See [FreeBSD.md](../../FreeBSD.md) for full details, required packages, and troubleshooting.

---

## Next Steps

Once installed, see [First Steps](first-steps.md) to get started with the application.
