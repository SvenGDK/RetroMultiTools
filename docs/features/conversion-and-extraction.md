# Conversion & Extraction

Tools for converting ROM formats, extracting archives, and assembling split files.

---

## N64 Format Converter

Converts between the three N64 ROM byte orders.

| Format | Extension | Byte Order |
|---|---|---|
| Big Endian | `.z64` | Native (most common) |
| Little Endian | `.n64` | 32-bit byte-swapped |
| Byte-swapped | `.v64` | 16-bit byte-swapped |

### Usage

1. Select an N64 ROM file.
2. The source format is auto-detected from the first four bytes (magic bytes).
3. Choose the target format and click **Convert**.

---

## ROM Format Converter

Performs header manipulation and disc image conversions.

### Header Operations

| Operation | Description |
|---|---|
| **Add Copier Header** | Prepends a 512-byte header to a ROM file (for SNES, Genesis, etc.) |
| **Remove Copier Header** | Strips the first 512 bytes from a headered ROM |
| **Remove iNES Header** | Strips the 16-byte iNES header from NES ROMs |
| **Fix iNES Header** | Cleans dirty bytes in the iNES header (bytes 8–15 for non-NES 2.0) |

A copier header is detected when the file size is greater than 512 bytes and `(size % 1024) == 512`.

### Disc Image Conversions

| Conversion | Requires |
|---|---|
| **Convert to CHD** | `chdman` (part of MAME) must be on the system PATH |
| **Convert to RVZ** | `DolphinTool` (part of Dolphin Emulator) must be on the system PATH |

### Batch Mode

Select a directory to convert all matching files at once. The tool scans for files with known ROM/disc extensions and applies the selected conversion to each.

---

## Save File Converter

Converts save files between formats and adjusts byte ordering for cross-platform compatibility.

### Supported Extensions

`.sav`, `.srm`, `.eep`, `.fla`, `.sra`

### Auto-Detection

Save type is detected from file size:

| Size Range | Detected Type |
|---|---|
| 512 B | EEPROM (4 Kbit) |
| 2 KB | EEPROM (16 Kbit) |
| 8 – 32 KB | SRAM |
| 64 – 128 KB | Flash |

### Conversion Operations

| Operation | Description |
|---|---|
| **Swap Endian (16-bit)** | Reverses byte pairs — useful for N64 saves between emulators |
| **Swap Endian (32-bit)** | Reverses 4-byte groups |
| **Pad to Power of Two** | Pads with `0xFF` to the next power-of-two size for flash cart compatibility |
| **Trim Trailing Zeros** | Removes trailing `0x00` bytes |
| **Trim Trailing 0xFF** | Removes trailing `0xFF` padding |
| **SRM → SAV / SAV → SRM** | Renames and copies the save file between formats |

---

## Archive Manager

Unified archive tool for extracting ROMs from and creating archives. Supports **ZIP**, **RAR**, **7z**, and **GZip** for extraction, and **ZIP** for creation.

### Extraction — Single File

1. Select a ZIP, RAR, 7z, or GZip file.
2. The ROM contents are listed with compressed and uncompressed sizes.
3. Click **Extract ROMs** to extract to the chosen output directory.

### Extraction — Batch Mode

Select a directory containing archives. All ZIP, RAR, 7z, and GZip files are scanned and ROM files inside them are extracted.

### Creation — Single Archive

1. Select one or more ROM files.
2. Choose an output path for the new ZIP archive.
3. Click **Create Archive**.

### Creation — Batch Mode

1. Select a directory of ROM files.
2. Choose between **one archive per ROM file** or a **single archive** containing all ROMs.
3. Click **Create Archive**.

> **Note:** Archive creation supports ZIP format only. RAR is a proprietary format and 7z writing is not available in the current library.

---

## Split ROM Assembler

Reassembles split ROM files into a single file.

### Supported Split Patterns

- Numbered parts: `.001`, `.002`, `.003`, …
- Named parts: `.part1`, `.part2`, `.part3`, …
- ZIP split archives: `.z01`, `.z02`, `.z03`, …

### Usage

1. Select the **first** part file.
2. All remaining parts are auto-detected and listed with their individual sizes.
3. Click **Assemble** to merge the parts into one output file.

---

## ROM Decompressor

> **Note:** The ROM Decompressor has been merged into the **Archive Manager**. GZip decompression is now available as part of the Archive Manager's extraction workflow.

GZip-compressed ROM files (`.gz`) can be extracted using the Archive Manager, alongside ZIP, RAR, and 7z archives.
