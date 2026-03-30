# USB Tools

Manage USB drives with a built-in file explorer, drive formatter, image writer, and image creator.

---

## Overview

The USB Tools utility provides cross-platform USB drive management using native system tools:

- **Windows** — diskpart / PowerShell
- **Linux** — udisks2 / dd
- **macOS** — diskutil / dd

---

## Features

### Drive Detection

Select a USB drive from the dropdown. Click **Refresh Drives** to scan for connected removable devices. Drives are listed with their device path, label, file system, and capacity.

---

### File Explorer

Browse and manage files on the selected USB drive.

| Action | Description |
|---|---|
| **Navigate** | Double-click folders to open them; click ↑ to go up one level |
| **Add Files** | Copy files from your computer to the current folder on the USB drive |
| **New Folder** | Create a new folder in the current directory |
| **Delete** | Delete the selected file or folder (with confirmation) |
| **Move** | Move the selected file or folder to a different location on the drive |

A context menu (right-click) provides the same actions for quick access.

---

### Format Drive

Erase and format the selected USB drive with a new file system.

| Option | Values |
|---|---|
| **File System** | FAT, FAT32, exFAT, NTFS |
| **Partition Scheme** | MBR, GPT |
| **Volume Label** | Custom name for the drive |

> ⚠️ **Warning:** Formatting permanently erases all data on the drive. A confirmation dialog is shown before proceeding.

**Note:** NTFS is not available on macOS.

---

### Write Image to USB

Write a disk image file directly to a USB drive. Useful for creating bootable drives, OS installers, or console-specific USB configurations.

1. Select the **Write Image** tab.
2. Browse for the image file.
3. Click **Write Image** to start. A confirmation dialog is shown first.

**Supported image formats:** `.iso`, `.img`, `.bin`, `.raw`, `.image`, `.dmg`, `.dd`, `.hdd`, `.wbfs`, `.xiso`

> ⚠️ **Warning:** Writing an image overwrites all existing data on the drive.

---

### Create Image from USB

Create a byte-for-byte disk image from a USB drive. Useful for backups and cloning.

1. Select the **Create Image** tab.
2. Choose an output file path.
3. Click **Create Image** to start. A confirmation dialog is shown first.

---

## Notes

- All destructive operations (format, write image, create image) require confirmation.
- Operations can be cancelled at any time using the **Cancel** button.
- On Linux, the user may need appropriate permissions for device access (e.g., being in the `disk` group or using `udisks2`).
- On macOS, drives are unmounted automatically before write operations.
