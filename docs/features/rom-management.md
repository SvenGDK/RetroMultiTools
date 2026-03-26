# ROM Management

Tools for exporting, fixing, trimming, renaming, and scraping ROM metadata.

---

## Header Export

Exports parsed ROM header information to external files.

### Export Formats

- **Text report** — human-readable summary for one or more ROMs.
- **CSV file** — tabular data with one row per ROM, suitable for spreadsheets.

### Batch Mode

Select a directory to export headers for all ROMs found inside it. The batch report includes a system-by-system summary.

---

## SNES Copier Header Tool

Manages the 512-byte copier header found in some SNES ROM dumps.

### Operations

| Operation | Description |
|---|---|
| **Detect** | Checks whether a copier header is present |
| **Add** | Prepends a 512-byte header to an unheadered ROM |
| **Remove** | Strips the first 512 bytes from a headered ROM |

Copier headers were added by hardware backup devices (Super Magicom, Super Wild Card, etc.) and can cause compatibility issues with some emulators and flash carts.

---

## Batch Header Fixer

Fixes or recalculates ROM headers for all supported files in a directory.

### Supported Fix Operations

| System | Fix Applied |
|---|---|
| SNES | Internal checksum recalculation |
| NES | iNES header cleanup (dirty bytes) |
| Game Boy / GBC | Header checksum and global checksum |
| GBA | Header checksum recalculation |
| Mega Drive / Genesis | Internal checksum recalculation |
| Sega 32X | Checksum recalculation |
| SMS / Game Gear | TMR SEGA checksum |
| N64 | CRC1/CRC2 checksum (CIC-NUS-6102) |
| Atari 7800 | Header validation |
| Atari Lynx | LYNX header cleanup |
| PC Engine | Copier header cleanup |
| Virtual Boy | Header validation |
| Neo Geo Pocket | Header validation |
| Atari Jaguar | Header validation |
| MSX | Cartridge header validation |
| ColecoVision | Header validation |
| Watara Supervision | Header validation |
| Nintendo DS | Header CRC16 recalculation |
| Intellivision | Header validation |

### Usage

1. Select a directory containing ROM files.
2. Click **Fix Headers**.
3. The tool processes each file and reports what was fixed or skipped.

---

## ROM Trimmer

Removes trailing padding bytes from ROM files to reduce file size.

### How It Works

1. Select a ROM file.
2. The tool analyzes trailing bytes (`0x00` and `0xFF` patterns).
3. The trimmed size is calculated, aligned to the nearest **power of two** to maintain compatibility.
4. Space savings are shown before you confirm the trim.

---

## ROM Renamer

Renames ROM files based on information parsed from their headers.

### Naming Components

The new filename is built from:

- Game title (internal name)
- Region code
- System identifier

### Modes

- **Single file** — rename one ROM.
- **Batch directory** — preview all proposed renames before applying them.

File names are sanitized for cross-platform compatibility (invalid characters are replaced).

---

## Metadata Scraper

Extracts metadata from ROM files in bulk and exports the results.

### Collected Metadata

- Header-parsed fields (title, region, mapper, sizes)
- File checksums (optional: CRC32, MD5, SHA-1, SHA-256)
- System type and file extension

### Export Formats

- **CSV** — tabular data with one row per ROM.
- **Text report** — human-readable summary.

---

## ROM Organizer

Automatically sorts ROMs into system-specific subfolders based on detected system type.

### How It Works

1. Select a **source directory** containing ROM files.
2. Select an **output directory** where system folders will be created.
3. Optionally filter to a specific system.
4. Choose **Copy** or **Move** mode.
5. Click **Organize** to begin.

### Features

- **System auto-detection** — each ROM is identified by its file extension and header via the ROM detector.
- **Copy or Move** — duplicate ROMs into organized folders while preserving originals, or relocate them.
- **System filter** — optionally organize only a specific platform.
- **Duplicate handling** — files that already exist at the destination are skipped (no overwrites).
- **Summary report** — shows processed, skipped, and failed file counts after completion.
