# Big Picture Mode Guide

A fullscreen, controller-friendly interface for browsing and launching ROMs, designed for couch gaming.

---

## Entering Big Picture Mode

1. Open the **ROM Browser** and scan a ROM folder.
2. Click **Big Picture Mode** in the toolbar, or enable **Start in Big Picture Mode automatically** in Settings.
3. The application hides the sidebar and title bar, switches to fullscreen, and displays the card-based grid.

Artwork is pre-loaded in the background so selections display instantly.

---

## Browsing & Navigation

### Keyboard Controls

| Key | Action |
|---|---|
| **←** **→** | Move selection left / right |
| **↑** **↓** | Move selection up / down one row |
| **Enter** / **Space** | Launch the selected ROM |
| **F** | Toggle favorite |
| **R** | Pick a random game |
| **I** | Show / hide ROM Info overlay |
| **+** / **−** | Zoom in / zoom out (50%–200%) |
| **H** / **?** | Show / hide keyboard shortcuts help overlay |
| **Escape** / **Backspace** | Exit Big Picture Mode (or defocus search) |
| **Tab** | Focus the search box |
| **Home** / **End** | Jump to first / last card |
| **PageUp** / **PageDown** | Scroll by several rows |

### Game Controller

Big Picture Mode supports native gamepad input via the SDL2 Game Controller API. Controllers are automatically recognised when connected.

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

#### Controller Setup

Controllers are configured automatically via the SDL2 controller database. To ensure your controller is recognised:

1. Go to **Settings** → **Controller Profiles** and click **Download / Update Profiles**.
2. If your controller is still not recognised, open the **Gamepad Mapping Tool** to create a custom mapping.

See [Settings — Controller Profiles](../configuration/settings.md#controller-profiles) for details.

#### SDL2 Requirement

| Platform | Install |
|---|---|
| **Windows** | Place `SDL2.dll` next to the executable, or install via [libsdl.org](https://libsdl.org) |
| **Linux** | `sudo apt install libsdl2-2.0-0` (or equivalent for your distribution) |
| **macOS** | `brew install sdl2` |

If SDL2 is not available, Big Picture Mode still works with keyboard input only.

---

## Filtering, Searching & Sorting

- **System filter** — select a specific console/computer from the dropdown to filter the grid.
- **Search** — press **Tab** to focus the search box and type to filter by game name.
- **Sort** — six sort options: name, system, size, and recently played.
- **Favorites** — click **★ Favorites** in the toolbar or press **F** to toggle favorites. Use the ★ toggle to show only favorites.

---

## Detail Panel

Selecting a card opens a side panel showing:

- Game title, system badge, and favorite toggle button
- File size, ROM validity, and play count
- Full file path
- Box art, screenshot, and title screen artwork (fetched from Libretro Thumbnails)
- **Launch with RetroArch** button
- **Launch with MAME** button (for Arcade ROMs when MAME is configured)

---

## ROM Info Overlay

Press **I** to view an overlay with:

- Parsed header details (title, mapper, checksums, etc.)
- CRC32, MD5, SHA-1 checksums
- GoodTools codes decoded from the filename

---

## Toolbar

| Button | Description |
|---|---|
| **★ Favorites** | Toggle filter to show only favorite ROMs |
| **Random Game** | Jump to a random ROM (avoids re-selecting the same game) |
| **Rescan** | Re-scan the current folder for newly added ROMs |
| **Select Folder** | Open a folder picker to scan a different directory |
| **Exit Big Picture** | Return to the standard ROM Browser view |

---

## Status Bar

| Element | Description |
|---|---|
| **Controller badge** | Connected controller name (e.g., "🎮 Xbox Controller"), or "No controller" |
| **Zoom badge** | Current card grid zoom level (e.g., "Zoom: 100%") |
| **ROM count badge** | Filtered and total ROM count |
| **Hint bar** | Quick-reference keyboard shortcuts or controller button hints |

---

## Grid Zoom

Press **+** or **−** to scale the card grid from 50% to 200%. The zoom level is saved between sessions.

---

## Settings

| Setting | Description | Default |
|---|---|---|
| **Start in Big Picture Mode automatically** | Launch directly into Big Picture Mode on startup | Disabled |
| **ROM folder for Big Picture Mode** | The ROM folder to scan when auto-starting | (none) |
| **Game controller input** | Enable or disable native gamepad support (requires SDL2) | Enabled |
| **Controller dead zone** | Analog stick dead zone (0.05–0.95) | 0.25 |

These settings are in the **Settings** view under **Big Picture Mode**.

---

## Exiting Big Picture Mode

- Press **Escape** or **Backspace**
- Click **Exit Big Picture** in the toolbar
- Press the **Guide** button on a game controller

The application returns to the standard ROM Browser view.
