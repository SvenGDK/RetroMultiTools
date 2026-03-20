# ROM Browser & RetroArch Integration — User Guide

A step-by-step guide to browsing, managing, and launching your ROM collection with the ROM Browser and RetroArch.

---

## Table of Contents

- [Opening the ROM Browser](#opening-the-rom-browser)
- [Scanning a ROM Folder](#scanning-a-rom-folder)
- [Filtering by System](#filtering-by-system)
- [Searching ROMs](#searching-roms)
- [Viewing Artwork](#viewing-artwork)
- [Big Picture Mode](#big-picture-mode)
  - [Entering Big Picture Mode](#entering-big-picture-mode)
  - [Browsing & Navigation](#browsing--navigation)
  - [Filtering, Searching & Sorting](#filtering-searching--sorting)
  - [Detail Panel & Artwork](#detail-panel--artwork)
  - [Launching a ROM](#launching-a-rom)
  - [Exiting Big Picture Mode](#exiting-big-picture-mode)
  - [Auto-Start Setting](#auto-start-setting)
- [Managing ROMs](#managing-roms)
  - [Adding ROMs](#adding-roms)
  - [Copying ROMs](#copying-roms)
  - [Moving ROMs](#moving-roms)
  - [Deleting ROMs](#deleting-roms)
- [Context Menu](#context-menu)
- [Organizing by System](#organizing-by-system)
- [Sending ROMs to a Remote Target](#sending-roms-to-a-remote-target)
- [Hosting & Sharing ROMs](#hosting--sharing-roms)
- [RetroArch Integration](#retroarch-integration)
  - [Configuring RetroArch](#configuring-retroarch)
  - [Launching a ROM with RetroArch](#launching-a-rom-with-retroarch)
  - [Minimize to Tray on Launch](#minimize-to-tray-on-launch)
  - [Discord Rich Presence](#discord-rich-presence)
  - [System-to-Core Mapping](#system-to-core-mapping)
  - [Downloading Missing Cores](#downloading-missing-cores)
- [Troubleshooting](#troubleshooting)

---

## Opening the ROM Browser

Select **ROM Browser** from the application sidebar. The view shows an empty ROM list with a toolbar at the top and a status bar at the bottom.

---

## Scanning a ROM Folder

1. Click **Select Folder** in the toolbar.
2. Choose the directory that contains your ROM files.
3. The scanner recursively searches all subdirectories and identifies files belonging to any of the 46 supported systems using known ROM file extensions.
4. A progress indicator shows scanning status. When finished, the status bar reports the total number of ROMs found.

The ROM list displays four columns:

| Column | Description |
|---|---|
| **File Name** | ROM file name |
| **System** | Detected console or computer (e.g., NES, SNES, Mega Drive) |
| **Size** | File size in a human-readable format |
| **Valid** | Basic validity check result |

Columns are sortable and resizable. You can select multiple ROMs by holding **Ctrl** or **Shift** while clicking.

---

## Filtering by System

Use the **Filter by System** dropdown in the toolbar to narrow the list to a single system (e.g., only Game Boy Advance ROMs). Select **All Systems** to show every ROM again.

The status bar updates to reflect the filtered ROM count.

---

## Searching ROMs

Use the **Search** text box in the toolbar to perform a detailed text search across your ROM collection. The search filters by:

- **File name** — matches any part of the ROM file name
- **System name** — matches the detected system display name
- **File size** — matches the formatted file size string

The search works in combination with the system filter. For example, you can select "Game Boy Advance" from the system dropdown and then type "Pokemon" in the search box to find all GBA Pokémon ROMs.

The status bar shows how many ROMs match the current filters (e.g., *Showing 5 of 1200 ROM(s).*).

> **Tip:** Clear the search box to return to the full filtered list.

---

## Viewing Artwork

1. Check the **Show Artwork** checkbox in the toolbar.
2. A side panel appears on the right showing three artwork slots: **Box Art**, **Screenshot**, and **Title Screen**.
3. Select a ROM in the list. The application fetches artwork from the [Libretro Thumbnails](https://github.com/libretro-thumbnails) repository.
4. Images are cached locally in `%APPDATA%/RetroMultiTools/ArtworkCache/` (or the equivalent on Linux/macOS) after the first download, so subsequent views load instantly without a network request.

Uncheck **Show Artwork** to hide the panel and free screen space for the ROM list.

> **Tip:** Artwork loading is cancelled automatically when you select a different ROM, so you can browse quickly without waiting.

---

## Big Picture Mode

Big Picture Mode transforms the ROM Browser into a fullscreen, controller-friendly experience inspired by Steam's Big Picture. It displays your ROM collection as large visual cards with color-coded system banners and box art thumbnails, making it ideal for couch gaming with a game controller or keyboard.

### Entering Big Picture Mode

There are two ways to enter Big Picture Mode:

1. **From the ROM Browser** — Click the **Big Picture Mode** button in the toolbar. If you already have a folder scanned, the current ROM list is carried over so you don't have to rescan.
2. **On startup** — Enable **Start in Big Picture Mode automatically** in the **Settings** view and configure a ROM folder. The application will launch directly into Big Picture Mode.

### Browsing & Navigation

The game grid displays ROM cards in a responsive wrap layout. Each card shows:

- A **color-coded banner** based on the system (e.g., red for Nintendo, blue for Sega, peach for Atari) with **box art thumbnails** that appear as artwork is pre-loaded
- The **game name** (from the file name, without extension)
- The **system name** and **file size**

A **ROM count badge** in the status bar always shows the number of visible ROMs (e.g., "42 / 128 ROMs" when a filter is active). A **zoom level badge** shows the current grid scale. When a game controller is connected, a **controller badge** shows its name.

Navigate the grid using these keyboard shortcuts:

| Key | Action |
|---|---|
| **←** **→** | Move selection left / right |
| **↑** **↓** | Move selection up / down one row (clamps to first / last card) |
| **Enter** / **Space** | Launch the selected ROM with RetroArch |
| **F** | Toggle the selected ROM as a favorite |
| **I** | Show / hide ROM Info overlay (header details, checksums, GoodTools codes) |
| **+** / **−** | Zoom in / zoom out the card grid (50%–200%) |
| **H** / **?** | Show / hide keyboard shortcuts help overlay |
| **Escape** / **Backspace** | Exit Big Picture Mode (or defocus the search box) |
| **Tab** | Focus the search box |
| **Home** / **End** | Jump to the first / last card |
| **PageUp** / **PageDown** | Scroll up / down by several rows |

### Game Controller Navigation

Big Picture Mode natively supports game controllers via the SDL2 Game Controller API.  Controllers are automatically detected when plugged in (autoconfig / Plug & Play), using the same controller database as RetroArch.

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

Additional controller mappings can be loaded from a `gamecontrollerdb.txt` file placed next to the executable, or from RetroArch's autoconfig directory.  SDL2 must be installed on the system for controller support (keyboard navigation always works regardless).

### Filtering, Searching & Sorting

The top bar provides several controls for organizing the grid:

- **Search** — Type to filter ROMs by file name or system name. Press Tab to focus the search box; press Escape to return focus to the card grid.
- **System filter** — Select a system from the dropdown to show only ROMs for that console or computer.
- **Sort** — Choose from Name (A–Z), Name (Z–A), System, Size ascending, Size descending, or **Recently Played**.
- **Favorites** — Click the ★ **Favorites** toggle button to show only ROMs you have marked as favorites.

You can also click **Select Folder** to scan a different ROM directory, **Rescan** to refresh the current folder, or **Random Game** to jump to a random ROM (avoids re-selecting the currently selected game).

### Favorites

Mark any ROM as a favorite by selecting it and pressing **F** on the keyboard, or by clicking the favorite button (☆ / ★) in the detail panel. Favorites are persisted across sessions.

Use the **★ Favorites** toggle button in the top bar to filter the grid and show only your favorite ROMs. This works in combination with the system filter and search box.

### Recently Played

When you launch a ROM, it is automatically recorded in your recently played history (up to 50 entries). Select **Recently Played** from the sort dropdown to see your most recently launched games at the top of the grid.

### Detail Panel & Artwork

When you select a card, a detail panel slides in from the right showing:

- **Game title** and **system badge**
- **Favorite button** (☆ / ★) to mark or unmark the ROM
- **File size**, **validity status**, **play count** (number of times the ROM has been launched), and **file path**
- **Box art**, **screenshot**, and **title screen** fetched from the Libretro Thumbnails repository
- A **Launch with RetroArch** button (enabled when the system has a mapped core)
- A **Launch with MAME** button (visible for Arcade ROMs when MAME is configured)

Artwork is cached locally after the first download so subsequent selections load instantly. When entering Big Picture Mode, artwork for all loaded ROMs is pre-loaded in the background so that browsing the collection feels instant. Box art thumbnails also appear directly on the game cards as they are loaded.

### Grid Zoom

Press **+** to zoom in or **−** to zoom out on the card grid. The zoom range is 50% to 200% in 10% steps. The current zoom level is displayed as a badge in the status bar (e.g., "Zoom: 120%"). The zoom preference is saved between sessions.

### Keyboard Shortcuts Help

Press **H** or **?** to show a help overlay listing all available keyboard shortcuts. The overlay blocks other input while visible. Press **H**, **?**, or **Escape** to dismiss it.

### ROM Info Overlay

Press **I** to show a ROM Info overlay for the currently selected ROM. The overlay displays:

- **ROM details** — file name, system, file size, validity status, and any parsed header fields
- **Checksums** — CRC32, MD5, SHA-1, and SHA-256 (computed asynchronously in the background)
- **GoodTools codes** — country codes, standard codes, and GoodGen codes parsed from the file name

Press **I** or **Escape** to dismiss the overlay. When using a game controller, press **B** to toggle it.

### Play Statistics

Each time you launch a ROM with RetroArch or MAME, the application increments a play counter for that ROM. The count is displayed in the detail panel as "Times played: N" and is persisted across sessions.

### Launching a ROM

Select a ROM card and press **Enter**, **Space**, or click the **Launch with RetroArch** button in the detail panel. For Arcade ROMs, a **Launch with MAME** button is also available when standalone MAME is configured. The application:

1. Looks up the recommended libretro core for the ROM's system.
2. Starts RetroArch with the correct core and ROM path.
3. Records the ROM in your **recently played** history and increments the **play count**.
4. If **Minimize to tray when launching a ROM** is enabled, minimizes to the system tray and restores automatically when the emulator exits.

### Exiting Big Picture Mode

- Press **Escape** or **Backspace** (when the search box is not focused).
- Click the **Exit Big Picture** button in the top-right corner.

If the search box is focused, pressing **Escape** returns focus to the card grid instead. Press **Escape** again to exit Big Picture Mode.

The application returns to the standard ROM Browser view at its previous window size.

### Auto-Start Setting

To launch directly into Big Picture Mode every time you open the application:

1. Open the **Settings** view.
2. In the **Big Picture Mode** section, check **Start in Big Picture Mode automatically**.
3. Click **Browse** to select the ROM folder that should be scanned automatically on startup.

---

## Managing ROMs

### Adding ROMs

1. After scanning a folder, click **Add ROMs**.
2. A file picker appears. Select one or more ROM files from anywhere on your system.
3. The selected files are copied into the currently scanned folder and the list is refreshed automatically.

### Copying ROMs

1. Select one or more ROMs in the list.
2. Click **Copy To**.
3. Choose a destination folder. The selected ROMs are copied there.

### Moving ROMs

1. Select one or more ROMs in the list.
2. Click **Move To**.
3. Choose a destination folder. The selected ROMs are moved and the list is refreshed.

### Deleting ROMs

1. Select one or more ROMs in the list.
2. Click **Delete**.
3. The files are removed from disk and the list is refreshed.

The status bar reports the number of files successfully processed and any failures for each operation.

---

## Context Menu

Right-click on a selected ROM in the list to access a context menu with quick actions:

| Action | Description |
|---|---|
| **🎮 Launch with RetroArch** | Launches the selected ROM in RetroArch with the appropriate core |
| **🕹️ Launch with MAME** | Launches the selected Arcade ROM in standalone MAME |
| **📋 Copy To...** | Copies the selected ROM(s) to a destination folder |
| **📁 Move To...** | Moves the selected ROM(s) to a destination folder |
| **📤 Send To Remote...** | Opens the Send to Remote dialog for the selected ROM(s) |
| **🌐 Host & Share...** | Opens the Host & Share dialog to serve the selected ROM(s) over a local HTTP server |
| **🔄 Convert Format** | Available for N64, SNES, and NES ROMs — opens the format converter |
| **✂️ Trim ROM** | Available for systems with trimmable ROMs (NES, SNES, N64, Game Boy, GBA) |
| **📄 Export Header** | Exports the ROM header to a text file for systems with parseable headers |
| **✅ Verify ROM** | Guides you to the DAT Verifier or Dump Verifier for ROM verification |
| **🗑️ Delete** | Deletes the selected ROM(s) from disk |

System-specific actions (Convert Format, Trim ROM, Export Header) are only visible when the selected ROM belongs to a system that supports that operation.

---

## Organizing by System

1. Scan a folder so the ROM list is populated.
2. Click **Organize ROMs**.
3. Choose an output directory.
4. The organizer copies every scanned ROM into system-specific subfolders based on the detected system type. For example:

```
Output/
├── NES/
├── SNES/
├── Sega Genesis/
├── Game Boy Advance/
└── ...
```

A summary reports how many files were copied, skipped (already exist), or failed.

---

## Sending ROMs to a Remote Target

1. Select one or more ROMs in the list.
2. Click **Send to Remote**.
3. A dialog opens where you choose a transfer protocol and enter connection details.

Supported protocols:

| Protocol | Default Port | Notes |
|---|---|---|
| **FTP** | 21 | Optional Explicit FTPS (TLS) |
| **SFTP** | 22 | SSH File Transfer Protocol |
| **WebDAV** | 443 | HTTP-based, auto-creates directories |
| **Amazon S3** | 443 | Also supports S3-compatible services (MinIO, Wasabi, etc.) |
| **Google Drive** | — | Uses OAuth access token; optional folder ID |
| **Dropbox** | — | Uses OAuth access token; uploads to specified path |
| **OneDrive** | — | Uses OAuth access token via Microsoft Graph API |

4. Click **Send** to begin the transfer. Progress is reported per file and can be cancelled.

For cloud storage providers (Google Drive, Dropbox, OneDrive), you will need to provide an OAuth access token obtained from the respective provider's developer console or API. The token is used for authentication and file upload.

See the [Remote Transfer Protocols](../reference/remote-transfer.md) reference for full connection details and limits.

---

## Hosting & Sharing ROMs

The Host & Share feature starts a lightweight HTTP server on your machine, allowing other devices on the same local network to browse and download your ROMs using any web browser.

### Starting the Server

1. Optionally select one or more ROMs in the list (for **Selected ROMs** mode), or leave no selection (for **Directory** mode).
2. Click **Host & Share** in the toolbar, or right-click and choose **🌐 Host & Share...** from the context menu.
3. A dialog opens showing the hosting configuration.
4. Set the port number (default: **8080**). Any available port between 1 and 65535 can be used.
5. Click **Start**.

### Hosting Modes

| Mode | Trigger | What Is Served |
|---|---|---|
| **Directory** | Click **Host & Share** with no ROMs selected | All ROM files in the currently browsed directory |
| **Selected ROMs** | Select ROM(s) first, then click **Host & Share** | Only the selected ROM file(s) |

### Sharing the URL

Once the server starts, the dialog displays one or more URLs — one for each network interface on your machine. Share any of these URLs with other users on your LAN:

```
http://192.168.1.100:8080/
```

Click **Copy URL** to copy the first URL to your clipboard.

Recipients open the URL in any web browser to see a styled directory listing with file names and sizes. Clicking a file name starts a download.

### Connection Log

The dialog shows a real-time log of incoming connections and requests, which is useful for confirming that clients are connecting successfully.

### Features

- **Resumable downloads** — The server supports HTTP range requests, so interrupted downloads can resume where they left off.
- **Cross-platform** — Works identically on Windows, Linux, and macOS with no additional setup or dependencies.
- **Concurrent connections** — Multiple clients can download files simultaneously.
- **Path-traversal protection** — The server only serves files from the specified directory or file list; it does not expose other files on your system.

### Stopping the Server

Click **Stop** to shut down the server immediately. The server also stops automatically when you close the Host & Share dialog.

---

## RetroArch Integration

The ROM Browser can launch any selected ROM directly in [RetroArch](https://www.retroarch.com/) with the correct libretro core for its system.

### Configuring RetroArch

Before launching ROMs you need to tell the application where RetroArch is installed. Open the **Settings** view and use the RetroArch configuration section:

| Action | Description |
|---|---|
| **Browse** | Manually select the RetroArch executable (on macOS you can select the `.app` bundle directly) |
| **Auto-Detect** | Searches the system PATH and common install locations automatically |
| **Download** | Opens the official RetroArch download page in your browser |
| **Clear** | Removes the stored path |

The configured path is saved in `settings.json` and persists across sessions.

**Auto-detection locations by platform:**

- **Windows** — `Program Files\RetroArch`, `Program Files (x86)\RetroArch`, `%LOCALAPPDATA%\RetroArch`, `C:\RetroArch`, `C:\RetroArch-Win64`, Scoop installs
- **Linux** — `/usr/bin/retroarch`, `/usr/local/bin/retroarch`, `/snap/bin/retroarch`, system PATH, Flatpak (`org.libretro.RetroArch`), AppImage files in `~/Applications`
- **macOS** — `/Applications/RetroArch.app`, `~/Applications/RetroArch.app`, Homebrew (`/opt/homebrew/bin/retroarch`, `/usr/local/bin/retroarch`), system PATH. You can also point at a `.app` bundle directly — the executable inside will be resolved automatically.

### Launching a ROM with RetroArch

1. Select a single ROM in the ROM Browser list.
2. Click **Launch RetroArch**.
3. The application:
   - Looks up the recommended libretro core for the ROM's system.
   - Locates the core library file in the RetroArch `cores/` directory (or platform-specific fallback locations).
   - Starts RetroArch with the `-L <core_path> <rom_path>` command line.
4. The status bar shows which core was used (e.g., *Launched with core: snes9x (SNES)*).

If the required core is not installed, the status bar displays a message asking you to install it. See [Downloading Missing Cores](#downloading-missing-cores) below.

### Minimize to Tray on Launch

When **Minimize to tray when launching a ROM** is enabled (the default), the application automatically minimizes to the system tray after launching RetroArch. A tray icon appears allowing you to restore the window at any time.

When the RetroArch process exits (i.e., you close the game), the application:

- Restores the main window automatically.
- Hides the tray icon.
- Clears the Discord Rich Presence status (if enabled).

This keeps the desktop clean while playing. The setting can be toggled in the **Settings** view under **System Tray**.

### Discord Rich Presence

When enabled, launching a ROM via RetroArch automatically updates your Discord status to show the game being played and the system it runs on.

1. Open the **Settings** view.
2. In the **Discord** section, check **Enable Discord Rich Presence**.
3. When you launch a ROM, your Discord status will display:
   - **Game name** — the ROM file name (without extension)
   - **System name** — the full name of the console or computer (e.g., "Super Nintendo", "Sega Genesis / Mega Drive")
   - **Elapsed time** — how long you've been playing

The status is updated each time you launch a new game. Discord must be running for the integration to work.

### System-to-Core Mapping

Each supported system maps to a recommended libretro core:

| System | Core | Core Project |
|---|---|---|
| NES | `fceumm` | FCEUmm |
| SNES | `snes9x` | Snes9x |
| Nintendo 64 | `mupen64plus_next` | Mupen64Plus-Next |
| Nintendo 64DD | `mupen64plus_next` | Mupen64Plus-Next |
| Nintendo DS | `melonds` | melonDS |
| Nintendo 3DS | `citra` | Citra |
| Game Boy | `gambatte` | Gambatte |
| Game Boy Color | `gambatte` | Gambatte |
| Game Boy Advance | `mgba` | mGBA |
| Virtual Boy | `mednafen_vb` | Mednafen VB |
| Sega Master System | `genesis_plus_gx` | Genesis Plus GX |
| Mega Drive / Genesis | `genesis_plus_gx` | Genesis Plus GX |
| Sega CD | `genesis_plus_gx` | Genesis Plus GX |
| Sega 32X | `picodrive` | PicoDrive |
| Game Gear | `genesis_plus_gx` | Genesis Plus GX |
| Atari 2600 | `stella2014` | Stella 2014 |
| Atari 5200 | `atari800` | Atari800 |
| Atari 7800 | `prosystem` | ProSystem |
| Atari 800 / XL / XE | `atari800` | Atari800 |
| Atari Jaguar | `virtualjaguar` | Virtual Jaguar |
| Atari Lynx | `handy` | Handy |
| PC Engine / TurboGrafx-16 | `mednafen_pce_fast` | Mednafen PCE Fast |
| Neo Geo (AES/MVS) | `geolith` | Geolith |
| Neo Geo CD | `neocd` | NeoCD |
| Neo Geo Pocket | `mednafen_ngp` | Mednafen NGP |
| ColecoVision | `bluemsx` | blueMSX |
| Intellivision | `freeintv` | FreeIntv |
| MSX / MSX2 | `bluemsx` | blueMSX |
| NEC PC-88 | `quasi88` | QUASI88 |
| Amstrad CPC | `cap32` | Caprice32 |
| Thomson MO5 | `theodore` | Theodore |
| Watara Supervision | `potator` | Potator |
| Color Computer | `xroar` | XRoar |
| Panasonic 3DO | `opera` | Opera |
| Philips CD-i | `same_cdi` | SAME CDi |
| Amiga CD32 | `puae` | P-UAE |
| Sega Saturn | `mednafen_saturn` | Mednafen Saturn |
| Sega Dreamcast | `flycast` | Flycast |
| GameCube | `dolphin` | Dolphin |
| Wii | `dolphin` | Dolphin |
| Arcade (MAME) | `mame2003_plus` | MAME 2003-Plus |

> **Neo Geo AES/MVS BIOS Note:** The `geolith` core supports both Neo Geo AES (home console) and MVS (arcade) modes. Using the **"AES"** BIOS will start games in their console (home) versions. Using the **"NEOGEO"** (Universe BIOS or standard MVS) BIOS will boot games in their arcade versions.

### Downloading Missing Cores

If a core is not installed you can download it from within the application:

1. Open the **Settings** view.
2. In the **RetroArch Core Downloader** section, click **Check Cores**.
3. A list appears showing each core with its install status:
   - ✔ — installed
   - ✘ — missing
4. Click **Download Missing Cores** to download and install all missing cores from the official RetroArch buildbot (`buildbot.libretro.com`).
5. Progress is shown in real time. Click **Cancel** to stop downloads at any time.

**Core directory by platform:**

| Platform | Directory |
|---|---|
| Windows | `cores/` next to `retroarch.exe` |
| macOS | `cores/` next to the RetroArch binary |
| Linux | `cores/` next to the executable, or `~/.config/retroarch/cores/` if the executable directory is not writable |

After downloading, return to the ROM Browser and click **Launch RetroArch** — the newly installed core will be detected automatically.

---

## MAME Integration

The ROM Browser can launch Arcade ROMs directly in standalone [MAME](https://www.mamedev.org/) as an alternative to using RetroArch with the `mame2003_plus` core.

### Configuring MAME

Open the **Settings** view and use the MAME configuration section:

| Action | Description |
|---|---|
| **Browse** | Manually select the MAME executable (on macOS you can select the `.app` bundle directly) |
| **Auto-Detect** | Searches the system PATH and common install locations automatically |
| **Download** | Opens the official MAME download page in your browser |
| **Clear** | Removes the stored path |

The configured path is saved in `settings.json` and persists across sessions.

**Auto-detection locations by platform:**

- **Windows** — `%ProgramFiles%\MAME`, `C:\MAME`, Scoop installs
- **Linux** — `/usr/bin/mame`, `/usr/local/bin/mame`, `/usr/games/mame`, `/snap/bin/mame`, system PATH
- **macOS** — `/Applications/mame`, `~/Applications/mame`, Homebrew (`/opt/homebrew/bin/mame`, `/usr/local/bin/mame`), system PATH

### Launching an Arcade ROM with MAME

1. Select an Arcade ROM in the ROM Browser list.
2. Click the **🕹️ MAME** toolbar button, or right-click and choose **Launch with MAME**.
3. The application:
   - Passes the ROM directory and ROM set name to MAME via `-rompath <dir> <romname>`.
   - Updates Discord Rich Presence (if enabled).
4. The status bar shows the launch result (e.g., *Launched sf2 with MAME.*).

In **Big Picture Mode**, an additional **Launch with MAME** button appears in the detail panel when an Arcade ROM is selected. Both RetroArch and MAME launch buttons are available simultaneously, letting you choose which emulator to use.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| **"MAME not found"** | Open **Settings** and use **Auto-Detect** or **Browse** to set the MAME path. If MAME is not installed, click **Download**. |
| **"RetroArch not found"** | Open **Settings** and use **Auto-Detect** or **Browse** to set the RetroArch path. If RetroArch is not installed, click **Download**. |
| **"Core not found"** | Use the **Core Downloader** in Settings to install the missing core, or install it from RetroArch's built-in **Online Updater → Core Downloader**. |
| **No artwork appears** | Check your internet connection. Artwork is fetched from the Libretro Thumbnails GitHub repository. After the first download, images are cached locally in the `ArtworkCache` directory under your app data folder. |
| **ROM not detected / shows wrong system** | System detection is based on file extensions. Ensure the ROM file has the correct extension for its system (see [Supported Systems](../reference/supported-systems.md)). |
| **Scan finds no ROMs** | Verify the folder contains files with recognized ROM extensions. The scanner searches subdirectories automatically. |
| **Flatpak RetroArch launch fails** | Flatpak installs are detected and launched with `flatpak run org.libretro.RetroArch`. If this fails, ensure the Flatpak package is installed and up to date. |
| **Send to Remote fails** | Verify the connection details (host, port, credentials). See the [Remote Transfer Protocols](../reference/remote-transfer.md) reference for protocol-specific requirements. |
| **Host & Share server won't start** | The port may already be in use by another application. Try a different port number. On some systems, ports below 1024 require administrator privileges. |
| **Others can't connect to hosted ROMs** | Ensure all devices are on the same local network. Check that your firewall allows incoming connections on the configured port. The dialog shows your LAN IP addresses — verify the client is using one of these. |
| **Window doesn't restore after closing RetroArch** | The application monitors the RetroArch process. If RetroArch was launched via a wrapper (e.g., Flatpak), the monitored process may exit before the game closes. Click the tray icon or use the tray context menu to restore the window manually. |
| **Tray icon not visible** | The tray icon only appears while the window is minimized to the tray. On some Linux desktop environments, a system tray extension may be required (e.g., AppIndicator on GNOME). |
| **Big Picture Mode is empty** | Click **Select Folder** in the Big Picture top bar to scan a ROM directory, or ensure the ROM folder configured in Settings exists and contains recognized ROM files. |
| **Big Picture auto-start shows nothing** | Open **Settings → Big Picture Mode** and verify the ROM folder path is set and points to a directory containing ROM files. |
