# Verification & Analysis

Tools for verifying ROM integrity, detecting duplicates, and analyzing security features.

---

## Checksum Calculator

Computes cryptographic and CRC checksums for any ROM file.

### Supported Algorithms

| Algorithm | Output Length |
|---|---|
| CRC32 | 8 hex digits |
| MD5 | 32 hex digits |
| SHA-1 | 40 hex digits |
| SHA-256 | 64 hex digits |

- Uses streaming I/O, so large disc images (ISO, BIN) are handled without loading the entire file into memory.
- Click any hash value to copy it to the clipboard.

---

## ROM Comparer

Performs a byte-by-byte comparison of two ROM files.

### Output

- **Identical / Different** status
- Number of differing bytes
- Offset of the first mismatch
- File size comparison

Streaming comparison allows large files to be compared efficiently.

---

## DAT Verifier

Verifies ROM files against No-Intro and TOSEC DAT databases.

### Supported DAT Formats

- **CLRMAMEPro** / **Logiqx XML** format (`.dat` or `.xml`)

### Verification Methods

ROMs are matched by:

1. **CRC32** checksum (fastest)
2. **MD5** checksum
3. **SHA-1** checksum

### Modes

- **Single ROM** — verify one file against the loaded DAT.
- **Directory** — batch verify all ROMs in a directory. Unmatched files are flagged.

---

## DAT Filter

Filters DAT file entries to produce a curated subset, similar to Retool.

### Category Exclusion

Exclude entries tagged as:

- Demos
- Betas
- Prototypes
- Samples
- Unlicensed titles
- BIOS files
- Applications
- Pirate editions

### 1G1R (One Game, One ROM)

Deduplicates entries so only one version of each game remains, based on No-Intro / Redump naming conventions.

- **Region priority** — prefer USA → Europe → Japan (customizable order from 23 supported regions).
- **Language priority** — prefer En → Fr → De → Es → Ja (customizable from 21 supported languages).
- **Revision preference** — choose higher or lower revision numbers.
- "World" entries act as a universal fallback region.

### Export

Filtered results can be exported as a **Logiqx XML** DAT file compatible with CLRMamePro and other ROM managers.

---

## Dump Verifier

Checks ROM dump quality by analyzing file structure and content.

### Checks Performed

| Check | Description |
|---|---|
| **Size validation** | Compares file size against expected sizes for the detected system |
| **Power-of-two** | Verifies the file size is a power of two (common for cartridge ROMs) |
| **Overdump detection** | Checks for repeated data at the end of the file |
| **Underdump detection** | Flags files significantly smaller than expected |
| **Blank region analysis** | Detects large regions of `0x00` or `0xFF` |
| **Header validation** | Verifies system-specific header fields |

---

## Duplicate Finder

Scans directories recursively to identify duplicate ROM files.

### How It Works

1. Select a directory to scan.
2. CRC32 checksums are computed for every ROM file found.
3. Files with matching checksums are grouped together.
4. Results show duplicate groups, file paths, and total wasted disk space.

---

## Batch ROM Hasher

Computes checksums for every ROM in a directory.

### Algorithm Selection

Choose one or more algorithms to compute:

- CRC32
- MD5
- SHA-1
- SHA-256

### Export Formats

| Format | Description |
|---|---|
| **CSV** | Comma-separated values with one row per file |
| **Text report** | Human-readable summary |
| **SFV** | Standard SFV checksum file (CRC32 only) |
| **MD5 sum** | Standard MD5 checksum file |

---

## Security & DRM Analysis

Detects region locking, copy protection, and checksum integrity in ROM files.

### Region Locking

Region codes are extracted from headers for all supported systems.

### Copy Protection Detection

| Mechanism | Systems |
|---|---|
| 10NES / CIC lockout chips | NES |
| TMSS (TradeMark Security System) | Sega Genesis |
| Nintendo logo check | Game Boy, GBA |
| Lynx encryption header | Atari Lynx |
| Atari 7800 digital signature | Atari 7800 |
| ColecoVision BIOS check | ColecoVision |
| Intellivision EXEC handshake | Intellivision |
| Jaguar encrypted boot | Atari Jaguar |
| MSX cartridge marker | MSX |
| Sega CD security ring | Sega CD |

### Checksum Validation

| Checksum Type | Systems |
|---|---|
| Internal checksum | SNES |
| CRC validation | N64 |
| Header checksum | Game Boy, GBC, GBA |
| Internal checksum | Mega Drive / Genesis |
| iNES header validation | NES |

### Modes

- **Single ROM** — analyze one file.
- **Batch directory** — analyze all ROMs in a directory with a summary report.
