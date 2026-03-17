# Browsing & Inspection

Tools for exploring your ROM collection, viewing ROM details, and inspecting file contents.

---

## ROM Browser

The ROM Browser lets you scan, browse, and manage your entire ROM collection from a single view.

### Scanning

- Click **Browse** to select a ROM directory.
- The scanner recursively searches subdirectories for files matching known ROM extensions across 46 supported systems.
- Results are displayed in a sortable list showing file name, system, and file size.

### Filtering

- Use the **System** dropdown to filter the list to a specific console or computer.
- The ROM count updates to show how many files match the active filter.

### Searching

- Type in the **Search** box to filter ROMs by name, system, or file size.
- Search results combine with the system filter for fine-grained browsing.

### Artwork

- Selecting a ROM displays box art, screenshots, and title screens fetched from the [Libretro Thumbnails](https://github.com/libretro-thumbnails) repository.
- Artwork is cached locally after the first download for each ROM, so subsequent views load instantly without a network request.

### Big Picture Mode

- Click **Big Picture Mode** in the toolbar to switch to a fullscreen, controller-friendly ROM library browser.
- Big Picture Mode displays ROMs as a card-based grid with color-coded system banners, box art thumbnails, game names, system labels, and file sizes.
- Use the system filter dropdown, search box, sort options, and **★ Favorites** toggle to organize the grid.
- Sort by name, system, size, or **Recently Played** to quickly find recently launched games.
- Mark ROMs as favorites with the **F** key or the favorite button — use the **★ Favorites** filter to show only favorites.
- A **ROM count badge** in the status bar shows how many ROMs are currently visible.
- Press **+** / **−** to **zoom in or out** on the card grid (50%–200%). The zoom level is saved between sessions.
- Press **H** or **?** to show a **keyboard shortcuts help overlay** listing all available shortcuts.
- Press **I** to show a **ROM Info overlay** with header details, checksums, and GoodTools codes for the selected ROM.
- Selecting a card opens a detail panel on the right showing the game title, system badge, favorite button, file info, **play count**, file path, and artwork (box art, screenshot, title screen).
- Click **Launch with RetroArch** in the detail panel to start the game with the appropriate libretro core. The **play count** is incremented with each launch.
- Use **Random Game** to jump to a random ROM (avoids re-selecting the same game), or **Rescan** to refresh the current folder.
- Keyboard navigation: arrow keys to browse cards, Enter/Space to launch, F to toggle favorite, I for ROM Info, +/− to zoom, H for help, Escape/Backspace to exit (Escape also defocuses the search box), Tab to focus the search box, Home/End to jump to the first/last card, PageUp/PageDown to scroll by rows.
- Press **Exit Big Picture** or Escape to return to the standard ROM Browser view.

### Organizing

- Click **Organize** to sort all scanned ROMs into system-specific folders (e.g., `NES/`, `SNES/`, `Sega Genesis/`).
- Choose an output directory and the organizer copies ROMs into the appropriate folders based on detected system type.
- A summary reports how many files were copied, skipped, or failed.

### Send to Remote

- Select one or more ROMs and click **Send to Remote** to transfer them to a remote target.
- Supported protocols: FTP (with FTPS), SFTP, WebDAV, Amazon S3 / S3-compatible services, Google Drive, Dropbox, and OneDrive.
- Cloud storage providers (Google Drive, Dropbox, OneDrive) use OAuth access tokens for authentication.
- See [Remote Transfer Protocols](../reference/remote-transfer.md) for connection details.

### Host & Share

- Click **Host & Share** to start a local HTTP server that makes your ROMs available to other devices on the same network.
- Two hosting modes are supported:
  - **Directory mode** — serves all ROM files in the currently browsed directory (when no ROMs are selected).
  - **Selected ROMs mode** — serves only the selected ROM(s) from the list.
- Configure the listening port (default: 8080) and click **Start**.
- The dialog displays one or more LAN URLs that you can share with others. Recipients open the URL in any web browser to browse and download the shared ROMs.
- A connection log shows incoming requests in real time.
- The server supports resumable downloads via HTTP range requests.
- Works cross-platform on Windows, Linux, and macOS with no additional dependencies.

---

## ROM Inspector

The ROM Inspector parses and displays detailed header information for individual ROM files.

### Detection

- System type is auto-detected from file extension and, where possible, from header magic bytes.

### Header Information

Parsed fields vary by system but typically include:

- **Game title** (internal name stored in the ROM header)
- **Region / country** codes
- **Mapper / banking** type (NES, SNES, Game Boy)
- **ROM size** and **RAM size**
- **Internal checksums** (where applicable)
- **Licensee / publisher** codes

### Artwork

- Box art, screenshots, and title screens are fetched from the Libretro Thumbnails repository, the same source used by the ROM Browser.

---

## Hex Viewer

The Hex Viewer displays raw ROM file contents in a standard hexadecimal + ASCII layout.

### Navigation

- Files are displayed in **pages** of 512 bytes. Use the **Previous** / **Next** buttons or page input to navigate.
- Enter a hex address in the **Go to offset** field to jump directly to a specific location.

### Search

- Enter a byte pattern (hex values) to search across the entire file.
- The viewer highlights and navigates to the first match.
