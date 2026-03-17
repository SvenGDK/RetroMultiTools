# MAME

Tools for auditing, rebuilding, and managing MAME ROM sets, CHD files, and audio samples.

All MAME tools share the same XML database loading. Export your MAME database with:

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
4. Click **Audit** to scan all sets.

---

## CHD Verifier

Verifies the integrity of CHD (Compressed Hunks of Data) files used by MAME for disc images.

### Supported Versions

| Version | Fields Validated |
|---|---|
| **CHD v3** | SHA-1, MD5 (16-byte), logical size, hunk size |
| **CHD v4** | SHA-1, raw SHA-1, logical size, hunk size |
| **CHD v5** | SHA-1, raw SHA-1, logical size, hunk size, unit size, compression type |

### Output

- Header version and format details
- SHA-1 and raw SHA-1 checksums
- Compression type and parameters
- Logical size and hunk count
- Parent CHD dependency (if present)

### Modes

- **Single file** — verify one CHD file.
- **Batch directory** — verify all `.chd` files in a directory (searches recursively).

---

## ROM Set Rebuilder

Rebuilds MAME ROM sets from scattered or loose ROM files into properly structured ZIP archives, similar to CLRMamePro's Rebuilder.

### How It Works

1. **Index** — the source directory is scanned recursively and all files (loose and inside ZIPs) are indexed by CRC32.
2. **Match** — each machine's required ROMs are matched against the index.
3. **Build** — for each machine with at least one matching ROM, a new ZIP archive is created in the output directory.

### Rebuild Modes

| Mode | Description |
|---|---|
| **Split** (default) | Each machine ZIP contains only its own unique ROMs. Clones depend on their parent ZIP for shared ROMs. |
| **Non-Merged** | Each machine ZIP contains ALL ROMs it needs, including those shared with the parent. Every ZIP is self-contained. |
| **Merged** | Only parent machine ZIPs are built. The parent ZIP includes its own ROMs plus all unique ROMs from its clones. No separate clone ZIPs are created. |

### Options

| Option | Description |
|---|---|
| **Rebuild Mode** | Choose Split, Non-Merged, or Merged (see above) |
| **Only complete** | Skip machines where any required ROM is missing |
| **Overwrite existing** | Replace ZIP files that already exist in the output directory |

### Output

Each rebuilt set reports:

- Machine name and description
- Number of ROMs included
- Number of ROMs missing (for partial sets)
- Names of missing ROMs

---

## Dir2Dat Creator

Creates a DAT file from a directory of ROM files, similar to CLRMamePro's Dir2Dat.

### Scanning

The tool scans ZIP archives (and optionally loose files and CHD files) in the selected directory.

For each file found:

- CRC32 is always computed
- SHA-1 and MD5 are optional (configurable)
- CHD file headers are read for disk entries

### DAT Metadata

Configurable fields for the output DAT:

- **Name** — the DAT file name / title
- **Description** — free-text description
- **Author** — your name or organization

### Output Format

Logiqx XML format compatible with CLRMamePro, RomVault, and other ROM managers.

---

## Sample Auditor

Audits MAME audio sample files against a MAME XML database.

### What It Checks

- Each machine's required `<sample>` entries from the MAME XML are checked against the corresponding ZIP archive.
- Sample ZIPs are expected to contain `.wav` files matching the sample names.
- Shared sample sets (machines with a `sampleof` attribute) are resolved.

### Status Categories

| Status | Meaning |
|---|---|
| **Good** | All required samples present |
| **Incomplete** | Some samples present, others missing |
| **Bad** | No required samples found, or the ZIP is corrupt |
| **Unknown** | ZIP found but the machine is not in the database |

### Options

| Option | Description |
|---|---|
| **Search subdirectories recursively** | Scan sample ZIPs in the selected directory and all its subdirectories |

### Usage

1. Load a MAME XML database.
2. Select the directory containing your sample ZIPs.
3. Optionally enable **recursive search** to include subdirectories.
4. Click **Audit** to scan all sample sets.

### Output

- Per-set results with present and missing sample lists
- List of completely missing sample sets
- Summary counts (good, incomplete, bad, missing)
