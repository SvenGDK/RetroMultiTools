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
| **P** | Toggle the recently played quick-access bar |
| **C** | Show / hide Collection Statistics overlay |
| **G** | Toggle auto-scroll gallery mode |
| **V** | Open / close fullscreen artwork viewer |
| **1** – **5** | Rate the selected game (press same key to clear) |
| **[** / **]** | Cycle system filter backward / forward |
| **A – Z** | Jump to the first ROM starting with that letter |
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
| **LB** | Page Up (modifier for combos below) |
| **RB** | Page Down |
| **LT** | Show / hide Collection Statistics overlay |
| **RT** | Toggle auto-scroll gallery mode |
| **L3 / R3** | Jump to first / last card |
| **Right Stick ↕** | Zoom in / out |
| **LB + A** | Open / close fullscreen artwork viewer |
| **LB + B** | Toggle recently played bar |
| **LB + Y** | Cycle game rating (0–5) |
| **LB + D-Pad ← / →** | Cycle system filter backward / forward |

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
- **Sort** — seven sort options: name, system, size, recently played, and rating.
- **Favorites** — click **★ Favorites** in the toolbar or press **F** to toggle favorites. Use the ★ toggle to show only favorites.

---

## Quick Letter Jump

Press any letter key (**A** – **Z**) to instantly jump to the first ROM whose name starts with that letter. A brief on-screen indicator displays the selected letter. This is especially useful for navigating large ROM collections quickly.

> **Note:** Keys that are already mapped to actions (**C**, **F**, **G**, **H**, **I**, **P**, **R**) keep their primary function instead of triggering a letter jump.

---

## Recently Played Quick Access Bar

Press **P** to toggle a horizontal bar at the top of the grid showing your most recently launched games (up to 10). Each mini-card displays the game's artwork (when available) and title. Click a card to select it, or double-click to launch immediately.

The bar automatically refreshes after every game launch. It only shows ROMs that exist in the currently loaded collection.

---

## Screensaver / Attract Mode

When enabled, the screensaver activates after a configurable period of inactivity (default: 5 minutes). It displays a fullscreen slideshow of random game artwork from your collection, cycling every 5 seconds.

- Press **any key** or **any gamepad button** to dismiss the screensaver and return to browsing.
- Configure the timeout in **Settings → Big Picture Mode → Screensaver timeout**. Set to **0** to disable.

---

## Collection Statistics Overlay

Press **C** (or **LT** on a controller) to display a modal overlay showing statistics about the currently loaded ROM collection:

- **Total ROMs** — number of ROMs in the full collection
- **Filtered ROMs** — shown only when a filter is active (search, system, or favorites)
- **Favorites** — how many ROMs in the collection are marked as favorites
- **Total Size** / **Average Size** — combined and per-ROM file size
- **ROMs by System** — per-system breakdown, sorted by count (descending), with each system's total file size

Press **C** or **Escape** to dismiss.

> **Note:** The **C** key is reserved for the statistics overlay and will not trigger a letter jump to "C".

---

## Quick System Cycle

Press **[** or **]** to cycle backward / forward through the system filter dropdown. A brief on-screen indicator displays the newly selected system name (or "All Systems" when the filter is cleared). This is especially useful in Big Picture Mode where mouse access is inconvenient.

The filter cycles wrap around: pressing **]** past the last system returns to "All Systems", and pressing **[** past "All Systems" jumps to the last system.

---

## Auto-Scroll Gallery Mode

Press **G** (or **RT** on a controller) to start an auto-scroll gallery that automatically advances through the card grid every 3 seconds. This is useful for hands-free browsing when you don't know what to play.

- A status message in the bottom bar indicates gallery mode is active.
- Gallery mode wraps around to the beginning when the last card is reached.
- Press **G** again, or use any **navigation key** (arrow keys, Home, End, Page Up/Down, Escape), to stop the gallery.

> **Note:** The **G** key is reserved for gallery mode and will not trigger a letter jump to "G".

---

## Fullscreen Artwork Viewer

Press **V** to open a fullscreen artwork viewer for the selected ROM. The viewer displays the box art, screenshot, and title screen images one at a time, enlarged to fill the screen.

- Press **←** / **→** to cycle between artwork types.
- Press **V** or **Escape** to close the viewer.
- Gamepad: use **Left** / **Right** on the D-Pad to cycle, **B** to dismiss.

> **Note:** The **V** key is reserved for the artwork viewer and will not trigger a letter jump to "V".

---

## User Game Ratings

Press **1** – **5** to rate the selected game from 1 to 5 stars. Pressing the same number again clears the rating.

- Ratings are displayed on the game cards as star characters (★★★☆☆).
- The detail panel shows the current rating.
- Sort by **Rating (High → Low)** to see your top-rated games first.
- Ratings are persisted across sessions.
- To disable ratings, uncheck **Enable user game ratings** in Settings.

---

## Play Timer / Session Tracking

Play time is automatically tracked whenever you launch a ROM via RetroArch or MAME and the app minimizes to tray. The elapsed time is recorded and accumulated per ROM.

- Play time is displayed in the detail panel (e.g. "2h 15m").
- The ROM Info overlay and Collection Statistics overlay also show play time information.
- Total play time for the entire collection is shown in the statistics overlay.
- To disable tracking, uncheck **Enable play count and session time tracking** in Settings.

---

## Detail Panel

Selecting a card opens a side panel showing:

- Game title, system badge, and favorite toggle button
- File size, ROM validity, play count, play time, and user rating
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
| **Card scale** | Zoom level for the card grid (0.5×–2.0×) | 1.0× |
| **Screensaver timeout** | Minutes of inactivity before the screensaver activates (0 = off) | 5 min |
| **Play count and session time tracking** | Track play count, session time, and recently-played history per ROM | Enabled |
| **User game ratings** | Allow rating games 1–5 stars via keyboard or controller | Enabled |
| **Game controller input** | Enable or disable native gamepad support (requires SDL2) | Enabled |
| **Controller dead zone** | Analog stick dead zone (0.05–0.95) | 0.25 |

These settings are in the **Settings** view under **Big Picture Mode**.

---

## Exiting Big Picture Mode

- Press **Escape** or **Backspace**
- Click **Exit Big Picture** in the toolbar
- Press the **Guide** button on a game controller

The application returns to the standard ROM Browser view.
