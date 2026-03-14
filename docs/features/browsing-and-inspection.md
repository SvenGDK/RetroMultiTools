# Browsing & Inspection

Tools for exploring your ROM collection, viewing ROM details, and inspecting file contents.

---

## ROM Browser

The ROM Browser lets you scan, browse, and manage your entire ROM collection from a single view.

### Scanning

- Click **Browse** to select a ROM directory.
- The scanner recursively searches subdirectories for files matching known ROM extensions across 32 supported systems.
- Results are displayed in a sortable list showing file name, system, and file size.

### Filtering

- Use the **System** dropdown to filter the list to a specific console or computer.
- The ROM count updates to show how many files match the active filter.

### Artwork

- Selecting a ROM displays box art, screenshots, and title screens fetched from the [Libretro Thumbnails](https://github.com/libretro-thumbnails) repository.
- Artwork is cached locally after the first download for each ROM.

### Organizing

- Click **Organize** to sort all scanned ROMs into system-specific folders (e.g., `NES/`, `SNES/`, `Sega Genesis/`).
- Choose an output directory and the organizer copies ROMs into the appropriate folders based on detected system type.
- A summary reports how many files were copied, skipped, or failed.

### Send to Remote

- Select one or more ROMs and click **Send to Remote** to transfer them to a remote target.
- Supported protocols: FTP (with FTPS), SFTP, WebDAV, and Amazon S3 / S3-compatible services.
- See [Remote Transfer Protocols](../reference/remote-transfer.md) for connection details.

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
