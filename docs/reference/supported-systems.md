# Supported Systems

Retro Multi Tools supports ROMs from the following 46 console and computer systems.

## System List

| System | Extensions | Header Parsing | Security Analysis | Dump Verification | Cheat Codes | Header Fixing |
|---|---|---|---|---|---|---|
| Amiga CD32 | `.iso`, `.cue` | ✔ | ✔ | ✔ | — | — |
| Amstrad CPC | `.dsk`, `.cdt`, `.sna` | ✔ | ✔ | ✔ | — | — |
| Arcade (MAME) | `.zip` | — | — | — | — | — |
| Atari 2600 | `.a26` | ✔ | ✔ | ✔ | — | — |
| Atari 5200 | `.a52` | ✔ | ✔ | ✔ | — | — |
| Atari 7800 | `.a78` | ✔ | ✔ | ✔ | — | ✔ |
| Atari 800 / XL / XE | `.atr`, `.xex`, `.car`, `.cas` | ✔ | ✔ | ✔ | — | — |
| Atari Jaguar | `.j64`, `.jag` | ✔ | ✔ | ✔ | — | ✔ |
| Atari Lynx | `.lnx`, `.lyx` | ✔ | ✔ | ✔ | — | ✔ |
| Coleco ColecoVision | `.col`, `.cv` | ✔ | ✔ | ✔ | — | ✔ |
| Fairchild Channel F | `.chf` | ✔ | ✔ | ✔ | — | — |
| Game Boy | `.gb` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Game Boy Advance | `.gba` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Game Boy Color | `.gbc` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Mattel Intellivision | `.int` | ✔ | ✔ | ✔ | — | ✔ |
| Memotech MTX | `.mtx`, `.run` | ✔ | ✔ | ✔ | — | — |
| MSX | `.mx1` | ✔ | ✔ | ✔ | — | ✔ |
| MSX2 | `.mx2` | ✔ | ✔ | ✔ | — | ✔ |
| NEC PC-88 | `.d88`, `.t88` | ✔ | ✔ | ✔ | — | — |
| Nintendo 3DS | `.3ds`, `.cia` | ✔ | ✔ | ✔ | — | — |
| Nintendo 64 | `.z64`, `.n64`, `.v64` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Nintendo 64DD | `.ndd` | ✔ | ✔ | ✔ | — | — |
| Nintendo DS | `.nds` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Nintendo Entertainment System (NES) | `.nes` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Nintendo GameCube | `.gcm`, `.iso` | ✔ | ✔ | ✔ | — | — |
| Nintendo Virtual Boy | `.vb`, `.vboy` | ✔ | ✔ | ✔ | — | ✔ |
| Nintendo Wii | `.iso` | ✔ | ✔ | ✔ | — | — |
| Oric / Atmos / TeleStrat | `.tap` | ✔ | ✔ | ✔ | — | — |
| Panasonic 3DO | `.3do`, `.iso`, `.cue` | ✔ | ✔ | ✔ | — | — |
| PC Engine / TurboGrafx-16 | `.pce`, `.tg16` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Philips CD-i | `.iso`, `.cue` | ✔ | ✔ | ✔ | — | — |
| Radio Shack Color Computer | `.ccc` | ✔ | ✔ | ✔ | — | — |
| Sega 32X | `.32x` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega CD | `.iso`, `.cue` | ✔ | ✔ | ✔ | ✔ | — |
| Sega Dreamcast | `.cdi`, `.gdi`, `.iso`, `.cue` | ✔ | ✔ | ✔ | ✔ | — |
| Sega Game Gear | `.gg` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega Master System | `.sms` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega Mega Drive / Genesis | `.md`, `.gen`, `.bin` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega Saturn | `.iso`, `.cue` | ✔ | ✔ | ✔ | ✔ | — |
| SNK Neo Geo | `.neo` | ✔ | ✔ | ✔ | ✔ | — |
| SNK Neo Geo CD | `.iso`, `.cue` | ✔ | ✔ | ✔ | — | — |
| SNK Neo Geo Pocket / Pocket Color | `.ngp`, `.ngc` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Super Nintendo (SNES) | `.smc`, `.sfc` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Thomson MO5 | `.mo5`, `.k7`, `.fd` | ✔ | ✔ | ✔ | — | — |
| Tiger Game Com | `.tgc` | ✔ | ✔ | ✔ | — | — |
| Watara Supervision | `.sv` | ✔ | ✔ | ✔ | — | ✔ |

