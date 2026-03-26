# Patching

Tools for applying and creating ROM patches.

---

## ROM Patcher

Applies IPS, BPS, or xDelta patches to ROM files.

### IPS Patches

- Applies standard **IPS** format patches (International Patching System).
- Supports **RLE-encoded** records for efficient patch data.
- Handles optional **truncation** records that resize the output file.
- The `EOF` marker is validated before patching.

### BPS Patches

- Applies **BPS** format patches (Beat Patching System).
- Full **CRC32 validation** of source ROM, target ROM, and patch data.
- Supports SourceRead, TargetRead, SourceCopy, and TargetCopy actions.
- If the source ROM CRC32 does not match the patch header, an error is raised indicating the wrong source file.

### xDelta Patches

- Applies **xDelta/VCDIFF** format patches (RFC 3284).
- Supports all standard VCDIFF instruction types: **ADD**, **RUN**, and **COPY**.
- Full **address cache** implementation (NEAR and SAME caches) for efficient COPY address decoding.
- **Adler-32 checksum** verification of target windows (xdelta3 extension).
- Supports multi-window patches and VCD_SOURCE/VCD_TARGET window modes.

### Usage

1. Select the **source ROM** (the unmodified file).
2. Select the **patch file** (`.ips`, `.bps`, `.xdelta`, or `.vcdiff`).
3. Choose an output path or let the tool generate one automatically.
4. Click **Apply Patch**.

---

## IPS Patch Creator

Creates IPS patches by comparing an original ROM with a modified version.

### How It Works

1. Select the **original ROM** and the **modified ROM**.
2. The tool analyzes both files and shows:
   - File sizes of each ROM
   - Number of differing bytes
   - Whether the difference fits the IPS format (max 16 MB)
3. Click **Create Patch** to generate the `.ips` file.

### Details

- Uses **RLE compression** for sequences of repeated byte values, producing smaller patches.
- Maximum supported file size is **16 MB** (IPS format limitation).
- If the modified ROM is shorter than the original, a truncation record is included so the patched output is the correct size.
