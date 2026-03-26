# Project Structure

Overview of the repository layout and code architecture.

---

## Repository Layout

```
RetroMultiTools/
├── docs/                          # Documentation
│   ├── features/                  # Feature documentation
│   ├── reference/                 # Reference guides
│   └── development/               # Developer documentation
├── RetroMultiTools/               # Main application project (Avalonia Desktop)
│   ├── Detection/                 # ROM system detection
│   ├── Localization/              # Language management
│   ├── Models/                    # Data models
│   ├── Resources/                 # Localization strings and assets
│   ├── Services/                  # Application services
│   ├── Utilities/                 # Core feature implementations
│   │   ├── Analogue/              # Analogue device utilities (Pocket, Mega SG, 3D)
│   │   ├── GamepadKeyMapper/      # Gamepad-to-keyboard/mouse mapping engine
│   │   ├── Mame/                  # MAME arcade utilities (auditor, CHD, rebuilder)
│   │   ├── Mednafen/              # Mednafen emulator launcher
│   │   └── RetroArch/             # RetroArch integration utilities (launcher, cores, playlists)
│   └── Views/                     # UI views (AXAML + code-behind)
│       ├── Analogue/              # Analogue device views
│       ├── Mame/                  # MAME tool views
│       ├── Mednafen/              # Mednafen integration views
│       └── RetroArch/             # RetroArch integration views
├── RetroMultiTools.Updater/       # External updater project (Console)
│   └── Program.cs                 # Update extraction and relaunch logic
├── RetroMultiTools.slnx           # Solution file
├── README.md                      # Project overview
├── LINUX.md                       # Linux installation guide
├── macOS.md                       # macOS installation guide
└── LICENSE                        # BSD 2-Clause License
```

---

## Architecture

Retro Multi Tools is a two-project **.NET 8** solution:

| Project | Type | Description |
|---|---|---|
| `RetroMultiTools` | Avalonia Desktop (WinExe) | Main application with UI, utilities, and feature logic |
| `RetroMultiTools.Updater` | Console (Exe) | External updater that applies downloaded updates after the main app exits |

The main application follows a straightforward view + utility architecture.

### Detection

`Detection/RomDetector.cs` — maps file extensions to `RomSystem` enum values. Used by the ROM Browser, ROM Inspector, and other tools to identify which system a ROM belongs to.

### Models

| File | Description |
|---|---|
| `RomSystem.cs` | Enum of all supported systems (NES, SNES, N64, Arcade, etc.) |
| `RomInfo.cs` | Data model for scanned ROM files (path, system, size) |
| `ArtworkInfo.cs` | Data model for artwork metadata (box art, screenshots) |
| `RemoteTarget.cs` | Connection configuration for remote file transfers |

### Services

| File | Description |
|---|---|
| `AppSettings.cs` | Singleton settings service — persists configuration to JSON |
| `ArtworkService.cs` | Fetches and caches artwork from the Libretro Thumbnails repository |
| `GamepadService.cs` | Singleton SDL2 game controller service for Big Picture Mode gamepad input |
| `SDL2Interop.cs` | P/Invoke bindings for the SDL2 native library used by GamepadService |

### Utilities

Each utility is a `static class` containing the core logic for a feature. Utilities:

- Accept input parameters and return results (or write to output files).
- Report progress via `IProgress<string>`.
- Support cancellation via `CancellationToken` where applicable.
- Use `ConfigureAwait(false)` on all `await` expressions (they run off the UI thread).
- Clean up partial output files on failure using a try-catch-delete pattern.

Utilities are organized into subdirectories by category:

#### Utilities/ (General)

