# MAME Integration

Tools for auditing, rebuilding, and managing MAME ROM sets, CHD files, and audio samples.

---

## Path Configuration

Configure the standalone MAME executable in the **Settings** view.

| Action | Description |
|---|---|
| **Browse** | Manually select the MAME executable (on macOS you can select the `.app` bundle directly) |
| **Auto-Detect** | Automatically find MAME on the system PATH or common install locations |
| **Download** | Opens the official MAME download page in a browser |
| **Clear** | Removes the stored MAME path |

The configured path is stored in `settings.json` and persists across sessions.

### What MAME Enables

- **ROM Browser** — launch Arcade ROMs directly in standalone MAME via the **🕹️ MAME** toolbar button or **Launch with MAME** context menu item.
- **Big Picture Mode** — an additional **Launch with MAME** button appears in the detail panel when an Arcade ROM is selected.

### Auto-Detection Locations

- **Windows** — `%ProgramFiles%\MAME`, `C:\MAME`, Scoop installs
- **Linux** — `/usr/bin/mame`, `/usr/local/bin/mame`, `/usr/games/mame`, `/snap/bin/mame`, system PATH
- **macOS** — `/Applications/mame`, Homebrew (`/opt/homebrew/bin/mame`, `/usr/local/bin/mame`), system PATH

---

## MAME XML Database

All MAME tools share the same XML database. Export your MAME database with:

```bash
mame -listxml > mame.xml
```

Or use a Logiqx-format DAT file from sources such as [Progretto-SNAPS](https://www.progettosnaps.net/).

---

## ROM Set Auditor

Audits MAME ROM sets against a MAME XML database for completeness and correctness.

### What It Checks

- **Completeness** — all required ROMs for each machine are present in the ZIP archive.
- **CRC32 checksums** — each ROM file matches the expected checksum.
- **File sizes** — each ROM matches the expected byte count.
- **Optional ROMs** — identified and reported separately (not required for a "good" status).

### Status Categories

| Status | Meaning |
|---|---|
| **Good** | All required ROMs present with correct checksums |
| **Incomplete** | Some ROMs present but others missing |
| **Bad** | All ROMs missing or checksums do not match |

### Options

| Option | Description |
|---|---|
| **Search subdirectories recursively** | Scan ROM ZIPs in the selected directory and all its subdirectories |

### Additional Features

- Identifies **clone / parent** ROM relationships.
- Detects machines that are completely **missing** from the ROM directory.
- Detailed per-ROM status for each machine.

### Usage

1. Load a MAME XML database.
2. Select the directory containing your ROM set ZIPs.
3. Optionally enable **recursive search** to include subdirectories.
4. Click **Audit ROMs** to begin.

---

## CHD Verifier

Verify MAME CHD (Compressed Hunks of Data) file integrity.

### What It Checks

- Reads and validates CHD v3, v4, and v5 headers
- Reports SHA-1 and raw SHA-1 checksums, compression type, logical size, hunk size, and unit size
- Detects parent CHD dependencies
- Single file or batch directory verification

---

## CHD Converter

Convert disc images to or from CHD format.

### Compress to CHD

1. Select an input disc image (ISO, CUE/BIN, GDI, etc.)
2. Click **Compress to CHD** — the tool runs `chdman createcd` and reports progress.

### Decompress from CHD

1. Select a CHD file.
2. Click **Decompress** — the tool runs `chdman extractcd` to produce the original disc image.

> **Requires:** `chdman` from the MAME distribution must be available on your system PATH or in the application directory.

---

## DAT Editor

Edit MAME DAT files (Logiqx XML format) directly.

- Load and save DAT files
- Add, edit, and remove machine entries
- Edit ROM entries per machine (name, size, CRC32, SHA-1, MD5, status)

---

## ROM Set Rebuilder

Rebuild MAME ROM sets from scattered or loose ROM files (similar to CLRMamePro Rebuilder).

### How It Works

1. Load a MAME XML database.
2. Select the source directory (contains scattered ROM files or ZIPs to rebuild from).
3. Select the output directory for the rebuilt ROM sets.
4. Choose a rebuild mode:

| Mode | Description |
|---|---|
| **Split** | Clones reference parent ZIP — smaller total size |
| **Non-Merged** | Each ZIP is fully self-contained |
| **Merged** | Parent ZIP includes all clone ROMs |

5. Optionally enable **Rebuild only complete sets** to skip sets with missing files.
6. Choose whether to overwrite or skip existing ZIP files.
7. Click **Rebuild** to start.

The rebuilder indexes the source directory recursively by CRC32, supporting both loose files and files inside ZIP archives.

---

## Dir2Dat Creator

Create a DAT file from a directory of ROM files (similar to CLRMamePro Dir2Dat).

### How It Works

1. Select the source directory containing ROM ZIPs.
2. Optionally enable **Include loose files** to include non-ZIP ROMs.
3. Set DAT metadata (name, description, author).
4. Click **Create** to generate the DAT.

### Output

- Exports in Logiqx XML format compatible with CLRMamePro and other ROM managers
- Computes CRC32, SHA-1, and MD5 checksums
- Reads CHD file headers for disk entries

---

## Sample Auditor

Audit MAME sample audio files against a MAME XML database.

### What It Checks

- Verifies that sample ZIP archives contain the expected WAV files for each machine
- Reports good, incomplete, and bad sample sets with missing file details
- Handles shared sample sets (`sampleof` attribute)
- Detects missing sample sets
- Optional recursive subdirectory search
