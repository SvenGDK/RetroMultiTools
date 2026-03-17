# Retro Multi Tools — Documentation

Welcome to the documentation for **Retro Multi Tools**, a cross-platform desktop utility for managing, inspecting, and patching retro game ROMs.

## Quick Start

1. Download a release from the [Releases](https://github.com/SvenGDK/RetroMultiTools/releases) page.
2. Install the [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (unless you chose a self-contained build).
3. Extract the archive and run the application.

Platform-specific guides: [Windows](../README.md#platform-guides) · [Linux](../LINUX.md) · [macOS](../macOS.md)

## Documentation Index

### Features

| Document | Description |
|---|---|
| [Browsing & Inspection](features/browsing-and-inspection.md) | ROM Browser (including Big Picture Mode and Host & Share), ROM Inspector, Hex Viewer |
| [ROM Browser & RetroArch Guide](features/rom-browser-guide.md) | Step-by-step guide to the ROM Browser and RetroArch integration |
| [Patching](features/patching.md) | ROM Patcher (IPS/BPS), IPS Patch Creator |
| [Conversion & Extraction](features/conversion-and-extraction.md) | N64 Converter, ROM Format Converter, Save File Converter, ZIP Extractor, Split ROM Assembler, ROM Decompressor |
| [Verification & Analysis](features/verification-and-analysis.md) | Checksum Calculator, ROM Comparer, DAT Verifier, DAT Filter, Dump Verifier, Duplicate Finder, Batch ROM Hasher, Security & DRM Analysis, GoodTools Identifier |
| [ROM Management](features/rom-management.md) | Header Export, SNES Header Tool, Batch Header Fixer, ROM Trimmer, ROM Renamer, Metadata Scraper |
| [Cheats & Emulation](features/cheats-and-emulation.md) | Cheat Code Converter, Emulator Config Generator |
| [MAME](features/mame.md) | ROM Set Auditor, CHD Verifier, ROM Set Rebuilder, Dir2Dat Creator, Sample Auditor |
| [Settings & Integration](features/settings-and-integration.md) | Native Menu, System Tray, Application Updates, RetroArch Core Downloader, SDL2 Gamepad Mapping Tool, Remote Transfer, Host & Share, Language settings |

### Reference

| Document | Description |
|---|---|
| [Supported Systems](reference/supported-systems.md) | Full list of supported consoles, computers, and file extensions |
| [Cheat Code Reference](reference/cheat-codes.md) | Supported cheat code formats with examples |
| [DAT File Formats](reference/dat-file-formats.md) | Supported DAT/XML formats for verification and filtering |
| [Emulator Configurations](reference/emulator-configurations.md) | Supported emulators, options, and generated config formats |
| [Remote Transfer Protocols](reference/remote-transfer.md) | FTP, SFTP, WebDAV, and S3 connection details |

### Development

| Document | Description |
|---|---|
| [Building from Source](development/building.md) | Build instructions and requirements |
| [Project Structure](development/project-structure.md) | Repository layout and architecture overview |
