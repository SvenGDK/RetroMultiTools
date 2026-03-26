# Settings

Application settings for Retro Multi Tools: language, updates, system tray, and emulator paths.

---

## Language

The application supports 20 languages. Change the language from the **Settings** view using the dropdown. The change takes effect immediately — no restart required.

**Supported languages:** English, Spanish, French, German, Portuguese, Italian, Japanese, Chinese (Simplified), Korean, Russian, Dutch, Polish, Turkish, Arabic, Hindi, Thai, Swedish, Czech, Vietnamese, Indonesian

---

## Application Updates

The application can check for and install updates directly from GitHub Releases.

### How It Works

1. On startup (if enabled), the application checks the [GitHub Releases API](https://api.github.com/repos/SvenGDK/RetroMultiTools/releases/latest) for a newer version.
2. If an update is available and a platform-specific ZIP asset is found (e.g., `win-x64.zip`), the user is prompted to install it.
3. Clicking **Update Now** downloads the ZIP to a temporary directory with a progress bar.
4. After the download completes, the application launches the external **RetroMultiTools.Updater** process and shuts down.
5. The updater waits for the main application to fully exit, extracts the ZIP over the installation directory, and relaunches the application.

If no matching platform asset is found, the application offers to open the release page in a browser instead.

| Setting | Description | Default |
|---|---|---|
| **Check for updates on startup** | Automatically check for new versions when the application starts | Enabled |

Click **Check for Updates** in Settings to check manually.

### Update Flow

```
Main App: Check for update → Download ZIP → Launch Updater → Shutdown
Updater:  Wait for exit → Extract ZIP → Relaunch main app
```

### Updater Process

The updater (`RetroMultiTools.Updater`) is a separate console application bundled with the main app. It:

- Accepts `--pid`, `--zip`, `--target`, and `--exe` command-line arguments
- Waits up to 60 seconds for the main process to exit
- Extracts the update ZIP with path-traversal protection
- Skips replacing its own running executable (uses a `.new` → `.bak` swap strategy)
- Retries file operations on transient locks (antivirus, etc.)
- Logs to `%TEMP%/RetroMultiTools-Update/updater.log` for debugging
- Relaunches the main application after the update is applied

### Post-Update Cleanup

On the next launch after an update, the main application swaps any pending `.new` updater file into place, deletes leftover `.bak` files, and removes the update temp directory.

---

## Native Menu

On macOS and supported Linux desktop environments, a platform-native application menu is available in the menu bar.

| Menu | Items |
|---|---|
| **File** | Exit |
| **Browse & Inspect** | ROM Browser, ROM Inspector, Hex Viewer |
| **Patching & Conversion** | Format Converter, N64 Converter, Patch Creator, ROM Patcher, Save Converter, Split Assembler |
| **Analysis & Verification** | Batch Hasher, Checksum Calculator, DAT Filter, DAT Verifier, Dump Verifier, Duplicate Finder, GoodTools Identifier, ROM Comparer, Security Analysis |
| **Headers & Trimming** | Header Export, Header Fixer, ROM Trimmer, SNES Header Tool |
| **Utilities** | Archives, Cheat Codes, Emulator Config, Gamepad Key Mapper, Metadata Scraper, ROM Organizer, ROM Renamer |
| **RetroArch** | Achievements Writer, Playlist Utility, RetroArch Integration, Shortcut Creator |
| **MAME** | CHD Converter, CHD Verifier, DAT Editor, Dir2Dat Creator, MAME Integration, ROM Set Auditor, ROM Set Rebuilder, Sample Auditor |
| **Mednafen** | Mednafen Integration |
| **Analogue** | 3D, Mega SG, NT / Super NT, Pocket |
| **Help** | Settings |

---

## System Tray

The application includes a system tray icon that appears when the window is minimized to the tray.

| Action | Description |
|---|---|
| **Show** | Restores the main window from the system tray |
| **Exit** | Shuts down the application |

### Minimize to Tray on ROM Launch

When **Minimize to tray when launching a ROM** is enabled (the default), launching a ROM via RetroArch automatically:

1. Minimizes the main window to the system tray.
2. Shows the tray icon.
3. Waits for the RetroArch emulator process to exit.
4. Restores the main window and hides the tray icon.
5. Clears the Discord Rich Presence status.

| Setting | Description | Default |
|---|---|---|
| **Minimize to tray when launching a ROM** | Automatically minimize to the system tray when a ROM is launched via RetroArch | Enabled |

---

## Controller Profiles

Downloads the community-maintained SDL game controller database (`gamecontrollerdb.txt`) for automatic controller recognition in Big Picture Mode and RetroArch.

### How It Works

1. Click **Download / Update Profiles** in the Controller Profiles section of the Settings view.
2. The latest `gamecontrollerdb.txt` is downloaded from the [SDL_GameControllerDB](https://github.com/gabomdq/SDL_GameControllerDB) GitHub repository.
3. The file is saved next to the application executable so SDL2 loads it on startup.
4. If RetroArch is configured, a copy is also placed in the RetroArch `autoconfig/` directory.

The database contains mapping profiles for hundreds of game controllers (Xbox, PlayStation, Nintendo Switch Pro, 8BitDo, Logitech, and many more).

### Status Display

- Number of controller profiles currently installed
- Last updated date
- Or a note that no profiles are installed yet

---

## Gamepad Mapping Tool

Create custom SDL2 controller mappings for unrecognized or unmapped game controllers. Accessible from **Settings** → **Controller Profiles** → **Open Gamepad Tool**.

### How It Works

1. Connect a game controller.
2. Open the Gamepad Mapping Tool from the Settings view.
3. Select a controller from the dropdown.
4. Click **Start Mapping** to begin the mapping wizard.
5. For each button and axis (21 elements total), press the corresponding physical input.
6. Use **Skip** to skip an element if your controller does not have that input.
7. After completing all steps, the generated SDL2 mapping string is displayed.
8. Click **Save** to store the mapping permanently, or **Copy** to copy it to the clipboard.

### Mapping Elements

The wizard maps 21 standard SDL2 game controller elements:

| Element | Description |
|---|---|
| `a`, `b`, `x`, `y` | Face buttons |
| `back`, `guide`, `start` | Menu buttons |
| `leftstick`, `rightstick` | Stick click (L3/R3) |
| `leftshoulder`, `rightshoulder` | Shoulder buttons (LB/RB) |
| `dpup`, `dpdown`, `dpleft`, `dpright` | D-Pad directions |
| `leftx`, `lefty`, `rightx`, `righty` | Analog stick axes |
| `lefttrigger`, `righttrigger` | Analog triggers |

### Custom Mapping Storage

Custom mappings are stored in `{AppData}/RetroMultiTools/custom_mappings.txt`. Each mapping is a single line in standard SDL2 mapping string format. Mappings are applied at startup via `SDL_GameControllerAddMapping`.

### Requirements

SDL2 must be installed on the system. If SDL2 is not available, the tool displays a message and mapping is unavailable.

---

## Gamepad Key Mapper

Maps gamepad buttons and analog sticks to keyboard keys, mouse buttons, mouse movement, scripts, or macros. Located in the sidebar under **Utilities** → **Gamepad Key Mapper**.

> **Note:** This is a separate tool from the Gamepad Mapping Tool above. The Mapping Tool creates SDL2 controller definitions for unrecognized controllers. The Key Mapper translates recognized controller inputs into keyboard, mouse, and other actions.

### Action Types

| Type | Description |
|---|---|
| **Keyboard** | Simulates a keyboard key press |
| **Mouse button** | Simulates a mouse click (Left, Right, Middle, Back, Forward) |
| **Mouse movement** | Moves the mouse cursor (adjustable speed 1–20) |
| **Script** | Launches an executable or script with optional arguments |
| **Macro** | Executes a multi-step sequence of key presses with configurable delays |

### Supported Inputs

All 20 standard gamepad inputs are mappable: face buttons (A, B, X, Y), D-Pad (up, down, left, right), shoulder buttons (LB, RB), triggers (LT, RT), stick buttons (L3, R3), and analog stick directions (left stick up/down/left/right, right stick up/down/left/right).

### Profiles

- **Create, rename, and delete** profiles to organize mappings for different games or applications.
- **Export and import** profiles for sharing or backup.
- **Multiple mapping sets** per profile — switch between sets at runtime with a button press.

### Auto-Profiles

Automatically switch to a specific profile when a matching window becomes active.

| Match Type | Description |
|---|---|
| **Window title** | Partial, case-insensitive match against the active window title |
| **Process name** | Match against the active process name |

### Mapping Wizard

A step-by-step wizard guides you through creating mappings. Press each gamepad input in sequence to assign an action.

### Dead-Zone

Adjustable analog stick dead-zone (0.05–0.95, default 0.25) to prevent drift from triggering actions.
