# Settings & Integration

Application settings, RetroArch integration, native menu, system tray, application updates, and remote file transfer.

---

## Native Menu

On macOS and supported Linux desktop environments, a platform-native application menu is available in the menu bar. The menu mirrors all sidebar navigation categories and items, providing keyboard-driven access to every tool.

### Menu Structure

| Menu | Items |
|---|---|
| **File** | Exit |
| **Browse & Inspect** | ROM Browser, ROM Inspector, Hex Viewer |
| **Patching & Conversion** | ROM Patcher, N64 Converter, Format Converter, Patch Creator, Save Converter, ZIP Extractor, Split Assembler, Decompressor |
| **Analysis & Verification** | Checksum Calculator, ROM Comparer, DAT Verifier, DAT Filter, Dump Verifier, Duplicate Finder, Batch Hasher, Security Analysis, GoodTools Identifier |
| **Headers & Trimming** | Header Export, SNES Header Tool, Header Fixer, ROM Trimmer |
| **Utilities** | Cheat Codes, Emulator Config, Metadata Scraper, ROM Renamer |
| **MAME** | ROM Auditor, CHD Verifier, Set Rebuilder, Dir2Dat, Sample Auditor |
| **Help** | Settings |

Clicking a menu item navigates the main window to the corresponding view and syncs the sidebar selection. If the window is currently minimized to the system tray, it is restored first.

---

## System Tray

The application includes a system tray icon that appears when the window is minimized to the tray. The tray icon provides quick access to restore the window or exit the application.

### Tray Context Menu

| Action | Description |
|---|---|
| **Show** | Restores the main window from the system tray |
| **Exit** | Shuts down the application |

Clicking the tray icon also restores the window.

### Minimize to Tray on ROM Launch

When **Minimize to tray when launching a ROM** is enabled (the default), launching a ROM via RetroArch in the ROM Browser automatically:

1. Minimizes the main window to the system tray.
2. Shows the tray icon.
3. Waits for the RetroArch emulator process to exit.
4. Restores the main window and hides the tray icon.
5. Clears the Discord Rich Presence status.

This keeps the desktop clean while playing a game and restores the application when you're done.

### Settings

| Setting | Description | Default |
|---|---|---|
| **Minimize to tray when launching a ROM** | Automatically minimize to the system tray when a ROM is launched via RetroArch | Enabled |

This setting is available in the **Settings** view under the **System Tray** section.

---

## Big Picture Mode

Big Picture Mode provides a fullscreen, controller-friendly interface for browsing and launching ROMs. It replaces the standard ROM Browser list with a card-based grid designed for couch gaming.

### How It Works

1. Click **Big Picture Mode** in the ROM Browser toolbar (or enable auto-start in Settings).
2. The application hides the sidebar and title bar, switches to fullscreen, and displays the Big Picture view.
3. Artwork for all loaded ROMs is pre-loaded in the background so selections display artwork instantly. Box art thumbnails appear on the game cards as they load.
4. Browse your ROM collection using the card grid, system filter, search box, sort options, and favorites filter.
5. Select a card to view detailed information, artwork, play count, and favorite status in a side panel.
6. Mark ROMs as favorites with the **F** key or the favorite button in the detail panel.
7. Press **+** or **−** to zoom in or out on the card grid (50%–200%). The zoom level is saved between sessions.
8. Press **H** or **?** to show a help overlay listing all keyboard shortcuts.
9. Launch the selected ROM directly with RetroArch from the detail panel. The play count is tracked and displayed.
10. Press **Escape** or click **Exit Big Picture** to return to the standard ROM Browser.

### Keyboard Navigation

| Key | Action |
|---|---|
| **←** **→** | Move selection left / right |
| **↑** **↓** | Move selection up / down one row (clamps to first / last card) |
| **Enter** / **Space** | Launch the selected ROM |
| **F** | Toggle the selected ROM as a favorite |
| **R** | Pick a random game |
| **I** | Show / hide ROM Info overlay |
| **+** / **−** | Zoom in / zoom out the card grid |
| **H** / **?** | Show / hide keyboard shortcuts help overlay |
| **Escape** / **Backspace** | Exit Big Picture Mode (or defocus the search box) |
| **Tab** | Focus the search box |
| **Home** / **End** | Jump to first / last card |
| **PageUp** / **PageDown** | Scroll by several rows |

### Game Controller Support

Big Picture Mode supports native game controller input via the SDL2 Game Controller API.  Controllers are automatically recognised when connected (autoconfig / Plug & Play), using the same controller database that RetroArch uses for maximum compatibility.

