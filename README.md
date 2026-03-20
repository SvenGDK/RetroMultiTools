<div align="center">

<img src="Icon.png" width="128px">

# Retro Multi Tools

<img src="/Screenshots/Screenshot.png" width="75%">
<img src="/Screenshots/BigPictureMode.png" width="75%">

A cross-platform desktop utility for managing, inspecting, and patching retro game ROMs.

[![License: BSD-2-Clause](https://img.shields.io/badge/License-BSD_2--Clause-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6.svg)](#)

</div>

## Table of Contents

- [Downloads](#downloads)
- [Platform Guides](#platform-guides)
- [Documentation](#documentation)
- [Features](#features)
- [Supported Systems](#supported-systems)
- [Localization](#localization)
- [Building from Source](#building-from-source)
- [License](#license)

## Downloads

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

Self-contained portable ZIPs (e.g. `win-x64-Selfcontained.zip`) are also available for each platform and architecture.

### Installers

| File | Description |
|---|---|
| `win-x64-Installer.exe` | Windows 64-bit (Intel/AMD) |
| `win-arm64-Installer.exe` | Windows ARM64 |
| `linux-x64-Installer.deb` | Linux 64-bit (Intel/AMD) |
| `linux-arm64-Installer.deb` | Linux ARM64 |
| `osx-x64-Installer.pkg` | macOS Intel |
| `osx-arm64-Installer.pkg` | macOS Apple Silicon |

Self-contained installers (e.g. `win-x64-Selfcontained-Installer.exe`) are also available for each platform and architecture.

Self-contained builds include the .NET runtime and do not require a separate installation.
Framework-dependent builds require the [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

## Platform Guides

- **Windows** — Install the [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0), extract the ZIP, and run `RetroMultiTools.exe`.
- **Linux** — See [LINUX.md](LINUX.md) for required packages and step-by-step instructions.
- **macOS** — See [macOS.md](macOS.md) for installation steps and Gatekeeper notes.

## Documentation

For detailed user guides and reference material, see the [full documentation](docs/README.md).

### Quick Links

- **[Installation Guide](docs/getting-started/installation.md)** — download and install on Windows, Linux, or macOS
- **[First Steps](docs/getting-started/first-steps.md)** — get up and running in minutes
- **[ROM Browser Guide](docs/guides/rom-browser-guide.md)** — step-by-step guide to browsing, managing, and launching ROMs
- **[Big Picture Mode](docs/guides/big-picture-mode.md)** — fullscreen, controller-friendly ROM library
- **[Keyboard Shortcuts](docs/configuration/keyboard-shortcuts.md)** — all shortcuts in one place
- **[Troubleshooting](docs/troubleshooting.md)** — common issues and solutions

## Features

### Browsing & Inspection

<details>
<summary><strong>ROM Browser</strong></summary>

- Scan directories recursively for ROM files across 46 console and computer types
- Filter ROMs by system
- Organize ROM collections into system-specific folders
- View box art, screenshots, and title screens for selected ROMs (artwork is cached locally after the first download)
- Send selected ROMs to remote targets via FTP, SFTP, WebDAV, Amazon S3, Google Drive, Dropbox, or OneDrive
- Host and share ROMs on the local network via a built-in HTTP server
- **Big Picture Mode** — fullscreen, controller-friendly ROM library browser with card-based grid, system filtering, search, sort, favorites, recently played tracking, artwork display, ROM Info overlay, grid zoom, keyboard shortcuts help overlay, play statistics, and RetroArch launching

</details>

<details>
<summary><strong>ROM Inspector</strong></summary>

- Detect and display ROM system type from headers and file extensions
- Parse detailed header information (title, mapper, ROM/RAM size, checksums, etc.)
- Fetch box art, screenshots, and title screens from the [Libretro Thumbnails](https://github.com/libretro-thumbnails) database

</details>

<details>
<summary><strong>Hex Viewer</strong></summary>

- View ROM file contents in hexadecimal format with ASCII sidebar
- Page-based navigation through files of any size
- Go to specific offset by hex address
- Byte pattern search across the entire file

</details>

### Patching

<details>
<summary><strong>ROM Patcher</strong></summary>

- Apply **IPS** patches (with RLE support and optional truncation)
- Apply **BPS** patches (with full CRC32 validation of source, target, and patch data)

</details>

<details>
<summary><strong>IPS Patch Creator</strong></summary>

- Create IPS patches by comparing an original ROM file with a modified version
- Automatic analysis shows file sizes, differing byte count, and format compatibility
- RLE compression for repeated byte sequences
- Supports files up to 16 MB (IPS format limit)

</details>

### Conversion & Extraction

<details>
<summary><strong>N64 Format Converter</strong></summary>

- Convert between N64 ROM byte orders: `.z64` (Big Endian), `.n64` (Little Endian), `.v64` (Byte-swapped)
- Auto-detects source format from ROM header magic bytes

</details>

<details>
<summary><strong>ROM Format Converter</strong></summary>

- Add or remove 512-byte copier headers from ROM dumps
- Remove iNES headers or fix dirty iNES header bytes
- Convert disc images to CHD (Compressed Hunks of Data) format via chdman
- Convert GameCube/Wii ISOs to RVZ (Dolphin compressed) format via DolphinTool
- Single file or batch conversion for entire directories

</details>

<details>
<summary><strong>Save File Converter</strong></summary>

- Convert save files between formats (.sav, .srm, .eep, .fla, .sra)
- Swap endianness (16-bit or 32-bit) for cross-platform save compatibility
- Pad save files to the next power-of-two size for flash cart compatibility
- Trim trailing padding bytes (0x00 / 0xFF) from oversized save files
- Detects save type (EEPROM, SRAM, Flash) from file size

</details>

<details>
<summary><strong>ZIP ROM Extractor</strong></summary>

- Extract ROM files from ZIP archives
- Lists archive contents with compressed and uncompressed sizes
- Batch extraction from a directory of ZIP files

</details>

<details>
<summary><strong>Split ROM Assembler</strong></summary>

- Reassemble split ROM files (.001/.002, .part1/.part2, .z01/.z02) into a single file
- Auto-detects all parts from the first part file
- Shows detected parts with individual sizes before assembly

</details>

<details>
<summary><strong>ROM Decompressor</strong></summary>

- Decompress GZip-compressed ROM files (.gz)
- Single file or batch decompression from a directory
- Reports compressed and decompressed sizes

</details>

### Verification & Analysis

<details>
<summary><strong>Checksum Calculator</strong></summary>

- Compute CRC32, MD5, SHA-1, and SHA-256 checksums for any ROM file
- Streaming I/O handles large ISO/BIN images without loading into memory
- Selectable hash values for easy copy to clipboard

</details>

<details>
<summary><strong>ROM Comparer</strong></summary>

- Streaming byte-by-byte binary comparison of two files
- Reports identical/different status, differing byte count, and first mismatch offset

</details>

<details>
<summary><strong>DAT Verifier</strong></summary>

- Verify ROM files against No-Intro and TOSEC DAT files (CLRMAMEPro / Logiqx XML format)
- Match ROMs by CRC32, MD5, or SHA-1 checksums against known-good dumps
- Single ROM or batch directory verification

</details>

<details>
<summary><strong>DAT Filter</strong></summary>

- Filter DAT file entries using Retool-like logic
- Category exclusion: demos, betas, prototypes, samples, unlicensed, BIOS, applications, pirate editions
- Region and language priority filtering
- 1G1R (One Game, One ROM) deduplication for No-Intro / Redump naming conventions
- Export filtered results as Logiqx XML format

</details>

<details>
<summary><strong>Dump Verifier</strong></summary>

- Verify ROM dump integrity by checking for overdumps, underdumps, blank regions, and bad headers
- Validates file sizes against expected sizes for each system
- Power-of-two size checks and trailing padding analysis

</details>

<details>
<summary><strong>Duplicate Finder</strong></summary>

- Scan directories recursively to find duplicate ROMs by CRC32 hash
- Shows duplicate groups with file paths and wasted disk space
- Delete duplicate files with a single click to free disk space (keeps the first copy in each group)
- Confirmation prompt shows file count and estimated space savings before deletion
- Both scanning and deletion support cancellation

</details>

<details>
<summary><strong>Batch ROM Hasher</strong></summary>

- Calculate CRC32, MD5, SHA-1, and SHA-256 checksums for all ROM files in a directory
- Selectable hash algorithms for speed vs. completeness
- Export results as CSV, text report, SFV checksum, or MD5 sum file

</details>

<details>
<summary><strong>Security & DRM Analysis</strong></summary>

- Detect region locking for all supported systems
- Identify copy protection mechanisms (10NES, CIC chips, TMSS, Nintendo logo checks, Lynx encryption, Atari 7800 digital signature, ColecoVision BIOS check, Intellivision EXEC handshake, Jaguar encrypted boot, MSX cartridge marker, Sega CD security ring)
- Validate checksum integrity (SNES internal checksums, N64 CRC, Game Boy header checksums, GBA header checksums, Mega Drive internal checksums, iNES headers)
- Single ROM or batch directory analysis

</details>

<details>
<summary><strong>GoodTools Identifier</strong></summary>

- Identify GoodTools labelling conventions (Country Codes, Standard Codes, and GoodGen-Specific Codes) from ROM filenames
- Decode each code tag to its meaning (e.g., `[!]` = Verified Good Dump, `(U)` = USA)
- Single ROM or batch directory processing

</details>

### ROM Management

<details>
<summary><strong>Header Export</strong></summary>

- Export ROM header information to text reports or CSV files
- Batch export for entire directories with system summary

</details>

<details>
<summary><strong>SNES Copier Header Tool</strong></summary>

- Detect, add, and remove the 512-byte copier header found in some SNES ROM dumps
- Useful for compatibility with different emulators and flash carts

</details>

<details>
<summary><strong>Batch Header Fixer</strong></summary>

- Fix ROM headers for all supported ROMs in a directory
- Supported operations:
  - SNES internal checksum recalculation
  - NES header cleanup
  - Game Boy / GBC header and global checksum
  - GBA header checksum
  - Mega Drive / Genesis checksum
  - Sega 32X checksum
  - SMS / Game Gear TMR SEGA checksum
  - N64 CRC1/CRC2 checksum (CIC-NUS-6102)
  - Atari 7800 header validation
  - Atari Lynx LYNX header cleanup
  - PC Engine copier header cleanup
  - Virtual Boy header validation
  - Neo Geo Pocket header validation
  - Atari Jaguar header validation
  - MSX cartridge header validation
  - ColecoVision header validation
  - Watara Supervision header validation
  - Nintendo DS header CRC16 recalculation
  - Intellivision header validation
- Single file or batch processing

</details>

<details>
<summary><strong>ROM Trimmer</strong></summary>

- Analyze and trim trailing padding bytes (0x00 / 0xFF) from ROM files
- Power-of-two size alignment preserves compatibility
- Shows space savings before trimming

</details>

<details>
<summary><strong>ROM Renamer</strong></summary>

- Rename ROM files based on header-detected game titles, regions, and system info
- Preview all renames before applying
- Single file or batch rename for entire directories
- Sanitizes file names for cross-platform compatibility

</details>

<details>
<summary><strong>Metadata Scraper</strong></summary>

- Scrape metadata from ROM files in bulk (header info, checksums, system details)
- Export results to CSV or text reports
- Optional checksum calculation for each ROM

</details>

### Cheats & Emulation

<details>
<summary><strong>Cheat Code Converter</strong></summary>

- Decode and encode Game Genie codes for NES, SNES, Game Boy, Game Boy Color, Sega Genesis, and Game Gear
- Decode and encode Pro Action Replay codes for SNES, Genesis, Game Boy, Master System, Sega 32X, and Sega CD
- Decode and encode N64 GameShark codes (9 code types including write, uncached, repeat, and activator)
- Decode and encode GBA GameShark / Action Replay codes (12 code types)
- Decode and encode Game Boy Color GameShark codes
- Decode and encode PC Engine raw cheat codes (address:value format)
- Decode and encode Neo Geo Pocket GameShark codes
- Decode and encode Nintendo DS Action Replay codes (16 code types)
- Decode and encode Sega Saturn Action Replay codes
- Decode and encode Sega Dreamcast CodeBreaker codes
- Decode and encode Game Boy GameShark codes
- Decode and encode Neo Geo raw cheat codes
- Decode and encode PlayStation GameShark codes (18 code types)
- Shows decoded address, value, and compare value components

</details>

<details>
<summary><strong>Emulator Config Generator</strong></summary>

- Generate configuration files for RetroArch, Mesen, Snes9x, Project64, mGBA, Kega Fusion, Mednafen, Stella, FCEUX, and MAME
- Mednafen supports per-system settings for PC Engine, Lynx, Neo Geo Pocket, SMS, Game Gear, Virtual Boy, NES, SNES, Game Boy, GBA, and Mega Drive
- Configurable video, audio, and input settings
- Set ROM, save, and save-state directory paths

</details>

### Settings

<details>
<summary><strong>Big Picture Mode</strong></summary>

- Fullscreen, controller-friendly ROM library browser designed for couch gaming
- Card-based grid with color-coded system banners, box art thumbnails, and game information
- Native **SDL2 gamepad support** — navigate, select, and launch ROMs with a game controller
- System filter, text search, and six sort options (name, system, size, recently played)
- **Favorites system** — mark ROMs as favorites with the **F** key or detail panel button, filter to show only favorites with the ★ toggle
- **Recently played** — automatically tracks launched ROMs, sort by recently played to find recent games
- **ROM Info overlay** — press **I** to view header details, checksums, and GoodTools codes for the selected ROM
- **ROM count badge** — always-visible count in the status bar showing filtered and total ROM count
- **Grid zoom** — press **+** / **−** to scale the card grid from 50% to 200%, persisted across sessions
- **Keyboard shortcuts help** — press **H** or **?** to show a help overlay listing all keyboard shortcuts
- **Play statistics** — tracks how many times each ROM has been launched; displayed in the detail panel
- Detail panel with box art, screenshots, title screen artwork, favorite toggle, play count, file path, and system badge
- **Random Game** button to jump to a random ROM (avoids re-selecting the same game) and **Rescan** button to refresh the folder
- Launch ROMs directly with RetroArch from the detail panel
- Keyboard navigation: arrow keys to browse, Enter/Space to launch, F to toggle favorite, I for ROM Info, +/− to zoom, H for help, Escape to exit or defocus search, Tab to search, Home/End, PageUp/PageDown
- Fully localized status messages — all 20 languages supported
- Toggle from the ROM Browser toolbar or configure auto-start in Settings
- Minimizes to tray during gameplay and restores when the emulator exits

</details>

<details>
<summary><strong>Native Menu</strong></summary>

- Platform-native application menu for macOS and supported Linux desktops
- Mirrors all sidebar navigation categories and items for keyboard-driven access
- Includes File (Exit) and Help (Settings) menus

</details>

<details>
<summary><strong>System Tray</strong></summary>

- System tray icon with Show / Exit context menu
- Automatically minimizes to the system tray when launching a ROM with RetroArch (configurable)
- Automatically restores when the emulator process exits
- Tray icon appears only while the window is minimized to the tray

</details>

<details>
<summary><strong>Application Updates</strong></summary>

- Automatically check for new versions on startup (configurable)
- Download and install updates directly from the application
- Progress reporting with cancel support during download
- Falls back to opening the release page in a browser if no platform-specific asset is found
- External updater process applies updates safely after the main application exits

</details>

<details>
<summary><strong>RetroArch Core Downloader</strong></summary>

- Auto-detect or manually configure the RetroArch executable path
- Scan for installed libretro cores and identify missing ones
- Download all missing cores from the official RetroArch buildbot
- Supports Windows (x64), Linux (x64, ARM64), and macOS (Intel, Apple Silicon)
- Handles Flatpak and Snap RetroArch installations on Linux
- Download progress with cancel support

</details>

<details>
<summary><strong>Controller Profiles</strong></summary>

- Download and update SDL2 game controller profiles from the [SDL_GameControllerDB](https://github.com/gabomdq/SDL_GameControllerDB) repository
- Enables automatic gamepad recognition for Big Picture Mode
- Shows the number of controller mappings currently installed

</details>

<details>
<summary><strong>SDL2 Gamepad Mapping Tool</strong></summary>

- Create custom SDL2 controller mappings for unrecognized or unmapped game controllers
- Step-by-step mapping wizard for all 21 standard SDL2 game controller elements (face buttons, D-pad, triggers, stick axes)
- Save custom mappings permanently or copy to clipboard
- Manage and delete saved custom mappings
- Mappings are automatically applied at startup in Big Picture Mode

</details>

<details>
<summary><strong>Discord Rich Presence</strong></summary>

- Automatically updates your Discord status when launching a ROM via RetroArch
- Shows the game being played and the system it runs on
- Status is cleared when the emulator exits
- Enable or disable in Settings

</details>

### MAME

<details>
<summary><strong>ROM Set Auditor</strong></summary>

- Audit MAME ROM sets against a MAME XML database (from `mame -listxml` or Logiqx DAT)
- Verifies ZIP-packaged ROM sets for completeness, correct CRC32 checksums, and proper file sizes
- Reports good, incomplete, and bad sets with detailed per-ROM status
- Identifies clones and parent ROM relationships
- Detects missing machines in ROM directory
- Optional recursive subdirectory search

</details>

<details>
<summary><strong>CHD Verifier</strong></summary>

- Verify MAME CHD (Compressed Hunks of Data) file integrity
- Reads and validates CHD v3, v4, and v5 headers
- Reports SHA-1 and raw SHA-1 checksums, compression type, logical size, hunk size, and unit size
- Detects parent CHD dependencies
- Single file or batch directory verification

</details>

<details>
<summary><strong>ROM Set Rebuilder</strong></summary>

- Rebuild MAME ROM sets from scattered or loose ROM files (similar to CLRMamePro Rebuilder)
- Indexes source directory recursively by CRC32 — supports both loose files and files inside ZIP archives
- Creates properly structured ZIP archives matching the MAME XML database
- Three rebuild modes: **Split** (clones reference parent), **Non-Merged** (each ZIP is self-contained), **Merged** (parent ZIP includes clone ROMs)
- Option to rebuild only complete sets or include partial sets
- Overwrite or skip existing ZIP files

</details>

<details>
<summary><strong>Dir2Dat Creator</strong></summary>

- Create a DAT file from a directory of ROM files (similar to CLRMamePro Dir2Dat)
- Scans ZIP archives and optionally loose files
- Computes CRC32, SHA-1, and MD5 checksums
- Reads CHD file headers for disk entries
- Exports in Logiqx XML format compatible with CLRMamePro and other ROM managers
- Configurable DAT metadata (name, description, author)

</details>

<details>
<summary><strong>Sample Auditor</strong></summary>

- Audit MAME sample audio files against a MAME XML database
- Verifies that sample ZIP archives contain the expected WAV files for each machine
- Reports good, incomplete, and bad sample sets with missing file details
- Handles shared sample sets (sampleof attribute)
- Detects missing sample sets
- Optional recursive subdirectory search

</details>

### RetroArch

<details>
<summary><strong>RetroArch Playlist Creator</strong></summary>

- Create RetroArch playlists (.lpl) from a directory of ROM files
- Select from all supported systems with automatic database name mapping
- Load and edit existing RetroArch playlists
- Add or remove individual playlist entries
- Download thumbnails (box art, snapshots, title screens) from the [Libretro Thumbnails](https://github.com/libretro-thumbnails) database
- Selectable thumbnail categories with overwrite option
- Save playlists in RetroArch's JSON .lpl format
- Automatic playlist and thumbnail directory detection for Windows, Linux, and macOS

</details>

<details>
<summary><strong>RetroArch Shortcut Creator</strong></summary>

- Create desktop shortcuts that launch a ROM directly in RetroArch with a specific libretro core
- Windows `.lnk` shortcuts (created via PowerShell), Linux `.desktop` files, macOS `.command` scripts
- Select system and core from a dropdown of all available cores
- Optional custom icon, fullscreen mode, and extra RetroArch arguments
- Override RetroArch path per shortcut
- Automatic core directory detection (including Flatpak, Snap, and Homebrew locations)
- Shell-safe argument escaping on Linux and macOS

</details>

<details>
<summary><strong>RetroAchievements Writer</strong></summary>

- Create and edit achievement definition files compatible with [RetroAchievements.org](https://retroachievements.org)
- Standard RACache local achievement JSON format for import into the RetroAchievements web toolkit
- Add achievements with title, description, points, memory address conditions, type, author, and badge ID
- Achievement types: Standard, Missable, Progression, Win Condition
- Supports 78 console IDs (NES, SNES, N64, Mega Drive, Game Boy, PlayStation, and many more)
- Validate achievement sets for completeness and correctness
- Export as plain-text local format for RAIntegration / RALibretro
- Load and save achievement sets as JSON files

</details>

### Analogue

<details>
<summary><strong>Analogue Pocket</strong></summary>

- Browse and manage openFPGA cores installed on the Analogue Pocket SD card
- Export Pocket screenshots (BMP) to a destination folder
- Backup and restore save files with directory structure preserved
- Manage and delete save states with multi-select and batch delete
- Extract and export Game Boy Camera photos from `.sav` files as grayscale BMP images
- Auto-copy files from a source folder to the Pocket SD card matching the directory structure
- Generate placeholder library images for ROMs that don't have one (160×144 BMP with game name)
- Browse game platform folders under Assets
- SD card validation to verify correct Pocket directory structure

</details>

<details>
<summary><strong>Analogue Mega SG</strong></summary>

- Generate custom 8×8 pixel fonts for the Mega SG on-screen menu
- Create fonts from a 128×128 BMP source image (16×16 grid of 8×8 characters) or use the built-in default font
- Convert save files between formats with endianness swapping, power-of-two padding, and trailing byte trimming
- Analyze save file type (EEPROM, SRAM, Flash) from file size
- Convert between `.sav` and `.srm` save formats

</details>

<details>
<summary><strong>Analogue NT / Super NT</strong></summary>

- Generate custom 8×8 pixel fonts for the Analogue NT and Super NT on-screen menus
- Create fonts from a 128×128 BMP source image or use the built-in default font
- Repair NES ROM headers using a safe temp-file workflow (writes to temp file first, then replaces original)

</details>

<details>
<summary><strong>Analogue 3D</strong></summary>

- Manage N64 Game Pak ROMs on the Analogue 3D SD card
- Scan and list all Game Paks with metadata (internal name, game code, file size)
- Per-game display settings: resolution, aspect ratio, smoothing, crop overscan
- Per-game hardware settings: Expansion Pak, Rumble Pak, CPU overclock, Controller Pak mode
- Set or remove custom label artwork (PNG) for each Game Pak
- Atomic settings writes (temp file + rename) to prevent SD card corruption
- SD card validation to verify correct Analogue 3D directory structure

</details>

## Supported Systems

| System | Extensions |
|---|---|
| Amiga CD32 | `.iso`, `.cue` |
| Amstrad CPC | `.dsk`, `.cdt`, `.sna` |
| Arcade (MAME) | `.zip` |
| Atari 2600 | `.a26` |
| Atari 5200 | `.a52` |
| Atari 7800 | `.a78` |
| Atari 800 / XL / XE | `.atr`, `.xex`, `.car`, `.cas` |
| Atari Jaguar | `.j64`, `.jag` |
| Atari Lynx | `.lnx`, `.lyx` |
| Coleco ColecoVision | `.col`, `.cv` |
| Fairchild Channel F | `.chf` |
| Game Boy | `.gb` |
| Game Boy Advance | `.gba` |
| Game Boy Color | `.gbc` |
| Mattel Intellivision | `.int` |
| Memotech MTX | `.mtx`, `.run` |
| MSX | `.mx1` |
| MSX2 | `.mx2` |
| NEC PC-88 | `.d88`, `.t88` |
| Nintendo 3DS | `.3ds`, `.cia` |
| Nintendo 64 | `.z64`, `.n64`, `.v64` |
| Nintendo 64DD | `.ndd` |
| Nintendo DS | `.nds` |
| Nintendo Entertainment System (NES) | `.nes` |
| Nintendo GameCube | `.gcm`, `.iso` |
| Nintendo Virtual Boy | `.vb`, `.vboy` |
| Nintendo Wii | `.iso` |
| Oric / Atmos / TeleStrat | `.tap` |
| Panasonic 3DO | `.3do`, `.iso`, `.cue` |
| PC Engine / TurboGrafx-16 | `.pce`, `.tg16` |
| Philips CD-i | `.iso`, `.cue` |
| Radio Shack Color Computer | `.ccc` |
| Sega 32X | `.32x` |
| Sega CD | `.iso`, `.cue` |
| Sega Dreamcast | `.cdi`, `.gdi`, `.iso`, `.cue` |
| Sega Game Gear | `.gg` |
| Sega Master System | `.sms` |
| Sega Mega Drive / Genesis | `.md`, `.gen`, `.bin` |
| Sega Saturn | `.iso`, `.cue` |
| SNK Neo Geo | `.neo` |
| SNK Neo Geo CD | `.iso`, `.cue` |
| SNK Neo Geo Pocket / Pocket Color | `.ngp`, `.ngc` |
| Super Nintendo (SNES) | `.smc`, `.sfc` |
| Thomson MO5 | `.mo5`, `.k7`, `.fd` |
| Tiger Game Com | `.tgc` |
| Watara Supervision | `.sv` |

### System Feature Coverage

| System | Header Parsing | Security Analysis | Dump Verification | Cheat Codes | Header Fixing |
|---|---|---|---|---|---|
| Amiga CD32 | ✔ | ✔ | ✔ | — | — |
| Amstrad CPC | ✔ | ✔ | ✔ | — | — |
| Arcade (MAME) | — | — | — | — | — |
| Atari 2600 | ✔ | ✔ | ✔ | — | — |
| Atari 5200 | ✔ | ✔ | ✔ | — | — |
| Atari 7800 | ✔ | ✔ | ✔ | — | ✔ |
| Atari 800 | ✔ | ✔ | ✔ | — | — |
| Atari Jaguar | ✔ | ✔ | ✔ | — | ✔ |
| Atari Lynx | ✔ | ✔ | ✔ | — | ✔ |
| ColecoVision | ✔ | ✔ | ✔ | — | ✔ |
| Color Computer | ✔ | ✔ | ✔ | — | — |
| Fairchild Channel F | ✔ | ✔ | ✔ | — | — |
| Game Boy | ✔ | ✔ | ✔ | ✔ | ✔ |
| Game Boy Advance | ✔ | ✔ | ✔ | ✔ | ✔ |
| Game Boy Color | ✔ | ✔ | ✔ | ✔ | ✔ |
| Intellivision | ✔ | ✔ | ✔ | — | ✔ |
| Mega Drive / Genesis | ✔ | ✔ | ✔ | ✔ | ✔ |
| Memotech MTX | ✔ | ✔ | ✔ | — | — |
| MSX / MSX2 | ✔ | ✔ | ✔ | — | ✔ |
| N64 | ✔ | ✔ | ✔ | ✔ | ✔ |
| N64DD | ✔ | ✔ | ✔ | — | — |
| NEC PC-88 | ✔ | ✔ | ✔ | — | — |
| Neo Geo | ✔ | ✔ | ✔ | ✔ | — |
| Neo Geo CD | ✔ | ✔ | ✔ | — | — |
| Neo Geo Pocket | ✔ | ✔ | ✔ | ✔ | ✔ |
| NES | ✔ | ✔ | ✔ | ✔ | ✔ |
| Nintendo 3DS | ✔ | ✔ | ✔ | — | — |
| Nintendo DS | ✔ | ✔ | ✔ | ✔ | ✔ |
| Nintendo GameCube | ✔ | ✔ | ✔ | — | — |
| Nintendo Wii | ✔ | ✔ | ✔ | — | — |
| Oric | ✔ | ✔ | ✔ | — | — |
| Panasonic 3DO | ✔ | ✔ | ✔ | — | — |
| PC Engine | ✔ | ✔ | ✔ | ✔ | ✔ |
| Philips CD-i | ✔ | ✔ | ✔ | — | — |
| Sega 32X | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega CD | ✔ | ✔ | ✔ | ✔ | — |
| Sega Dreamcast | ✔ | ✔ | ✔ | ✔ | — |
| Sega Game Gear | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega Master System | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega Saturn | ✔ | ✔ | ✔ | ✔ | — |
| SNES | ✔ | ✔ | ✔ | ✔ | ✔ |
| Thomson MO5 | ✔ | ✔ | ✔ | — | — |
| Tiger Game Com | ✔ | ✔ | ✔ | — | — |
| Virtual Boy | ✔ | ✔ | ✔ | — | ✔ |
| Watara Supervision | ✔ | ✔ | ✔ | — | ✔ |

## Localization

The application is available in 20 languages:

English, Spanish, French, German, Portuguese, Italian, Japanese, Chinese (Simplified), Korean, Russian, Dutch, Polish, Turkish, Arabic, Hindi, Thai, Swedish, Czech, Vietnamese, Indonesian

## Building from Source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/SvenGDK/RetroMultiTools.git
cd RetroMultiTools
dotnet build
```

The solution contains two projects:

| Project | Description |
|---|---|
| `RetroMultiTools` | Main Avalonia desktop application |
| `RetroMultiTools.Updater` | External updater console app — applies downloaded updates after the main application exits |

To run the application:

```bash
dotnet run --project RetroMultiTools
```

## License

BSD 2-Clause License — see [LICENSE](LICENSE) for details.
