# Disc Tools

Burn disc images, create images from discs, and build ISO files — all cross-platform.

---

## Overview

The Disc Tools utility provides optical disc operations using native system tools:

- **Windows** — IMAPI2 COM interface via PowerShell
- **Linux** — cdrecord / wodim
- **macOS** — hdiutil / drutil

---

## Operations

### Burn Image to Disc

Write a disc image file (ISO, BIN, CUE, IMG, etc.) to a physical optical drive.

1. Select **Burn Image** as the operation.
2. Click **Refresh Drives** to detect available optical drives.
3. Choose the target drive from the dropdown.
4. Browse for the disc image file.
5. Set the write speed (Auto, 1×–48×).
6. Optionally enable **Verify after burn** to confirm data integrity.
7. Click **Burn** to start.

**Supported image formats:** `.iso`, `.bin`, `.cue`, `.img`, `.mdf`, `.nrg`, `.cdi`, `.gdi`, `.3do`, `.toc`

### Burn Files to Disc

Write files and folders to a data disc with a custom volume label.

1. Select **Burn Files** as the operation.
2. Select the target drive.
3. Browse for the files or folder to burn.
4. Enter a volume label (ISO 9660 compatible).
5. Choose the write speed.
6. Click **Burn** to start.

### Create Image from Disc

Read a physical disc and save it as an ISO image file.

1. Select **Image from Disc** as the operation.
2. Select the source drive containing the disc.
3. Choose an output file path.
4. Click **Create** to start the extraction.

### Create Image from Files

Build an ISO image from files and folders on your local system.

1. Select **Image from Files** as the operation.
2. Browse for the files or folder to include.
3. Choose an output file path.
4. Enter a volume label.
5. Click **Create** to start.

---

## Options

| Option | Description |
|---|---|
| **Write Speed** | Auto, or a fixed speed from 1× to 48× |
| **Verify after Burn** | Read back the disc after writing to verify data integrity |
| **Volume Label** | Name for the disc (ISO 9660 rules apply) |

---

## Notes

- Drive detection is platform-specific. Ensure your disc drive is connected and powered on.
- On Linux, `cdrecord` or `wodim` must be installed and accessible.
- On macOS, built-in `hdiutil` and `drutil` are used.
- The operation can be cancelled at any time using the **Cancel** button.
