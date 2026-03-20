# RetroArch Integration

Configuration, core management, playlists, shortcuts, and achievements for RetroArch.

---

## Path Configuration

Configure the RetroArch executable in the **Settings** view.

| Action | Description |
|---|---|
| **Browse** | Manually select the RetroArch executable (on macOS you can select the `.app` bundle directly) |
| **Auto-Detect** | Automatically find RetroArch on the system PATH or common install locations |
| **Download** | Opens the official RetroArch download page in a browser |
| **Clear** | Removes the stored RetroArch path |

The configured path is stored in `settings.json` and persists across sessions.

### What RetroArch Enables

- **ROM Browser** — launch ROMs directly in RetroArch with the appropriate libretro core.
- **Big Picture Mode** — launch ROMs from the fullscreen interface with per-system core mapping.
- **Core Downloader** — install missing cores into the RetroArch cores directory.
- **Playlist Creator** — create and manage RetroArch playlists with thumbnail downloading.
- **Shortcut Creator** — generate desktop shortcuts for direct ROM launching.
- **Discord Rich Presence** — show the game being played in your Discord status.

---

## Core Downloader

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
| Linux | x86_64 or aarch64 (auto-detected) |
| macOS | x86_64 or arm64 (auto-detected) |

### Core Directory

- **Windows / macOS** — the `cores/` directory next to the RetroArch executable.
- **Linux** — if the directory next to the executable is not writable (e.g., system package install), the downloader uses `~/.config/retroarch/cores/`. For Flatpak installations, `~/.var/app/org.libretro.RetroArch/config/retroarch/cores/` is used. For Snap installations, `~/snap/retroarch/current/.config/retroarch/cores/` is used.

---

## Playlist Creator

Create and manage RetroArch playlists (`.lpl` files) from ROM directories, with integrated thumbnail downloading.

### How It Works

1. Select a ROM directory and target system from the dropdown.
2. Click **Build Playlist** to scan the directory for ROM files and create a playlist.
3. Optionally enable recursive scanning to include subdirectories.
4. Review the generated playlist entries in the list.
5. Remove unwanted entries with the **Remove** button.
6. Click **Save** to write the playlist in RetroArch's JSON `.lpl` format.

### Loading Existing Playlists

Click **Load Playlist** to open an existing `.lpl` file for editing. Entries can be added or removed before saving.

### Thumbnail Downloading

1. Build or load a playlist first.
2. Select the thumbnail categories to download: **Boxarts**, **Snaps** (screenshots), and/or **Titles** (title screens).
3. Optionally enable **Overwrite** to replace existing thumbnails.
4. Click **Download Thumbnails** to fetch images from the [Libretro Thumbnails](https://github.com/libretro-thumbnails) repository.
5. A **Cancel** button is available during downloads.

Thumbnails are saved to the RetroArch thumbnails directory. On Linux and macOS, the config directory (`~/.config/retroarch/thumbnails`) is preferred over the executable directory.

### Playlist Directory

| Platform | Directory |
|---|---|
| **Windows** | `<RetroArch dir>\playlists\` |
| **Linux** | `~/.config/retroarch/playlists/` (or Flatpak/Snap equivalent) |
| **macOS** | `~/Library/Application Support/RetroArch/playlists/` |

---

## Shortcut Creator

Create desktop shortcuts that launch a specific ROM directly in RetroArch with a chosen libretro core.

### How It Works

1. Select a ROM file and the target system.
2. Choose a libretro core from the dropdown (auto-selected based on system).
3. Set a shortcut name (auto-filled from the ROM filename).
4. Optionally set an output directory, custom icon, fullscreen mode, or extra RetroArch arguments.
5. Click **Create Shortcut** to generate the shortcut file.

### Platform-Specific Shortcuts

| Platform | Format | Description |
|---|---|---|
| **Windows** | `.lnk` | Standard Windows shortcut created via PowerShell |
| **Linux** | `.desktop` | XDG desktop entry file, auto-set as executable |
| **macOS** | `.command` | Shell script, auto-set as executable |

### Core Discovery

The shortcut creator searches for libretro cores in multiple locations:

- **Windows** — `cores/` next to RetroArch executable
- **Linux** — `cores/` next to executable, `/usr/lib/libretro`, `/usr/lib64/libretro`, `~/.config/retroarch/cores/`, Flatpak and Snap core directories
- **macOS** — `cores/` next to executable, `~/Library/Application Support/RetroArch/cores/`, Homebrew locations

---

## RetroAchievements Writer

Create and edit achievement definition files compatible with [RetroAchievements.org](https://retroachievements.org).

### How It Works

1. Enter a game title, game ID, and select the console.
2. Add achievements with a title, description, points (0–100), memory address condition, type, author, and badge ID.
3. Validate the achievement set for completeness.
4. Save as JSON (RACache local format) or export as plain text for RAIntegration / RALibretro.

### Achievement Types

| Type | Description |
|---|---|
| **Standard** | Default achievement type |
| **Missable** | Can be permanently missed during a playthrough |
| **Progression** | Marks progress through the game |
| **Win Condition** | Awarded for completing the game or a major milestone |

### Supported Consoles

78 console IDs are supported, including NES, SNES, N64, Mega Drive, Game Boy (all variants), PlayStation, PS2, PSP, Atari systems, Neo Geo, Arcade, PC Engine, and many more.

### Validation

The validator checks for:

- Missing game title or game ID
- Invalid console ID
- Empty achievement list
- Missing achievement titles, descriptions, or memory addresses
- Invalid point values (must be 0–100)
- Duplicate achievement IDs

---

## Discord Rich Presence

Automatically updates your Discord status when launching a ROM via RetroArch.

- Shows the game being played and the system it runs on
- Status is cleared when the emulator exits
- Enable or disable in Settings
