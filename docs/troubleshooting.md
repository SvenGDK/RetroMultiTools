# Troubleshooting

Common issues and solutions for Retro Multi Tools.

---

## Installation & Startup

### Application does not start

On **Linux**, make sure all required system packages are installed:

```bash
# Ubuntu / Debian / Linux Mint
sudo apt install libicu-dev libfontconfig1 libx11-6 libice6 libsm6

# Fedora / CentOS Stream / RHEL / Rocky Linux / AlmaLinux / Oracle Linux
sudo dnf install libicu fontconfig libX11 libICE libSM

# Arch Linux
sudo pacman -S icu fontconfig libx11 libice libsm

# Alpine Linux
sudo apk add icu-libs fontconfig libx11 libice libsm

# openSUSE / SLES
sudo zypper install libicu-devel fontconfig libX11-6 libICE6 libSM6
```

### Missing libSkiaSharp (Linux)

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

# macOS
brew install font-liberation
```

### macOS Gatekeeper blocks the application

If macOS shows **"cannot be opened because the developer cannot be verified"**:

1. Open **System Settings** → **Privacy & Security**
2. Click **Open Anyway** next to the Retro Multi Tools message
3. Or remove the quarantine attribute:

```bash
xattr -rd com.apple.quarantine RetroMultiTools
```

---

## ROM Browser

### No ROMs found after scanning

- Verify that the folder contains ROM files with recognized extensions (see [Supported Systems](reference/supported-systems.md)).
- Check that files are not nested inside archives — use the **Archive Manager** to extract them first.
- Ensure the folder is readable and not on a disconnected drive.

### Artwork does not load

- Artwork is fetched from the [Libretro Thumbnails](https://github.com/libretro-thumbnails) repository. An internet connection is required for the first download.
- After the first download, artwork is cached locally and loads offline.
- If the ROM filename does not match the Libretro naming convention, artwork may not be found.

---

## RetroArch Integration

### RetroArch not detected

- Click **Auto-Detect** in **Settings** → **RetroArch** to search common installation locations.
- If auto-detect fails, use **Browse** to manually select the RetroArch executable.
- On Linux, Flatpak and Snap installations are also checked.

### ROM does not launch

- Ensure the correct libretro core is installed for the system. Use the **Core Downloader** to check and install missing cores.
- Verify that the ROM file is not corrupt — use the **Checksum Calculator** or **DAT Verifier**.

### Cores directory not writable (Linux)

If cores cannot be downloaded to the system directory, the downloader falls back to `~/.config/retroarch/cores/`. For Flatpak: `~/.var/app/org.libretro.RetroArch/config/retroarch/cores/`. For Snap: `~/snap/retroarch/current/.config/retroarch/cores/`.

---

## Big Picture Mode

### Game controller not detected

1. Make sure SDL2 is installed (see [Big Picture Mode Guide](guides/big-picture-mode.md#sdl2-requirement)).
2. Download controller profiles: **Settings** → **Controller Profiles** → **Download / Update Profiles**.
3. If your controller is still not recognized, use the **Gamepad Mapping Tool** to create a custom mapping.

### Controller works but buttons are wrong

Use the **Gamepad Mapping Tool** in Settings to create a custom mapping for your controller.

---

## MAME

### ROM sets show as incomplete

- Ensure you are using a MAME XML database that matches your ROM set version. Export with `mame -listxml > mame.xml`.
- Clone ROMs may require the parent ROM set to be present (in Split mode).

---

## Remote Transfer

### Transfer fails or times out

- Verify the connection details (host, port, credentials).
- Check that the remote server is reachable from your network.
- Files larger than **2 GB** are not supported.
- The default connection timeout is **30 seconds**.

### Cloud storage upload fails

- Ensure your OAuth access token is valid and not expired.
- For Google Drive, verify the folder ID (if specified) exists and is accessible.

---

## Updates

### Update fails to install

- Check the updater log at `%TEMP%/RetroMultiTools-Update/updater.log` (Windows) for details.
- Ensure the application directory is writable.
- Antivirus software may temporarily lock files — the updater retries automatically.
- If the update continues to fail, download the latest release manually from the [Releases](https://github.com/SvenGDK/RetroMultiTools/releases) page.
