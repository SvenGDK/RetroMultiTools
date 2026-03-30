<div align="center">

# Running Retro Multi Tools on FreeBSD

Step-by-step guide for installing and running Retro Multi Tools on FreeBSD.

</div>

## Prerequisites

- FreeBSD 13.0 or later (x64 or ARM64)
- Root or `sudo` access

> **Note:** Retro Multi Tools is compiled as a Linux binary. FreeBSD runs it through the built-in Linux binary compatibility layer. Building from source directly on FreeBSD is **not** supported.

## Step 1 — Enable Linux Binary Compatibility

FreeBSD can run Linux binaries natively through its Linuxulator subsystem. Enable it and start the service:

```bash
sudo sysrc linux_enable="YES"
sudo service linux start
```

Verify that the Linux compatibility layer is loaded:

```bash
kldstat | grep linux
```

You should see `linux64.ko` (or `linux.ko`) in the output.

## Step 2 — Install Rocky Linux 9 Base and Required Packages

Retro Multi Tools requires a Rocky Linux 9 userland and several Linux libraries. Install them from the FreeBSD package repository:

> **Tip:** The version suffixes below (e.g. `67.1_2`) reflect the versions available at the time of writing. If `pkg install` cannot find an exact version, drop the version suffix (e.g. use `linux-rl9-icu` instead of `linux-rl9-icu-67.1_2`) or run `pkg search linux-rl9-icu` to find the current version.

```bash
sudo pkg install \
  linux_base-rl9 \
  linux-rl9-icu-67.1_2 \
  linux-rl9-fontconfig-2.14.0_2 \
  linux-rl9-freetype-2.10.4_3 \
  linux-rl9-wget-1.21.1_1 \
  linux-rl9-ffmpeg-libs-5.1.6_3 \
  linux-rl9-dbus-libs-1.12.20_3 \
  linux-rl9-at-spi2-atk-2.38.0_1 \
  linux-rl9-atk-2.36.0_1 \
  linux-rl9-cups-libs-2.3.3_8 \
  linux-rl9-libxkbcommon-1.0.3_2 \
  linux-rl9-alsa-lib-1.2.13
```

After installing, restart the Linux compatibility service to pick up the new userland:

```bash
sudo service linux restart
```

## Downloading a Release

Download the release that matches your architecture from the [Releases](https://github.com/SvenGDK/RetroMultiTools/releases) page.

### Portable ZIPs

| File | Description |
|---|---|
| `freebsd-x64.zip` | FreeBSD 64-bit (Intel/AMD) |
| `freebsd-arm64.zip` | FreeBSD ARM64 |

### PKG Installers

| File | Description |
|---|---|
| `freebsd-x64-Installer.pkg` | FreeBSD 64-bit (Intel/AMD) |
| `freebsd-arm64-Installer.pkg` | FreeBSD ARM64 |

## Installing from a Portable ZIP

1. Extract the downloaded ZIP:

```bash
unzip freebsd-x64.zip -d RetroMultiTools
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

## Installing from a PKG

1. Install the package using `pkg`:

```bash
sudo pkg add freebsd-x64-Installer.pkg
```

2. Launch the application from the terminal:

```bash
retromultitools
```

Or find **Retro Multi Tools** in your desktop environment's application menu.

## Troubleshooting

### Application does not start

Make sure the Linux compatibility layer is running and all Rocky Linux 9 packages are installed:

```bash
# Check that the Linux module is loaded
kldstat | grep linux

# Restart the service
sudo service linux restart
```

### Missing shared libraries

If the application reports missing `.so` files, check which Linux libraries are available:

```bash
pkg info -l linux_base-rl9
```

Install any additional `linux-rl9-*` packages as needed from the FreeBSD repository.

### Display / rendering issues

Retro Multi Tools on FreeBSD uses X11 for rendering. Make sure you have a working X11 environment:

```bash
# Install Xorg if not already present
sudo pkg install xorg
```

### Fonts look incorrect

Install liberation fonts for proper text rendering:

```bash
sudo pkg install liberation-fonts-ttf
```
