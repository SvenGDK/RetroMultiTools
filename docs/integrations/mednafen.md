# Mednafen

Integration with the [Mednafen](https://mednafen.github.io/) multi-system emulator.

---

## Path Configuration

Configure the Mednafen executable path so ROMs can be launched directly from the ROM Browser and Big Picture Mode.

| Action | Description |
|---|---|
| **Browse** | Manually select the Mednafen executable |
| **Auto-Detect** | Searches common installation directories for each platform |
| **Download** | Opens the Mednafen releases page in a browser |

### Auto-Detection Locations

| Platform | Paths Searched |
|---|---|
| Windows | `C:\Program Files\Mednafen`, Scoop, Chocolatey |
| Linux | `/usr/bin/mednafen`, `/usr/local/bin/mednafen`, Snap (`/snap/mednafen/current/`) |
| macOS | `/Applications/Mednafen.app`, Homebrew (`/usr/local/bin/mednafen`, `/opt/homebrew/bin/mednafen`) |

macOS `.app` bundles are automatically resolved to the inner executable.

---

## Supported Systems

Mednafen supports launching ROMs for the following 13 systems. The correct Mednafen module is automatically selected based on the ROM's detected system type.

| System | Mednafen Module |
|---|---|
| Atari Lynx | `lynx` |
| Game Boy | `gb` |
| Game Boy Advance | `gba` |
| Game Boy Color | `gb` |
| Mega Drive / Genesis | `md` |
| Neo Geo Pocket | `ngp` |
| NES | `nes` |
| PC Engine / TurboGrafx-16 | `pce` |
| Sega Game Gear | `gg` |
| Sega Master System | `sms` |
| Sega Saturn | `ss` |
| SNES | `snes` |
| Virtual Boy | `vb` |

---

## Launching ROMs

ROMs can be launched with Mednafen from:

- **ROM Browser** — right-click a ROM and select **Launch with Mednafen**, or use the toolbar launch button.
- **Big Picture Mode** — select a ROM and launch from the detail panel.

Mednafen is invoked with the appropriate module flag to ensure the correct system emulation.

---

## Discord Rich Presence

When a ROM is launched via Mednafen, the application updates your Discord status with the game name and system. The status is cleared when the emulator exits.
