# Analogue Hardware

Manage Analogue FPGA consoles: Pocket, Mega SG, NT / Super NT, and 3D.

---

## Analogue Pocket

Manage the Analogue Pocket SD card: browse cores, export screenshots, backup and restore saves, manage save states, extract Game Boy Camera photos, and generate library images.

### SD Card Setup

1. Click **Browse SD Card** and select the root of the Analogue Pocket SD card.
2. The tool validates the card by checking for expected directories (`Cores/`, `Platforms/`, `Assets/`).
3. Once loaded, all Pocket management features become available.

### Features

| Feature | Description |
|---|---|
| **Browse Cores** | Lists all installed openFPGA cores with platform, version, and size |
| **Export Screenshots** | Copies all screenshots from the Pocket to a chosen folder |
| **Backup Saves** | Backs up all save files to a destination folder, preserving the directory structure |
| **Restore Saves** | Restores saves from a backup folder to the Pocket SD card |
| **Manage Save States** | Lists save states with platform grouping; select and delete individual or batch states |
| **Export GB Camera Photos** | Extracts photos from a Game Boy Camera `.sav` file and exports as grayscale BMP images |
| **Auto-Copy Files** | Copies files from a source folder to the SD card, matching the directory structure |
| **Generate Library Images** | Creates placeholder 160×144 BMP images for ROMs without library art |
| **Open Game Folders** | Lists platform folders under `Assets/` for quick access in the file explorer |

---

## Analogue Mega SG

Generate custom fonts for the Analogue Mega SG on-screen menu and convert save files.

### Font Generator

1. Select the Mega SG SD card root.
2. Optionally select a 128×128 BMP source image (16×16 grid of 8×8 character tiles). If no image is provided, the built-in default font is used.
3. Click **Generate Font** to create the font binary and save it to the `System/` directory on the SD card.

### Save File Converter

1. Browse and select a save file (`.sav`, `.srm`, `.eep`, `.fla`, `.sra`).
2. The tool analyzes the file and displays its size, detected type (EEPROM, SRAM, or Flash), and power-of-two alignment status.
3. Select a conversion type:
   - **Swap Endian 16-bit** — swap bytes in 16-bit pairs
   - **Swap Endian 32-bit** — swap bytes in 32-bit groups
   - **Pad to Power of Two** — pad to the next power-of-two size for flash cart compatibility
   - **Trim Trailing 0x00** — remove trailing zero padding
   - **Trim Trailing 0xFF** — remove trailing 0xFF padding
   - **SRM → SAV** / **SAV → SRM** — convert between save formats
4. Click **Convert** to create the converted file alongside the original (with `_converted` suffix).

---

## Analogue NT / Super NT

Generate custom fonts for the Analogue NT and Super NT on-screen menus and repair NES ROM headers.

### Font Generator

1. Select the NT or Super NT SD card root and the target console (NT or Super NT).
2. Optionally select a 128×128 BMP source image. If none is provided, the built-in default font is used.
3. Click **Generate Font** to create the font binary on the SD card.

### NES Header Repair

1. Select a NES ROM file (`.nes`).
2. Click **Repair Header** to fix common header issues.
3. The repair uses a safe temp-file workflow: writes the fixed ROM to a temporary file first, then replaces the original only on success.

---

## Analogue 3D

Manage N64 Game Pak ROMs on the Analogue 3D SD card with per-game display and hardware settings.

### SD Card Setup

1. Click **Browse SD Card** and select the root of the Analogue 3D SD card.
2. The tool validates the card by checking for expected directories (`System/`, `N64/`).
3. Click **Scan Game Paks** to list all N64 ROM files on the card.

### Per-Game Settings

Click a Game Pak to open the settings editor:

**Display Settings:**

| Setting | Options |
|---|---|
| **Resolution** | Auto, 240p, 480i, 480p |
| **Aspect Ratio** | Auto, 4:3, 16:9 |
| **Smoothing** | Off, Low, Medium, High |
| **Crop Overscan** | On / Off |

**Hardware Settings:**

| Setting | Options |
|---|---|
| **Expansion Pak** | On / Off |
| **Rumble Pak** | On / Off |
| **CPU Overclock** | On / Off |
| **Controller Pak** | Auto, None, Memory |

Settings are saved atomically (writes to a temp file first, then renames) to prevent SD card corruption if the write is interrupted.

### Label Artwork

With a Game Pak selected, click **Set Artwork** to assign a custom PNG label artwork image for the game.
