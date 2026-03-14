# Supported Systems

Retro Multi Tools supports ROMs from the following 32 console and computer systems.

## System List

| System | Extensions | Header Parsing | Security Analysis | Dump Verification | Cheat Codes | Header Fixing |
|---|---|---|---|---|---|---|
| Nintendo Entertainment System (NES) | `.nes` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Super Nintendo (SNES) | `.smc`, `.sfc` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Nintendo 64 | `.z64`, `.n64`, `.v64` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Game Boy | `.gb` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Game Boy Color | `.gbc` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Game Boy Advance | `.gba` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Nintendo Virtual Boy | `.vb`, `.vboy` | ✔ | ✔ | ✔ | — | ✔ |
| Sega Master System | `.sms` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega Mega Drive / Genesis | `.md`, `.gen`, `.bin` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega CD | `.iso`, `.cue` | ✔ | ✔ | ✔ | ✔ | — |
| Sega 32X | `.32x` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Sega Game Gear | `.gg` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Atari 2600 | `.a26` | ✔ | ✔ | ✔ | — | — |
| Atari 5200 | `.a52` | ✔ | ✔ | ✔ | — | — |
| Atari 7800 | `.a78` | ✔ | ✔ | ✔ | — | ✔ |
| Atari Jaguar | `.j64`, `.jag` | ✔ | ✔ | ✔ | — | ✔ |
| Atari Lynx | `.lnx`, `.lyx` | ✔ | ✔ | ✔ | — | ✔ |
| PC Engine / TurboGrafx-16 | `.pce`, `.tg16` | ✔ | ✔ | ✔ | ✔ | ✔ |
| SNK Neo Geo Pocket / Pocket Color | `.ngp`, `.ngc` | ✔ | ✔ | ✔ | ✔ | ✔ |
| Coleco ColecoVision | `.col`, `.cv` | ✔ | ✔ | ✔ | — | ✔ |
| Mattel Intellivision | `.int` | ✔ | ✔ | ✔ | — | — |
| MSX | `.mx1` | ✔ | ✔ | ✔ | — | ✔ |
| MSX2 | `.mx2` | ✔ | ✔ | ✔ | — | ✔ |
| Amstrad CPC | `.dsk`, `.cdt`, `.sna` | ✔ | ✔ | ✔ | — | — |
| Oric / Atmos / TeleStrat | `.tap` | ✔ | ✔ | ✔ | — | — |
| Thomson MO5 | `.mo5`, `.k7`, `.fd` | ✔ | ✔ | ✔ | — | — |
| Watara Supervision | `.sv` | ✔ | ✔ | ✔ | — | ✔ |
| Radio Shack Color Computer | `.ccc` | ✔ | ✔ | ✔ | — | — |
| Panasonic 3DO | `.3do`, `.iso`, `.cue` | ✔ | ✔ | ✔ | — | — |
| Amiga CD32 | `.iso`, `.cue` | ✔ | ✔ | ✔ | — | — |
| Sega Saturn | `.iso`, `.cue` | ✔ | ✔ | ✔ | — | — |
| Sega Dreamcast | `.cdi`, `.gdi`, `.iso`, `.cue` | ✔ | ✔ | ✔ | — | — |
| Nintendo GameCube | `.gcm`, `.iso` | ✔ | ✔ | ✔ | — | — |
| Nintendo Wii | `.iso` | ✔ | ✔ | ✔ | — | — |

## Feature Legend

| Symbol | Meaning |
|---|---|
| ✔ | Supported |
| — | Not applicable or not supported for this system |

## Extension Notes

- `.bin` is shared by Sega Genesis and other systems. The detector prefers Genesis when the internal header matches.
- `.iso` and `.cue` are shared by disc-based systems (Sega CD, 3DO, Amiga CD32, Sega Saturn, Sega Dreamcast, GameCube, Wii). Content detection is used where possible.
- `.cdi` and `.gdi` are Sega Dreamcast disc image formats (DiscJuggler and GD-ROM respectively).
- MAME arcade ROMs are ZIP-packaged and are handled by the dedicated MAME tools rather than the ROM Browser.

## RetroArch Core Mapping

Each system is mapped to a default libretro core for RetroArch integration:

| System | Default Core |
|---|---|
| NES | `fceumm` |
| SNES | `snes9x` |
| N64 | `mupen64plus_next` |
| Game Boy | `gambatte` |
| Game Boy Color | `gambatte` |
| GBA | `mgba` |
| Virtual Boy | `mednafen_vb` |
| Master System | `genesis_plus_gx` |
| Mega Drive / Genesis | `genesis_plus_gx` |
| Sega CD | `genesis_plus_gx` |
| Sega 32X | `picodrive` |
| Game Gear | `genesis_plus_gx` |
| Atari 2600 | `stella` |
| Atari 5200 | `atari800` |
| Atari 7800 | `prosystem` |
| Atari Jaguar | `virtualjaguar` |
| Atari Lynx | `handy` |
| PC Engine | `mednafen_pce` |
| Neo Geo Pocket | `mednafen_ngp` |
| ColecoVision | `bluemsx` |
| Intellivision | `freeintv` |
| MSX / MSX2 | `bluemsx` |
| Amstrad CPC | `cap32` |
| Thomson MO5 | `theodore` |
| Watara Supervision | `potator` |
| Arcade | `mame2003_plus` |
| Panasonic 3DO | `opera` |
| Amiga CD32 | `puae` |
| Sega Saturn | `mednafen_saturn` |
| Sega Dreamcast | `flycast` |
| GameCube | `dolphin` |
| Wii | `dolphin` |

> **Note:** Oric / Atmos does not have a standard libretro core. RetroArch launching is not available for this system.