| File | Description |
|---|---|
| `AppUpdater.cs` | Checks GitHub Releases API for updates, downloads ZIPs, launches the external updater, and cleans up after updates |
| `BatchHasher.cs` | Computes CRC32, MD5, SHA-1, and SHA-256 checksums for all ROMs in a directory with export to CSV, text, SFV, or MD5 sum |
| `BatchHeaderFixer.cs` | Fixes ROM headers (checksums, validation) across all supported systems in batch |
| `BpsPatcher.cs` | Applies BPS patches with full CRC32 validation of source, target, and patch data |
| `CheatCodeConverter.cs` | Encodes and decodes cheat codes for Game Genie, Pro Action Replay, GameShark, CodeBreaker, and more |
| `ChecksumCalculator.cs` | Computes CRC32, MD5, SHA-1, and SHA-256 checksums for individual ROM files |
| `DatFilter.cs` | Filters DAT file entries with category exclusion, region/language priority, and 1G1R deduplication |
| `DatVerifier.cs` | Verifies ROM files against No-Intro and TOSEC DAT databases by CRC32, MD5, or SHA-1 |
| `DiscordRichPresence.cs` | Discord Rich Presence integration via IPC pipe — shows current game activity |
| `DumpVerifier.cs` | Checks ROM dump quality (overdumps, underdumps, blank regions, bad headers, size validation) |
| `DuplicateFinder.cs` | Scans directories for duplicate ROM files by CRC32 hash and optionally deletes extra copies |
| `EmulatorConfigGenerator.cs` | Generates configuration files for RetroArch, Mesen, Snes9x, Project64, and other emulators |
| `FileUtils.cs` | Shared file I/O helper methods used by other utilities |
| `GamepadMappingStorage.cs` | Persists and loads custom SDL2 controller mappings from disk |
| `GoodToolsIdentifier.cs` | Identifies GoodTools labelling conventions (country, standard, and GoodGen codes) from ROM filenames |
| `HexViewer.cs` | Reads and formats ROM file contents for hexadecimal display with page-based navigation |
| `IpsPatcher.cs` | Applies IPS patches with RLE support and optional truncation |
| `MetadataScraper.cs` | Scrapes header info, checksums, and system details from ROM files in bulk |
| `N64FormatConverter.cs` | Converts between N64 ROM byte orders (.z64, .n64, .v64) |
| `PatchCreator.cs` | Creates IPS patches by comparing original and modified ROM files |
| `RemoteTransferService.cs` | Transfers files to remote targets via FTP, SFTP, WebDAV, Amazon S3, and cloud storage providers |
| `RomComparer.cs` | Streaming byte-by-byte binary comparison of two ROM files |
| `RomFormatConverter.cs` | Adds/removes copier headers, converts disc images to CHD or RVZ format |
| `RomHeaderExporter.cs` | Exports ROM header information to text or CSV reports |
| `RomHostingService.cs` | Built-in HTTP server for sharing ROMs on the local network |
| `RomOrganizer.cs` | Sorts scanned ROMs into system-specific folders |
| `RomRenamer.cs` | Renames ROM files based on header-detected titles and regions |
| `RomTrimmer.cs` | Trims trailing padding bytes from ROM files with power-of-two alignment |
| `SaveFileConverter.cs` | Converts save files between formats with endianness swap and padding options |
| `SecurityAnalyzer.cs` | Detects region locking, copy protection, and checksum integrity across all supported systems |
| `SnesHeaderTool.cs` | Detects, adds, and removes the 512-byte copier header from SNES ROM dumps |
| `SplitRomAssembler.cs` | Reassembles split ROM files (.001/.002, .part1/.part2, .z01/.z02) into a single file |
| `XdeltaPatcher.cs` | Applies xDelta/VCDIFF patches (RFC 3284) with address cache and Adler-32 verification |
| `ArchiveManager.cs` | Unified archive manager: extract ROMs from ZIP/RAR/7z/GZip, create ZIP archives, single or batch |

#### Utilities/Analogue/

| File | Description |
|---|---|
| `Analogue3DManager.cs` | Manages Analogue 3D N64 Game Pak settings on SD card |
| `AnalogueFontGenerator.cs` | Generates 8×8 pixel fonts for Analogue Mega SG, NT, and Super NT |
| `AnaloguePocketManager.cs` | Manages Analogue Pocket SD card operations (cores, saves, screenshots) |

#### Utilities/GamepadKeyMapper/

| File | Description |
|---|---|
| `GamepadKeyMapperEngine.cs` | Core gamepad-to-keyboard/mouse mapping engine with profile and set management |
| `GamepadKeyMapperModels.cs` | Data models for button mappings, profiles, mapping sets, and auto-profile rules |
| `ActiveWindowMonitor.cs` | Monitors the active window for auto-profile switching |
| `InputSimulator.cs` | Simulates keyboard and mouse input on the host system |

#### Utilities/Mame/

| File | Description |
|---|---|
| `MameChdConverter.cs` | Converts MAME CHD (Compressed Hunks of Data) files between formats |
| `MameChdVerifier.cs` | Verifies MAME CHD file integrity (headers, checksums, compression metadata) |
| `MameCrc32.cs` | Shared CRC32 helper for MAME ROM auditing |
| `MameDatEditor.cs` | Edits MAME DAT database files |
| `MameDir2Dat.cs` | Creates Logiqx XML DAT files from a directory of ROM ZIPs, loose files, and CHD disks |
| `MameLauncher.cs` | Launches standalone MAME emulator with ROM sets |
| `MameRomAuditor.cs` | Audits MAME ROM sets against a MAME XML database for completeness and CRC32 correctness |
| `MameSampleAuditor.cs` | Audits MAME sample audio ZIPs for expected WAV files per machine |
| `MameSetRebuilder.cs` | Rebuilds MAME ROM sets (split, non-merged, or merged) from scattered files |

#### Utilities/Mednafen/

| File | Description |
|---|---|
| `MednafenLauncher.cs` | Launches ROMs with the Mednafen emulator with automatic module selection |

#### Utilities/RetroArch/

