using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Markdown.Avalonia;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;

namespace RetroMultiTools;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;

            // Set up the native menu (macOS & supported Linux desktops)
            BuildNativeMenu();

            // Set up the system tray icon
            BuildTrayIcon();

            // Rebuild the native menu and tray icon when the language changes
            // (their labels are plain strings, not data-bound).
            // The Culture setter fires PropertyChanged twice ("" then "Item");
            // respond only to "Item" and post at Background priority so all
            // binding updates have settled before native menus are touched.
            // Native menu backends on macOS/Linux can crash when manipulated
            // during re-entrant event processing or active layout passes.
            LocalizationManager.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != "Item") return;
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        BuildNativeMenu();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[App] BuildNativeMenu failed after language change: {ex}");
                    }

                    try
                    {
                        RebuildTrayMenu();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[App] RebuildTrayMenu failed after language change: {ex}");
                    }
                }, DispatcherPriority.Background);
            };

            // Show/hide the tray icon when the window is minimized to or restored from the tray
            _mainWindow.IsMinimizedToTrayChanged += minimized =>
            {
                if (_trayIcon != null)
                    _trayIcon.IsVisible = minimized;
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                _trayIcon?.Dispose();
                _trayIcon = null;
            };

            // Clean up leftover files from a previous update cycle
            AppUpdater.CleanupAfterUpdate();

            // Auto-start Big Picture Mode if enabled in settings
            if (AppSettings.Instance.StartInBigPictureMode)
            {
                _mainWindow.EnterBigPictureMode(AppSettings.Instance.BigPictureRomFolder);
            }

            if (AppSettings.Instance.CheckForUpdatesOnStartup)
            {
                _ = CheckForUpdatesAsync(_mainWindow, desktop);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void BuildNativeMenu()
    {
        var loc = LocalizationManager.Instance;

        var nativeMenu = new NativeMenu();

        // File menu
        var fileMenu = new NativeMenuItem(loc["Menu_File"])
        {
            Menu = new NativeMenu()
        };
        var exitItem = new NativeMenuItem(loc["Menu_Exit"]);
        exitItem.Click += (_, _) => ShutdownApp();
        fileMenu.Menu!.Items.Add(exitItem);
        nativeMenu.Items.Add(fileMenu);

        // Browse & Inspect
        AddNavSubmenu(nativeMenu, loc["Nav_BrowseInspect"],
            ("browser", loc["Nav_RomBrowser"]),
            ("inspector", loc["Nav_RomInspector"]),
            ("hexviewer", loc["Nav_HexViewer"]));

        // Patching & Conversion
        AddNavSubmenu(nativeMenu, loc["Nav_PatchingConversion"],
            ("formatconv", loc["Nav_FormatConvert"]),
            ("n64conv", loc["Nav_N64Converter"]),
            ("patchcreator", loc["Nav_PatchCreator"]),
            ("patcher", loc["Nav_RomPatcher"]),
            ("saveconv", loc["Nav_SaveConverter"]),
            ("splitrom", loc["Nav_SplitAssembler"]));

        // Analysis & Verification
        AddNavSubmenu(nativeMenu, loc["Nav_AnalysisVerification"],
            ("batchhash", loc["Nav_BatchHasher"]),
            ("checksum", loc["Nav_ChecksumCalc"]),
            ("datfilter", loc["Nav_DatFilter"]),
            ("datverifier", loc["Nav_DatVerifier"]),
            ("dumpverifier", loc["Nav_DumpVerifier"]),
            ("duplicates", loc["Nav_DuplicateFinder"]),
            ("goodtools", loc["Nav_GoodToolsIdentifier"]),
            ("comparer", loc["Nav_RomComparer"]),
            ("security", loc["Nav_SecurityAnalysis"]));

        // Headers & Trimming
        AddNavSubmenu(nativeMenu, loc["Nav_HeadersTrimming"],
            ("export", loc["Nav_HeaderExport"]),
            ("headerfixer", loc["Nav_HeaderFixer"]),
            ("trimmer", loc["Nav_RomTrimmer"]),
            ("snesheader", loc["Nav_SnesHeader"]));

        // Utilities
        AddNavSubmenu(nativeMenu, loc["Nav_Utilities"],
            ("archives", loc["Nav_Archives"]),
            ("cheatcodes", loc["Nav_CheatCodes"]),
            ("emuconfig", loc["Nav_EmulatorConfig"]),
            ("metascraper", loc["Nav_MetadataScraper"]),
            ("romorganizer", loc["Nav_RomOrganizer"]),
            ("romrenamer", loc["Nav_RomRenamer"]));

        // RetroArch
        AddNavSubmenu(nativeMenu, loc["Nav_RetroArch"],
            ("raachievements", loc["Nav_RetroAchievementsWriter"]),
            ("raplaylist", loc["Nav_RetroArchPlaylist"]),
            ("raintegration", loc["Nav_RetroArchIntegration"]),
            ("rashortcut", loc["Nav_RetroArchShortcut"]));

        // MAME
        AddNavSubmenu(nativeMenu, loc["Nav_Mame"],
            ("mamechdconv", loc["Nav_MameChdConv"]),
            ("mamechd", loc["Nav_MameChd"]),
            ("mamedateditor", loc["Nav_MameDatEditor"]),
            ("mamedir2dat", loc["Nav_MameDir2Dat"]),
            ("mameintegration", loc["Nav_MameIntegration"]),
            ("mameauditor", loc["Nav_MameAuditor"]),
            ("mamerebuilder", loc["Nav_MameRebuilder"]),
            ("mamesamples", loc["Nav_MameSamples"]));

        // Analogue
        AddNavSubmenu(nativeMenu, loc["Nav_Analogue"],
            ("analogue3d", loc["Nav_Analogue3D"]),
            ("analoguemegasg", loc["Nav_AnalogueMegaSg"]),
            ("analoguentsupernt", loc["Nav_AnalogueNtSuperNt"]),
            ("analoguepocket", loc["Nav_AnaloguePocket"]));

        // Help menu with Settings
        var helpMenu = new NativeMenuItem(loc["Menu_Help"])
        {
            Menu = new NativeMenu()
        };
        var settingsItem = new NativeMenuItem(loc["Nav_Settings"]);
        settingsItem.Click += (_, _) =>
        {
            _mainWindow?.RestoreFromTray();
            _mainWindow?.NavigateToView("settings");
        };
        helpMenu.Menu!.Items.Add(settingsItem);
        nativeMenu.Items.Add(helpMenu);

        NativeMenu.SetMenu(this, nativeMenu);

        // On macOS the menu bar belongs to the frontmost window.
        // Setting the menu on the Window in addition to the Application
        // ensures the native menu bar is visible on macOS.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && _mainWindow != null)
        {
            NativeMenu.SetMenu(_mainWindow, nativeMenu);
        }
    }

    private void AddNavSubmenu(NativeMenu parentMenu, string header, params (string tag, string label)[] items)
    {
        var submenu = new NativeMenuItem(header)
        {
            Menu = new NativeMenu()
        };

        foreach (var (tag, label) in items)
        {
            var menuItem = new NativeMenuItem(label);
            menuItem.Click += (_, _) =>
            {
                _mainWindow?.RestoreFromTray();
                _mainWindow?.NavigateToView(tag);
            };
            submenu.Menu!.Items.Add(menuItem);
        }

        parentMenu.Items.Add(submenu);
    }

    private void ShutdownApp()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void BuildTrayIcon()
    {
        var loc = LocalizationManager.Instance;

        var trayMenu = new NativeMenu();

        var showItem = new NativeMenuItem(loc["Tray_Show"]);
        showItem.Click += (_, _) => _mainWindow?.RestoreFromTray();
        trayMenu.Items.Add(showItem);

        trayMenu.Items.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem(loc["Tray_Exit"]);
        exitItem.Click += (_, _) => ShutdownApp();
        trayMenu.Items.Add(exitItem);

        using var iconStream = Avalonia.Platform.AssetLoader.Open(
            new Uri("avares://RetroMultiTools/Assets/Icon.png"));

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(iconStream),
            ToolTipText = LocalizationManager.Instance["AppTitle"],
            Menu = trayMenu,
            IsVisible = false
        };

        _trayIcon.Clicked += (_, _) => _mainWindow?.RestoreFromTray();
    }

    /// <summary>
    /// Rebuilds the tray icon context menu with updated localized labels.
    /// Called when the application language changes at runtime.
    /// </summary>
    private void RebuildTrayMenu()
    {
        if (_trayIcon is null) return;

        var loc = LocalizationManager.Instance;

        var trayMenu = new NativeMenu();

        var showItem = new NativeMenuItem(loc["Tray_Show"]);
        showItem.Click += (_, _) => _mainWindow?.RestoreFromTray();
        trayMenu.Items.Add(showItem);

        trayMenu.Items.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem(loc["Tray_Exit"]);
        exitItem.Click += (_, _) => ShutdownApp();
        trayMenu.Items.Add(exitItem);

        _trayIcon.Menu = trayMenu;
    }

    private const int UpdateCheckDelayMs = 2000;

    private static async Task CheckForUpdatesAsync(Window owner, IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            // Small delay to let the window fully render first
            await System.Threading.Tasks.Task.Delay(UpdateCheckDelayMs);

            var update = await AppUpdater.CheckForUpdateAsync();
            if (update is null)
                return;

            var localization = Localization.LocalizationManager.Instance;
            string title = localization["Settings_UpdateAvailableTitle"];
            string message = string.Format(
                localization["Settings_UpdateAvailableMessage"],
                update.CurrentVersion, update.NewVersion);

            var dialog = new Window
            {
                Title = title,
                Width = 520,
                Height = string.IsNullOrWhiteSpace(update.ReleaseNotes) ? 220 : 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = Avalonia.Media.Brush.Parse("#1E1E2E")
            };

            var messageText = new TextBlock
            {
                Text = message,
                Foreground = Avalonia.Media.Brush.Parse("#CDD6F4"),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(16, 16, 16, 4),
                FontSize = 14
            };

            // Render release notes as Markdown when available
            Control? releaseNotesControl = null;
            if (!string.IsNullOrWhiteSpace(update.ReleaseNotes))
            {
                var mdViewer = new MarkdownScrollViewer
                {
                    Markdown = update.ReleaseNotes
                };
                releaseNotesControl = new Border
                {
                    Background = Avalonia.Media.Brush.Parse("#11161B"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8),
                    Margin = new Thickness(16, 4, 16, 4),
                    MaxHeight = 240,
                    Child = mdViewer
                };
            }

            var progressText = new TextBlock
            {
                Foreground = Avalonia.Media.Brush.Parse("#A6ADC8"),
                FontSize = 12,
                Margin = new Thickness(16, 0, 16, 4),
                IsVisible = false
            };

            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Margin = new Thickness(16, 0, 16, 8),
                IsVisible = false,
                Height = 6
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(16, 4, 16, 16)
            };

            bool canInstall = !string.IsNullOrEmpty(update.DownloadUrl);

            var updateButton = new Button
            {
                Content = canInstall
                    ? localization["Settings_UpdateInstallNow"]
                    : localization["Settings_UpdateDownload"],
                Padding = new Thickness(16, 8),
                Background = Avalonia.Media.Brush.Parse("#89B4FA"),
                Foreground = Avalonia.Media.Brush.Parse("#1E1E2E")
            };

            var laterButton = new Button
            {
                Content = localization["Settings_UpdateLater"],
                Padding = new Thickness(16, 8)
            };

            string laterButtonOriginalContent = localization["Settings_UpdateLater"];
            var cts = new CancellationTokenSource();

            // Cancel any in-progress download when the dialog is closed.
            dialog.Closed += async (_, _) =>
            {
                cts.Cancel();
                // Brief delay to let the download task observe the cancellation
                // before disposing the CTS, avoiding ObjectDisposedException.
                await Task.Delay(100);
                cts.Dispose();
            };

            updateButton.Click += async (_, _) =>
            {
                if (canInstall)
                {
                    // Download and install the update
                    updateButton.IsEnabled = false;
                    laterButton.Content = localization["Common_Cancel"];
                    progressText.IsVisible = true;
                    progressBar.IsVisible = true;

                    try
                    {
                        progressText.Text = localization["Settings_UpdateDownloading"];
                        var progress = new Progress<int>(p =>
                        {
                            progressBar.Value = p;
                            progressText.Text = string.Format(
                                localization["Settings_UpdateDownloadingPercent"], p);
                        });

                        string zipPath = await AppUpdater.DownloadUpdateAsync(
                            update.DownloadUrl!, progress, cts.Token);

                        progressText.Text = localization["Settings_UpdateInstalling"];

                        if (AppUpdater.LaunchUpdaterAndExit(zipPath))
                        {
                            desktop.Shutdown();
                        }
                        else
                        {
                            progressText.Text = localization["Settings_UpdateInstallError"];
                            updateButton.IsEnabled = true;
                            laterButton.Content = laterButtonOriginalContent;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Download was cancelled by the user — dialog is closing
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[App] Update install failed: {ex.Message}");
                        progressText.Text = localization["Settings_UpdateDownloadError"];
                        progressBar.IsVisible = false;
                        updateButton.IsEnabled = true;
                        laterButton.Content = laterButtonOriginalContent;
                    }
                }
                else
                {
                    // Fallback: open browser to release page
                    AppUpdater.OpenReleasePage(update.ReleaseUrl);
                    dialog.Close();
                }
            };

            laterButton.Click += (_, _) =>
            {
                cts.Cancel();
                dialog.Close();
            };

            buttonPanel.Children.Add(updateButton);
            buttonPanel.Children.Add(laterButton);

            var panel = new StackPanel();
            panel.Children.Add(messageText);
            if (releaseNotesControl != null)
                panel.Children.Add(releaseNotesControl);
            panel.Children.Add(progressText);
            panel.Children.Add(progressBar);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;
            await dialog.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            // Non-critical: log and continue if update check fails on startup
            System.Diagnostics.Trace.WriteLine($"[App] Startup update check failed: {ex.Message}");
        }
    }
}