## Feature Legend

| Symbol | Meaning |
|---|---|
| ✔ | Supported |
| — | Not applicable or not supported for this system |

## Extension Notes

- `.bin` is shared by Sega Genesis and other systems. The detector prefers Genesis when the internal header matches.
- `.iso` and `.cue` are shared by disc-based systems (Sega CD, 3DO, Amiga CD32, Sega Saturn, Sega Dreamcast, GameCube, Wii, Neo Geo CD, Philips CD-i). Content detection is used where possible.
- `.cdi` and `.gdi` are Sega Dreamcast disc image formats (DiscJuggler and GD-ROM respectively).
- `.d88` and `.t88` are NEC PC-88 disk and tape image formats respectively.
- `.ndd` is the Nintendo 64DD disk image format.
- `.neo` is a consolidated Neo Geo ROM format containing all ROM data in a single file.
- MAME arcade ROMs are ZIP-packaged and are handled by the dedicated MAME tools rather than the ROM Browser.
- `.chf` is the Fairchild Channel F cartridge ROM format.
- `.tgc` is the Tiger Game Com cartridge ROM format.
- `.mtx` and `.run` are Memotech MTX program formats.

## RetroArch Core Mapping

Each system is mapped to a default libretro core for RetroArch integration:

| System | Default Core |
|---|---|
| Amiga CD32 | `puae` |
| Amstrad CPC | `cap32` |
| Arcade | `mame2003_plus` |
| Atari 2600 | `stella2014` |
| Atari 5200 | `atari800` |
| Atari 7800 | `prosystem` |
| Atari 800 | `atari800` |
| Atari Jaguar | `virtualjaguar` |
| Atari Lynx | `handy` |
| ColecoVision | `bluemsx` |
| Color Computer | `xroar` |
| Fairchild Channel F | `freechaf` |
| Game Boy | `gambatte` |
| Game Boy Color | `gambatte` |
| GameCube | `dolphin` |
| GBA | `mgba` |
| Game Gear | `genesis_plus_gx` |
| Intellivision | `freeintv` |
| Master System | `genesis_plus_gx` |
| Mega Drive / Genesis | `genesis_plus_gx` |
| Memotech MTX | `mame2003_plus` |
| MSX / MSX2 | `bluemsx` |
| N64 | `mupen64plus_next` |
| N64DD | `mupen64plus_next` |
| NEC PC-88 | `quasi88` |
| Neo Geo | `geolith` |
| Neo Geo CD | `neocd` |
| Neo Geo Pocket | `mednafen_ngp` |
| NES | `fceumm` |
| Nintendo 3DS | `citra` |
| Nintendo DS | `melonds` |
| Panasonic 3DO | `opera` |
| PC Engine | `mednafen_pce_fast` |
| Philips CD-i | `same_cdi` |
| Sega 32X | `picodrive` |
| Sega CD | `genesis_plus_gx` |
| Sega Dreamcast | `flycast` |
| Sega Saturn | `mednafen_saturn` |
| SNES | `snes9x` |
| Thomson MO5 | `theodore` |
| Virtual Boy | `mednafen_vb` |
| Watara Supervision | `potator` |
| Wii | `dolphin` |

> **Note:** Oric / Atmos ROMs are fully supported for browsing, inspection, and analysis. However, there is no standard libretro core available, so launching via RetroArch is not supported for this system.

> **Note:** Tiger Game Com ROMs are fully supported for browsing, inspection, and analysis. However, there is no standard libretro core available, so launching via RetroArch is not supported for this system.

> **Note (Neo Geo AES/MVS):** The `geolith` core supports both Neo Geo AES (home console) and MVS (arcade) modes. Using the **"AES"** BIOS will start games in their console (home) versions. Using the **"NEOGEO"** (Universe BIOS or standard MVS) BIOS will boot games in their arcade versions.

## Standalone MAME

In addition to the RetroArch `mame2003_plus` core, Arcade ROMs can be launched with a standalone [MAME](https://www.mamedev.org/) installation. Configure the MAME executable path in **Settings → MAME Integration**.

| System | Standalone MAME Support |
|---|---|
| Arcade | ✔ Supported |

MAME is launched with `-rompath <directory> <romname>`, where `<romname>` is the ROM set name (ZIP file name without extension). All other systems continue to use RetroArch for launching.
