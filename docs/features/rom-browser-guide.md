# ROM Browser & RetroArch Integration — User Guide

A step-by-step guide to browsing, managing, and launching your ROM collection with the ROM Browser and RetroArch.

---

## Table of Contents

- [Opening the ROM Browser](#opening-the-rom-browser)
- [Scanning a ROM Folder](#scanning-a-rom-folder)
- [Filtering by System](#filtering-by-system)
- [Viewing Artwork](#viewing-artwork)
- [Managing ROMs](#managing-roms)
  - [Adding ROMs](#adding-roms)
  - [Copying ROMs](#copying-roms)
  - [Moving ROMs](#moving-roms)
  - [Deleting ROMs](#deleting-roms)
- [Organizing by System](#organizing-by-system)
- [Sending ROMs to a Remote Target](#sending-roms-to-a-remote-target)
- [RetroArch Integration](#retroarch-integration)
  - [Configuring RetroArch](#configuring-retroarch)
  - [Launching a ROM with RetroArch](#launching-a-rom-with-retroarch)
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
3. The scanner recursively searches all subdirectories and identifies files belonging to any of the 32 supported systems using known ROM file extensions.
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

## Viewing Artwork

1. Check the **Show Artwork** checkbox in the toolbar.
2. A side panel appears on the right showing three artwork slots: **Box Art**, **Screenshot**, and **Title Screen**.
3. Select a ROM in the list. The application fetches artwork from the [Libretro Thumbnails](https://github.com/libretro-thumbnails) repository.
4. Images are cached locally after the first download so subsequent views load instantly.

Uncheck **Show Artwork** to hide the panel and free screen space for the ROM list.

> **Tip:** Artwork loading is cancelled automatically when you select a different ROM, so you can browse quickly without waiting.

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

4. Click **Send** to begin the transfer. Progress is reported per file and can be cancelled.

See the [Remote Transfer Protocols](../reference/remote-transfer.md) reference for full connection details and limits.

---

## RetroArch Integration

The ROM Browser can launch any selected ROM directly in [RetroArch](https://www.retroarch.com/) with the correct libretro core for its system.

### Configuring RetroArch

Before launching ROMs you need to tell the application where RetroArch is installed. Open the **Settings** view and use the RetroArch configuration section:

| Action | Description |
|---|---|
| **Browse** | Manually select the RetroArch executable |
| **Auto-Detect** | Searches the system PATH and common install locations automatically |
| **Download** | Opens the official RetroArch download page in your browser |
| **Clear** | Removes the stored path |

The configured path is saved in `settings.json` and persists across sessions.

**Auto-detection locations by platform:**

- **Windows** — `Program Files\RetroArch`, `Program Files (x86)\RetroArch`, `%LOCALAPPDATA%\RetroArch`, `C:\RetroArch`, `C:\RetroArch-Win64`, Scoop installs
- **Linux** — `/usr/bin/retroarch`, `/usr/local/bin/retroarch`, `/snap/bin/retroarch`, Flatpak (`org.libretro.RetroArch`), AppImage files in `~/Applications`
- **macOS** — `/Applications/RetroArch.app`, `~/Applications/RetroArch.app`, Homebrew (`/opt/homebrew/bin/retroarch`, `/usr/local/bin/retroarch`)

### Launching a ROM with RetroArch

1. Select a single ROM in the ROM Browser list.
2. Click **Launch RetroArch**.
3. The application:
   - Looks up the recommended libretro core for the ROM's system.
   - Locates the core library file in the RetroArch `cores/` directory (or platform-specific fallback locations).
   - Starts RetroArch with the `-L <core_path> <rom_path>` command line.
4. The status bar shows which core was used (e.g., *Launched with core: snes9x (SNES)*).

If the required core is not installed, the status bar displays a message asking you to install it. See [Downloading Missing Cores](#downloading-missing-cores) below.

### System-to-Core Mapping

Each supported system maps to a recommended libretro core:

| System | Core | Core Project |
|---|---|---|
| NES | `fceumm` | FCEUmm |
| SNES | `snes9x` | Snes9x |
| Nintendo 64 | `mupen64plus_next` | Mupen64Plus-Next |
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
| Atari Jaguar | `virtualjaguar` | Virtual Jaguar |
| Atari Lynx | `handy` | Handy |
| PC Engine / TurboGrafx-16 | `mednafen_pce_fast` | Mednafen PCE Fast |
| Neo Geo Pocket | `mednafen_ngp` | Mednafen NGP |
| ColecoVision | `bluemsx` | blueMSX |
| Intellivision | `freeintv` | FreeIntv |
| MSX / MSX2 | `bluemsx` | blueMSX |
| Amstrad CPC | `cap32` | Caprice32 |
| Thomson MO5 | `theodore` | Theodore |
| Watara Supervision | `potator` | Potator |
| Color Computer | `xroar` | XRoar |
| Panasonic 3DO | `opera` | Opera |
| Amiga CD32 | `puae` | P-UAE |
| Sega Saturn | `mednafen_saturn` | Mednafen Saturn |
| Sega Dreamcast | `flycast` | Flycast |
| GameCube | `dolphin` | Dolphin |
| Wii | `dolphin` | Dolphin |
| Arcade (MAME) | `mame2003_plus` | MAME 2003-Plus |

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

## Troubleshooting

| Problem | Solution |
|---|---|
| **"RetroArch not found"** | Open **Settings** and use **Auto-Detect** or **Browse** to set the RetroArch path. If RetroArch is not installed, click **Download**. |
| **"Core not found"** | Use the **Core Downloader** in Settings to install the missing core, or install it from RetroArch's built-in **Online Updater → Core Downloader**. |
| **No artwork appears** | Check your internet connection. Artwork is fetched from the Libretro Thumbnails GitHub repository. After the first download, images are cached locally. |
| **ROM not detected / shows wrong system** | System detection is based on file extensions. Ensure the ROM file has the correct extension for its system (see [Supported Systems](../reference/supported-systems.md)). |
| **Scan finds no ROMs** | Verify the folder contains files with recognized ROM extensions. The scanner searches subdirectories automatically. |
| **Flatpak RetroArch launch fails** | Flatpak installs are detected and launched with `flatpak run org.libretro.RetroArch`. If this fails, ensure the Flatpak package is installed and up to date. |
| **Send to Remote fails** | Verify the connection details (host, port, credentials). See the [Remote Transfer Protocols](../reference/remote-transfer.md) reference for protocol-specific requirements. |