| File | Description |
|---|---|
| `ControllerProfileDownloader.cs` | Downloads SDL2 game controller profile database from the SDL_GameControllerDB repository |
| `RetroAchievementsWriter.cs` | Creates and edits RetroAchievements definition files in RACache JSON format |
| `RetroArchCoreDownloader.cs` | Downloads libretro cores from the official RetroArch buildbot |
| `RetroArchLauncher.cs` | Launches ROMs with RetroArch and manages the emulator process lifecycle |
| `RetroArchPlaylistCreator.cs` | Creates and edits RetroArch playlists (.lpl) with thumbnail downloading |
| `RetroArchShortcutCreator.cs` | Creates OS-specific desktop shortcuts for launching ROMs via RetroArch |

### RetroMultiTools.Updater

A standalone console application that applies updates. It is bundled alongside the main executable in release builds.

| Step | Description |
|---|---|
| 1 | Parses `--pid`, `--zip`, `--target`, `--exe` arguments |
| 2 | Waits for the main application process to exit (up to 60 seconds) |
| 3 | Extracts the update ZIP over the installation directory with path-traversal protection |
| 4 | Skips replacing its own running executable (extracts to `.new`, swaps via `.bak`) |
| 5 | Cleans up the downloaded ZIP and temp directory |
| 6 | Relaunches the main application |

Logs are written to `%TEMP%/RetroMultiTools-Update/updater.log` for debugging.

### Views

Each feature has a pair of files:

- `FeatureView.axaml` — XAML layout (Avalonia markup).
- `FeatureView.axaml.cs` — code-behind with event handlers.

Views are organized into subdirectories by category:

- `Views/` — General ROM tool views (browser, inspector, patcher, checksum, etc.)
- `Views/Analogue/` — Analogue device views (Pocket, Mega SG, NT/Super NT, 3D)
- `Views/Mame/` — MAME tool views (auditor, CHD, rebuilder, Dir2Dat, DAT editor, integration)
- `Views/Mednafen/` — Mednafen emulator integration view
- `Views/RetroArch/` — RetroArch integration views (playlists, shortcuts, achievements)

Views handle:

- File/folder picker dialogs
- Calling utility methods with progress reporting
- Displaying results and error messages
- Exception handling via `catch (Exception ex) when (ex is ...)` filters for specific exception types

### Localization

| File | Description |
|---|---|
| `Localization/LocalizationManager.cs` | Manages the current culture and language switching |
| `Resources/Strings.resx` | English (default) resource strings |
| `Resources/Strings.{culture}.resx` | Translated strings for 19 additional languages |

All user-facing text uses resource keys from `Strings.resx`. The `LocalizationManager` switches the active culture at runtime.

### Main Window

`MainWindow.axaml` defines the sidebar navigation with 44+ feature entries organized into categories (Browse & Inspect, Patching & Conversion, Analysis & Verification, Headers & Trimming, Utilities, RetroArch, MAME, Mednafen, Analogue, Settings). Clicking a menu item swaps the content area to the corresponding view. Three features open separate modal windows: `GamepadMapperWindow` (SDL2 gamepad mapping tool), `HostRomsWindow` (ROM hosting server), and `SendToRemoteWindow` (remote file transfer).

`MainWindow.axaml.cs` provides public methods for external control:

| Method / Property | Description |
|---|---|
| `NavigateToView(tag)` | Switches the content area to the view matching the given sidebar tag and syncs the sidebar selection |
| `MinimizeToTray()` | Hides the window and removes it from the taskbar |
| `RestoreFromTray()` | Shows the window, restores the previous window state, and activates it |
| `IsMinimizedToTray` | Returns `true` when the window is hidden in the system tray |
| `IsMinimizedToTrayChanged` | Event raised when `IsMinimizedToTray` changes (used by `App` to toggle tray icon visibility) |

### Application Shell

`App.axaml.cs` builds the platform-native menu (`NativeMenu`) and system tray icon (`TrayIcon`) in code-behind to support localized menu item labels. The native menu mirrors all sidebar categories and items. The tray icon appears only while the window is minimized to the tray and provides Show / Exit actions.

---

## Key Patterns

### CRC32 Computation

All utilities that compute CRC32 use a **table-based** approach with a pre-generated lookup table (polynomial `0xEDB88320`). Each utility has its own private table to avoid cross-class dependencies.

### ROM Extension Sets

ROM extension collections for batch scanning use `static readonly HashSet<string>` with `StringComparer.OrdinalIgnoreCase` for O(1) case-insensitive lookup.

### Error Handling

- **Utility classes** — throw specific exceptions (`IOException`, `InvalidOperationException`, `InvalidDataException`, etc.).
- **View classes** — catch specific exception types using exception filters and display user-friendly messages:

```csharp
catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
{
    ShowStatus($"✘ Error: {ex.Message}", isError: true);
}
```

### Output File Cleanup

All file-writing utility methods use a try-catch cleanup pattern:

```csharp
try
{
    await File.WriteAllTextAsync(outputPath, content).ConfigureAwait(false);
}
catch
{
    try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
    throw;
}
```

This ensures partial output files are removed if an error occurs during writing.
