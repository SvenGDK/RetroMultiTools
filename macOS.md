<div align="center">

# Running Retro Multi Tools on macOS

Step-by-step guide for installing and running Retro Multi Tools on macOS.

</div>

## Prerequisites

- macOS 10.15 (Catalina) or later
- .NET 8.0 Runtime or SDK

## Installing the .NET 8 Runtime

### Using the Installer

1. Go to <https://dotnet.microsoft.com/download/dotnet/8.0>
2. Download the **macOS** installer for your Mac:
   - **Apple Silicon** (M1 / M2 / M3 / M4) — Arm64
   - **Intel** — x64
3. Open the `.pkg` file and follow the installation steps

### Using Homebrew

```bash
brew install dotnet@8
```

After installing, make sure the `dotnet` command is available:

```bash
dotnet --info
```

> **Note:** If you downloaded a self-contained release you do not need to install the .NET runtime.

## Downloading a Release

Download the release ZIP that matches your Mac from the [Releases](https://github.com/SvenGDK/RetroMultiTools/releases) page.

| File | Description |
|---|---|
| `osx-x64.zip` | Framework-dependent build for Intel Macs |
| `osx-arm64.zip` | Framework-dependent build for Apple Silicon Macs |
| `osx-x64-selfcontained.zip` | Self-contained build for Intel Macs (no runtime required) |
| `osx-arm64-selfcontained.zip` | Self-contained build for Apple Silicon Macs (no runtime required) |

## Running the Application

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

### Application does not start

Verify the .NET 8 runtime is installed:

```bash
dotnet --info
```

If you installed via Homebrew and the command is not found, add it to your `PATH`:

```bash
export PATH="/usr/local/share/dotnet:$PATH"
```

Add this line to your `~/.zshrc` to make it permanent.

### Fonts look incorrect

Install a common font package via Homebrew:

```bash
brew install font-liberation
```
