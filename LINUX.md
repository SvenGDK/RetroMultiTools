<div align="center">

# Running Retro Multi Tools on Linux

Step-by-step guide for installing and running Retro Multi Tools on Linux.

</div>

## Prerequisites

- A 64-bit Linux distribution (x64 or ARM64)
- .NET 8.0 Runtime or SDK

## Installing the .NET 8 Runtime

### Ubuntu / Debian

```bash
sudo apt update
sudo apt install dotnet-runtime-8.0
```

### Fedora

```bash
sudo dnf install dotnet-runtime-8.0
```

### Arch Linux

```bash
sudo pacman -S dotnet-runtime-8.0
```

### openSUSE

```bash
sudo zypper install dotnet-runtime-8.0
```

### Manual Installation

If your distribution does not offer a .NET 8 package, download the runtime directly from Microsoft:

1. Go to <https://dotnet.microsoft.com/download/dotnet/8.0>
2. Download the **Linux** binary for your architecture (x64 or ARM64)
3. Extract the archive and add the directory to your `PATH`:

```bash
mkdir -p $HOME/.dotnet
tar -xzf dotnet-runtime-8.0.*-linux-x64.tar.gz -C $HOME/.dotnet
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$HOME/.dotnet
```

Add the `export` lines to your `~/.bashrc` or `~/.zshrc` to make them permanent.

> **Note:** If you downloaded a self-contained release you do not need to install the .NET runtime.

## Required System Packages

Retro Multi Tools uses [Avalonia UI](https://avaloniaui.net/) which requires a few system libraries for rendering.

### Ubuntu / Debian

```bash
sudo apt install libicu-dev libfontconfig1 libx11-6 libice6 libsm6
```

### Fedora

```bash
sudo dnf install libicu fontconfig libX11 libICE libSM
```

### Arch Linux

```bash
sudo pacman -S icu fontconfig libx11 libice libsm
```

### openSUSE

```bash
sudo zypper install libicu-devel fontconfig libX11-6 libICE6 libSM6
```

## Downloading a Release

Download the release ZIP that matches your architecture from the [Releases](https://github.com/SvenGDK/RetroMultiTools/releases) page.

| File | Description |
|---|---|
| `linux-x64.zip` | Framework-dependent build for 64-bit Intel/AMD |
| `linux-arm64.zip` | Framework-dependent build for 64-bit ARM |
| `linux-x64-selfcontained.zip` | Self-contained build for 64-bit Intel/AMD (no runtime required) |
| `linux-arm64-selfcontained.zip` | Self-contained build for 64-bit ARM (no runtime required) |

## Running the Application

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

Make sure all required system packages are installed (see [Required System Packages](#required-system-packages)) and that the .NET 8 runtime is on your `PATH`:

```bash
dotnet --info
```

### Missing libSkiaSharp

If you see an error about `libSkiaSharp`, install the OpenGL library for your distribution:

```bash
# Ubuntu / Debian
sudo apt install libgl1-mesa-glx

# Fedora
sudo dnf install mesa-libGL

# Arch Linux
sudo pacman -S mesa
```

### Fonts look incorrect

Install a common font package:

```bash
# Ubuntu / Debian
sudo apt install fonts-liberation

# Fedora
sudo dnf install liberation-fonts

# Arch Linux
sudo pacman -S ttf-liberation
```
