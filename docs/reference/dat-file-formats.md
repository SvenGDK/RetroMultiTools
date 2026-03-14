# DAT File Formats

Reference for the DAT file formats supported by the DAT Verifier and DAT Filter.

---

## Logiqx XML Format

The primary supported format is the **Logiqx XML** format used by CLRMamePro, RomVault, and other ROM management tools. Files typically have a `.dat` or `.xml` extension.

### Structure

```xml
<?xml version="1.0"?>
<!DOCTYPE datafile PUBLIC "-//Logiqx//DTD ROM Management Datafile//EN"
  "http://www.logiqx.com/Dats/datafile.dtd">
<datafile>
  <header>
    <name>Example DAT</name>
    <description>Example DAT description</description>
    <version>20240101</version>
    <author>Author Name</author>
    <homepage>https://example.com</homepage>
  </header>
  <game name="Game Title (Region)">
    <description>Game Title (Region)</description>
    <rom name="game.rom" size="262144" crc="AABBCCDD" md5="..." sha1="..." />
  </game>
</datafile>
```

### Key Elements

| Element | Description |
|---|---|
| `<header>` | Metadata about the DAT file itself |
| `<game>` | One entry per game/ROM set |
| `<rom>` | Individual ROM file with checksum attributes |
| `<disk>` | CHD disk image entry (used in MAME DATs) |

### ROM Attributes

| Attribute | Description |
|---|---|
| `name` | ROM file name |
| `size` | File size in bytes |
| `crc` | CRC32 checksum (8 hex digits) |
| `md5` | MD5 checksum (32 hex digits, optional) |
| `sha1` | SHA-1 checksum (40 hex digits, optional) |
| `status` | ROM status: `good`, `baddump`, or `nodump` |

---

## MAME XML Format

MAME's native XML output (from `mame -listxml`) is also supported by the MAME tools.

### Structure

```xml
<?xml version="1.0"?>
<mame build="0.264">
  <machine name="pacman" sourcefile="pacman/pacman.cpp" sampleof="pacman">
    <description>Pac-Man (Midway)</description>
    <rom name="pacman.6e" size="4096" crc="c1e6ab10" sha1="..." />
    <rom name="pacman.6f" size="4096" crc="1a6fb2d4" sha1="..." />
    <sample name="credit" />
    <disk name="disc1" sha1="..." />
  </machine>
</mame>
```

### Machine Attributes

| Attribute | Description |
|---|---|
| `name` | Machine short name (used as ZIP filename) |
| `cloneof` | Parent machine name (for clone sets) |
| `romof` | ROM parent name |
| `sampleof` | Parent machine for shared audio samples |

### ROM Element Attributes

| Attribute | Description |
|---|---|
| `name` | ROM file name inside the ZIP |
| `size` | File size in bytes |
| `crc` | CRC32 checksum |
| `sha1` | SHA-1 checksum |
| `merge` | Merge target (ROM is provided by parent) |
| `status` | `good`, `baddump`, or `nodump` |
| `optional` | Whether this ROM is optional (`yes`/`no`) |

---

## No-Intro DAT Files

[No-Intro](https://no-intro.org/) provides DAT files for cartridge-based systems using the Logiqx XML format. These DATs use standardized naming conventions:

```
Game Title (Region) (Revision)
```

Examples:
- `Super Mario Bros. (World)`
- `Legend of Zelda, The (USA) (Rev 1)`
- `Sonic the Hedgehog (Europe)`

---

## TOSEC DAT Files

[TOSEC](https://www.tosecdev.org/) DAT files also use the Logiqx XML format with a different naming convention:

```
Game Title (Year)(Publisher)(Region)(Language)
```

Both No-Intro and TOSEC DATs are supported for verification.

---

## DAT Sources

| Source | Focus | URL |
|---|---|---|
| No-Intro | Cartridge-based systems | https://no-intro.org/ |
| Redump | Disc-based systems | http://redump.org/ |
| TOSEC | Multi-platform preservation | https://www.tosecdev.org/ |
| MAME | Arcade machines | https://www.mamedev.org/ |
| Progretto-SNAPS | MAME extras and DATs | https://www.progettosnaps.net/ |
