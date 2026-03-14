# Patching

Tools for applying and creating ROM patches.

---

## ROM Patcher

Applies IPS or BPS patches to ROM files.

### IPS Patches

- Applies standard **IPS** format patches (International Patching System).
- Supports **RLE-encoded** records for efficient patch data.
- Handles optional **truncation** records that resize the output file.
- The `EOF` marker is validated before patching.

### BPS Patches

- Applies **BPS** format patches (Beat Patching System).
- Full **CRC32 validation** of source ROM, target ROM, and patch data.
- Supports SourceRead, TargetRead, SourceCopy, and TargetCopy actions.
- If the source ROM CRC32 does not match the patch header, a warning is shown before proceeding.

### Usage

1. Select the **source ROM** (the unmodified file).
2. Select the **patch file** (`.ips` or `.bps`).
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