#### Controller Button Mapping

| Button | Action |
|---|---|
| **D-Pad / Left Stick** | Navigate between cards |
| **A** | Launch the selected ROM |
| **B** | Toggle ROM Info window |
| **Y** | Toggle favorite |
| **X** | Focus search box |
| **Start** | Show / hide help overlay |
| **Back (Select)** | Pick a random game |
| **Guide** | Exit Big Picture Mode |
| **LB / RB** | Page Up / Page Down |
| **L3 / R3** | Jump to first / last card |
| **Right Stick ↕** | Zoom in / out |

#### Autoconfig (Plug & Play)

Controllers are automatically detected and configured thanks to SDL2's built-in controller database, which covers Xbox, PlayStation, Nintendo Switch, and hundreds of other controllers.  Additional mappings can be loaded by placing a `gamecontrollerdb.txt` file next to the application executable.  If RetroArch is configured, its `autoconfig/gamecontrollerdb.txt` is also loaded.

#### Requirements

SDL2 must be installed on the system:

| Platform | Install |
|---|---|
| **Windows** | Place `SDL2.dll` next to the executable, or install via [libsdl.org](https://libsdl.org) |
| **Linux** | `sudo apt install libsdl2-2.0-0` (or equivalent for your distribution) |
| **macOS** | `brew install sdl2` |

If SDL2 is not available, Big Picture Mode still works with keyboard input only — gamepad features are silently disabled.

### Toolbar Features

| Button | Description |
|---|---|
| **★ Favorites** | Toggle filter to show only favorite ROMs |
| **Random Game** | Jump to a random ROM in the current filtered list |
| **Rescan** | Re-scan the current folder for newly added ROMs |
| **Select Folder** | Open a folder picker to scan a different directory |
| **Exit Big Picture** | Return to the standard ROM Browser view |

### Status Bar

| Element | Description |
|---|---|
| **Controller badge** | Shows the connected game controller name (e.g., "🎮 Xbox Controller"), or "No controller" |
| **Zoom badge** | Shows the current card grid zoom level (e.g., "Zoom: 100%") |
| **ROM count badge** | Displays filtered and total ROM count |
| **Hint bar** | Quick-reference keyboard shortcuts, or controller button hints when a game controller is connected |

### Detail Panel

Selecting a game card opens a side panel displaying:

- Game title, system badge, and favorite toggle button
- File size, ROM validity, and **play count** (how many times the ROM has been launched)
- Full file path
- Box art, screenshot, and title screen artwork (fetched from Libretro Thumbnails)
- **Launch with RetroArch** button

### Settings

| Setting | Description | Default |
|---|---|---|
| **Start in Big Picture Mode automatically** | Launch directly into Big Picture Mode when the application starts | Disabled |
| **ROM folder for Big Picture Mode** | The ROM folder to scan automatically when auto-starting Big Picture Mode | (none) |
| **Game controller input** | Enable or disable native gamepad support (requires SDL2) | Enabled |
| **Controller dead zone** | Analog stick dead zone (0.05–0.95) | 0.25 |

These settings are available in the **Settings** view under the **Big Picture Mode** section.

---

## Application Updates

The application can check for and install updates directly from GitHub Releases.

### How It Works

1. On startup (if enabled), the application checks the [GitHub Releases API](https://api.github.com/repos/SvenGDK/RetroMultiTools/releases/latest) for a newer version.
2. If an update is available and a platform-specific ZIP asset is found (e.g., `win-x64.zip`), the user is prompted to install it.
3. Clicking **Update Now** downloads the ZIP to a temporary directory with a progress bar.
4. After the download completes, the application launches the external **RetroMultiTools.Updater** process and shuts down.
5. The updater waits for the main application to fully exit, extracts the ZIP over the installation directory, and relaunches the application.

If no matching platform asset is found, the application offers to open the release page in a browser instead.

### Settings

| Setting | Description | Default |
|---|---|---|
| **Check for updates on startup** | Automatically check for new versions when the application starts | Enabled |

### Manual Update Check

In the **Settings** view, click **Check for Updates** to manually check. If an update is available, an **Install Update** button appears. Progress is displayed with a cancel option.

### Update Flow

```
Main App: Check for update → Download ZIP → Launch Updater → Shutdown
Updater:  Wait for exit → Extract ZIP → Relaunch main app
```

### Updater Process

The updater (`RetroMultiTools.Updater`) is a separate console application bundled with the main app. It:

- Accepts `--pid`, `--zip`, `--target`, and `--exe` command-line arguments
- Waits up to 60 seconds for the main process to exit
- Extracts the update ZIP with path-traversal protection
- Skips replacing its own running executable (uses a `.new` → `.bak` swap strategy)
- Retries file operations on transient locks (antivirus, etc.)
- Logs to `%TEMP%/RetroMultiTools-Update/updater.log` for debugging
- Relaunches the main application after the update is applied

### Post-Update Cleanup

On the next launch after an update, the main application:

- Swaps any pending `.new` updater file into place
- Deletes leftover `.bak` files
- Removes the update temp directory

### Platform-Specific Notes

| Platform | ZIP Asset Name | Updater Executable |
|---|---|---|
| Windows x64 | `win-x64.zip` | `RetroMultiTools.Updater.exe` |
| Windows ARM64 | `win-arm64.zip` | `RetroMultiTools.Updater.exe` |
| Linux x64 | `linux-x64.zip` | `RetroMultiTools.Updater` |
| Linux ARM64 | `linux-arm64.zip` | `RetroMultiTools.Updater` |
| macOS x64 | `osx-x64.zip` | `RetroMultiTools.Updater` |
| macOS ARM64 | `osx-arm64.zip` | `RetroMultiTools.Updater` |

On Linux and macOS, the updater automatically sets the executable permission (`chmod +x`) on both itself and the main application binary.

---

## Language Settings

The application supports 20 languages. Change the language from the **Settings** view using the language dropdown. The change takes effect immediately.

### Supported Languages

English, Spanish, French, German, Portuguese, Italian, Japanese, Chinese (Simplified), Korean, Russian, Dutch, Polish, Turkish, Arabic, Hindi, Thai, Swedish, Czech, Vietnamese, Indonesian

---

## RetroArch Configuration

The Settings view provides tools for configuring and managing RetroArch integration.

### Path Configuration

| Action | Description |
|---|---|
| **Browse** | Manually select the RetroArch executable (on macOS you can select the `.app` bundle directly) |
| **Auto-Detect** | Automatically find RetroArch on the system PATH or common install locations |
| **Download** | Opens the official RetroArch download page in a browser |
| **Clear** | Removes the stored RetroArch path |

The configured path is stored in `settings.json` inside the application data directory and persists across sessions.

### RetroArch Integration

Once configured, RetroArch is used by:

- **ROM Browser** — launch ROMs directly in RetroArch with the appropriate libretro core.
- **Core Downloader** — install missing cores into the RetroArch cores directory.

---

## RetroArch Core Downloader

Downloads and installs libretro cores from the official RetroArch buildbot.

### How It Works

1. Click **Check Cores** to scan the RetroArch `cores/` directory and compare installed cores against the list of cores used by the application.
2. A list shows each core with its install status (✔ installed / ✘ missing).
3. Click **Download Missing Cores** to download and install all missing cores.
4. A **Cancel** button is available during downloads.

### Platform Support

Cores are downloaded from `buildbot.libretro.com` for the current platform:

| Platform | Architecture |
|---|---|
| Windows | x86_64 |
| Linux | x86_64 |
| macOS | x86_64 or arm64 (auto-detected) |

### Core Directory

- **Windows / macOS** — the `cores/` directory next to the RetroArch executable.
- **Linux** — if the directory next to the executable is not writable (e.g., system package install), the downloader uses `~/.config/retroarch/cores/`.

---

## Controller Profiles

Downloads the community-maintained SDL game controller database (`gamecontrollerdb.txt`) for automatic controller recognition in Big Picture Mode and RetroArch.

### How It Works

1. Click **Download / Update Profiles** in the Controller Profiles section of the Settings view.
2. The latest `gamecontrollerdb.txt` is downloaded from the [SDL_GameControllerDB](https://github.com/gabomdq/SDL_GameControllerDB) GitHub repository.
3. The file is saved next to the application executable so SDL2 loads it on startup.
4. If RetroArch is configured, a copy is also placed in the RetroArch `autoconfig/` directory for RetroArch-compatible autoconfig.

### What It Provides

The database contains mapping profiles for hundreds of game controllers (Xbox, PlayStation, Nintendo Switch Pro, 8BitDo, Logitech, and many more). Installing it ensures:

- **Big Picture Mode** — automatic recognition of your controller via SDL2, no manual mapping needed.
- **RetroArch** — the same profiles are compatible with RetroArch's autoconfig system.

### Status Display

The info text shows:

- Number of controller profiles currently installed
- Last updated date
- Or a note that no profiles are installed yet

---

## SDL2 Gamepad Mapping Tool

Create custom SDL2 controller mappings for unrecognized or unmapped game controllers. Accessible from the **Settings** view under **Controller Profiles → Open Gamepad Tool**.

### How It Works

1. Connect a game controller.
2. Open the Gamepad Mapping Tool from the Settings view.
3. Select a controller from the dropdown (controllers are shown with their mapped/unmapped status).
4. Click **Start Mapping** to begin the mapping wizard.
5. For each button and axis (21 elements total), press the corresponding physical input on the controller.
6. Use **Skip** to skip an element if the controller does not have that input.
7. After completing all steps, the generated SDL2 mapping string is displayed.
8. Click **Save** to store the mapping permanently, or **Copy** to copy it to the clipboard.

### Mapping Elements

The wizard maps the following 21 standard SDL2 game controller elements in order:

| Element | Description |
|---|---|
| `a`, `b`, `x`, `y` | Face buttons |
| `back`, `guide`, `start` | Menu buttons |
| `leftstick`, `rightstick` | Stick click (L3/R3) |
| `leftshoulder`, `rightshoulder` | Shoulder buttons (LB/RB) |
| `dpup`, `dpdown`, `dpleft`, `dpright` | D-Pad directions |
| `leftx`, `lefty`, `rightx`, `righty` | Analog stick axes |
| `lefttrigger`, `righttrigger` | Analog triggers |

### Custom Mapping Storage

Custom mappings are stored in `{AppData}/RetroMultiTools/custom_mappings.txt`. Each mapping is a single line in standard SDL2 mapping string format. Mappings are applied to SDL2 at startup via `SDL_GameControllerAddMapping` so they take effect in Big Picture Mode immediately.

### Controller Info Display

When a controller is selected, the tool shows:

- **GUID** — the unique identifier for the controller hardware.
- **Name** — the reported controller name.
- **Mapping status** — whether an existing mapping is found (✔) or not (✘).

### Custom Mappings List

The bottom section shows all saved custom mappings. Each mapping can be deleted individually using the **Delete** button.

### Requirements

SDL2 must be installed on the system. If SDL2 is not available, the tool displays a message and mapping is unavailable.

---

## Remote Transfer (Send to Remote)

Transfer ROM files to remote targets directly from the ROM Browser.

### Supported Protocols

| Protocol | Default Port | Description |
|---|---|---|
| **FTP** | 21 | Standard FTP with optional Explicit FTPS (TLS encryption) |
| **SFTP** | 22 | SSH File Transfer Protocol |
| **WebDAV** | 443 | HTTP-based file transfer with automatic directory creation |
| **S3** | 443 | Amazon S3 or S3-compatible services (MinIO, Wasabi, etc.) |
| **Google Drive** | — | Google Drive API v3 upload via OAuth token |
| **Dropbox** | — | Dropbox API v2 upload via OAuth token |
| **OneDrive** | — | Microsoft Graph API upload via OAuth token |

### Connection Parameters

**FTP / SFTP / WebDAV:**

- Host
- Port
- Username
- Password
- Remote path (default: `/`)
- Use FTPS (FTP only)

**S3-Compatible:**

- Bucket name
- Access key
- Secret key
- Region (default: `us-east-1`)
- Service URL (optional, for non-AWS providers)

### Limits

- Maximum file size: **2 GB**
- Default connection timeout: **30 seconds**

### Usage

1. In the ROM Browser, select one or more ROMs.
2. Click **Send to Remote**.
3. Choose the protocol and enter connection details.
4. Click **Send** to begin the transfer.
5. Progress is reported per file. Transfers can be cancelled.

See [Remote Transfer Protocols](../reference/remote-transfer.md) for a detailed protocol reference.

---

## Host & Share

Share ROM files with other devices on the local network via a built-in HTTP server.

### Hosting Modes

| Mode | Description |
|---|---|
| **Directory** | Serves all ROM files in the currently browsed directory (when no ROMs are selected) |
| **Selected ROMs** | Serves only the selected ROM(s) from the ROM Browser list |

### How It Works

1. In the ROM Browser, optionally select ROMs to share (or leave none selected for directory mode).
2. Click **Host & Share** in the toolbar or context menu.
3. Set the port (default: **8080**) and click **Start**.
4. Share the displayed URL with others on your network.
5. Recipients open the URL in any web browser to browse and download the shared files.

### Features

- Resumable downloads via HTTP range requests
- Concurrent connections from multiple clients
- Path-traversal protection (only specified files are accessible)
- Real-time connection log
- Cross-platform (Windows, Linux, macOS) with no extra dependencies
