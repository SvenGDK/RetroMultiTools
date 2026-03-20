# Cheats & Emulation

Tools for converting cheat codes and generating emulator configuration files.

---

## Cheat Code Converter

Decodes and encodes cheat codes for retro gaming systems. Enter a code to decode it into address, value, and compare-value components, or enter raw values to encode them back into a cheat code string.

### Supported Systems

| System | Code Format |
|---|---|
| NES | Game Genie (6 or 8 characters) |
| SNES | Game Genie (8 characters), Pro Action Replay |
| Game Boy / GBC | Game Genie (6 or 9 characters), Pro Action Replay, GameShark |
| GBA | GameShark / Action Replay (12 code types) |
| N64 | GameShark (9 code types) |
| Sega Genesis | Game Genie, Pro Action Replay |
| Sega Game Gear | Game Genie |
| Sega Master System | Pro Action Replay |
| Sega 32X | Pro Action Replay |
| Sega CD | Pro Action Replay |
| PC Engine | Raw address:value |
| Neo Geo Pocket | GameShark |
| Nintendo DS | Action Replay (16 code types) |
| Sega Saturn | Action Replay |
| Sega Dreamcast | CodeBreaker |
| Game Boy | GameShark |
| Neo Geo | Raw address:value |
| PlayStation | GameShark (18 code types) |

### N64 GameShark Code Types

| Type | Description |
|---|---|
| 80 | Write 8-bit value |
| 81 | Write 16-bit value |
| A0 | Write 8-bit uncached |
| A1 | Write 16-bit uncached |
| D0 | 8-bit equal activator |
| D1 | 16-bit equal activator |
| D2 | 8-bit not-equal activator |
| D3 | 16-bit not-equal activator |
| 50 | Repeat (serial) |

### GBA GameShark / Action Replay Code Types

Supports 12 code types including conditional writes, multi-line patches, master code enables, and slow-motion toggles.

---

## Emulator Config Generator

Generates ready-to-use configuration files for popular retro gaming emulators.

### Supported Emulators

| Emulator | Config Format | Focus |
|---|---|---|
| RetroArch | `.cfg` | Multi-system |
| Mesen | `.xml` | NES / SNES |
| Snes9x | `.cfg` | SNES |
| Project64 | `.cfg` | N64 |
| mGBA | `.ini` | Game Boy / GBA |
| Kega Fusion | `.ini` | Sega systems |
| Mednafen | `.cfg` | Multi-system |
| Stella | `.ini` | Atari 2600 |
| FCEUX | `.cfg` | NES |
| MAME | `.ini` | Arcade |

### Common Options

These settings are available for all emulators:

- **Fullscreen** — launch in fullscreen or windowed mode
- **VSync** — enable vertical sync
- **Smooth video** — bilinear or CRT-style filtering
- **Integer scaling** — pixel-perfect scaling
- **Shaders** — enable shader/filter support
- **Rewind** — enable rewind functionality
- **Audio volume** — 0–100%
- **Show FPS** — display frame rate
- **Auto save state** — save state on exit

### Directory Paths

All emulators support configuring:

- **ROM directory** — default ROM folder
- **Save directory** — save file location
- **Save state directory** — save state folder
- **Screenshot directory** — screenshot output folder

### Emulator-Specific Options

<details>
<summary><strong>RetroArch</strong></summary>

- Video driver (gl, glcore, vulkan, d3d11, d3d12)
- Menu driver (ozone, xmb, rgui, glui)
- Audio driver (auto, alsa, pulse, sdl2, wasapi, dsound, coreaudio)
- Input driver (auto, udev, sdl2, x, dinput, xinput)
- Run-ahead frames (0–8)
- Threaded video
- Audio sync
- Notifications
- Enable cheats
- Config save on exit
- Pause on unfocus

</details>

<details>
<summary><strong>Mesen</strong></summary>

- Region (Auto, NTSC, PAL, Dendy)
- Remove sprite limit
- Overclock
- Enable cheats

</details>

<details>
<summary><strong>Snes9x</strong></summary>

- Region (Auto, NTSC, PAL)
- Turbo speed multiplier
- SuperFX overclock
- Enable cheats
- Block invalid VRAM access
- Dynamic rate control

</details>

<details>
<summary><strong>Project64</strong></summary>

- CPU core (Recompiler, Interpreter)
- Counter factor (1–6)
- Enable cheats
- Display speed

</details>

<details>
<summary><strong>mGBA</strong></summary>

- Audio sync
- Use BIOS
- Fast-forward speed multiplier
- Enable cheats
- Frame skip (0–10)
- Allow opposing directions

</details>

<details>
<summary><strong>Kega Fusion</strong></summary>

- Region (Auto, USA, Europe, Japan)
- Perfect sync
- Enable cheats
- SRAM auto-save

</details>

<details>
<summary><strong>Mednafen</strong></summary>

- CD image memory cache
- Enable cheats
- Sound buffer size (8–200 ms)
- Video driver (opengl, sdl, softfb)
- Per-system settings for: PC Engine, Lynx, Neo Geo Pocket, SMS, Game Gear, Virtual Boy, NES, SNES, Game Boy, GBA, Mega Drive

</details>

<details>
<summary><strong>Stella</strong></summary>

- Palette (standard, z26, custom)
- Phosphor glow effect
- Enable cheats
- TV scanline effects (0–100)

</details>

<details>
<summary><strong>FCEUX</strong></summary>

- Region (NTSC, PAL, Dendy)
- Remove sprite limit
- New PPU
- Enable cheats
- Sound quality (0–2)
- Game Genie

</details>

<details>
<summary><strong>MAME</strong></summary>

- Skip game info screen
- Skip warnings screen
- Enable cheats
- Detailed path, video, audio, input, and state options

</details>

### Usage

1. Select the target emulator from the dropdown.
2. Configure common and emulator-specific options.
3. Optionally set ROM, save, state, and screenshot directories.
4. Click **Generate** to create the configuration file.
5. When RetroArch is selected, click **Export to RetroArch** to write the configuration directly to the detected RetroArch installation's `retroarch.cfg`.
