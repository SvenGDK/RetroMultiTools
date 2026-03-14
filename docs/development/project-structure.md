# Project Structure

Overview of the repository layout and code architecture.

---

## Repository Layout

```
RetroMultiTools/
├── docs/                          # Documentation
│   ├── features/                  # Feature documentation
│   ├── reference/                 # Reference guides
│   └── development/               # Developer documentation
├── RetroMultiTools/               # Application project
│   ├── Detection/                 # ROM system detection
│   ├── Localization/              # Language management
│   ├── Models/                    # Data models
│   ├── Resources/                 # Localization strings and assets
│   ├── Services/                  # Application services
│   ├── Utilities/                 # Core feature implementations
│   └── Views/                     # UI views (AXAML + code-behind)
├── RetroMultiTools.slnx           # Solution file
├── README.md                      # Project overview
├── LINUX.md                       # Linux installation guide
├── macOS.md                       # macOS installation guide
└── LICENSE                        # BSD 2-Clause License
```

---

## Architecture

Retro Multi Tools is a single-project **Avalonia UI** desktop application targeting **.NET 8**. It follows a straightforward view + utility architecture.

### Detection

`Detection/RomDetector.cs` — maps file extensions to `RomSystem` enum values. Used by the ROM Browser, ROM Inspector, and other tools to identify which system a ROM belongs to.

### Models

| File | Description |
|---|---|
| `RomSystem.cs` | Enum of all supported systems (NES, SNES, N64, Arcade, etc.) |
| `RomInfo.cs` | Data model for scanned ROM files (path, system, size) |
| `ArtworkInfo.cs` | Data model for artwork metadata (box art, screenshots) |
| `RemoteTarget.cs` | Connection configuration for remote file transfers |

### Services

| File | Description |
|---|---|
| `AppSettings.cs` | Singleton settings service — persists configuration to JSON |
| `ArtworkService.cs` | Fetches and caches artwork from the Libretro Thumbnails repository |

### Utilities

Each utility is a `static class` containing the core logic for a feature. Utilities:

- Accept input parameters and return results (or write to output files).
- Report progress via `IProgress<string>`.
- Support cancellation via `CancellationToken` where applicable.
- Use `ConfigureAwait(false)` on all `await` expressions (they run off the UI thread).
- Clean up partial output files on failure using a try-catch-delete pattern.

### Views

Each feature has a pair of files:

- `FeatureView.axaml` — XAML layout (Avalonia markup).
- `FeatureView.axaml.cs` — code-behind with event handlers.

Views handle:

- File/folder picker dialogs
- Calling utility methods with progress reporting
- Displaying results and error messages
- Specific exception handling (`IOException`, `UnauthorizedAccessException`, `InvalidOperationException`, etc.)

### Localization

| File | Description |
|---|---|
| `Localization/LocalizationManager.cs` | Manages the current culture and language switching |
| `Resources/Strings.resx` | English (default) resource strings |
| `Resources/Strings.{culture}.resx` | Translated strings for 19 additional languages |

All user-facing text uses resource keys from `Strings.resx`. The `LocalizationManager` switches the active culture at runtime.

### Main Window

`MainWindow.axaml` defines the sidebar navigation with 33 feature entries organized into categories (Browsing, Patching, Conversion, Verification, Management, Cheats, MAME, Settings). Clicking a menu item swaps the content area to the corresponding view.

---

## Key Patterns

### CRC32 Computation

All utilities that compute CRC32 use a **table-based** approach with a pre-generated lookup table (polynomial `0xEDB88320`). Each utility has its own private table to avoid cross-class dependencies.

### ROM Extension Sets

ROM extension collections for batch scanning use `static readonly HashSet<string>` with `StringComparer.OrdinalIgnoreCase` for O(1) case-insensitive lookup.

### Error Handling

- **Utility classes** — throw specific exceptions (`IOException`, `InvalidOperationException`, `InvalidDataException`, etc.).
- **View classes** — catch specific exception types and display user-friendly messages. Generic `catch (Exception)` is avoided.

### Output File Cleanup

All file-writing utility methods use a try-catch cleanup pattern:

```csharp
try
{
    await File.WriteAllTextAsync(outputPath, content).ConfigureAwait(false);
}
catch
{
    try { File.Delete(outputPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    throw;
}
```

This ensures partial output files are removed if an error occurs during writing.
