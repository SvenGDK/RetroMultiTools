using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
                CheckForUpdatesAsync(_mainWindow, desktop);
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
            ("patcher", loc["Nav_RomPatcher"]),
            ("n64conv", loc["Nav_N64Converter"]),
            ("formatconv", loc["Nav_FormatConvert"]),
            ("patchcreator", loc["Nav_PatchCreator"]),
            ("saveconv", loc["Nav_SaveConverter"]),
            ("zipextract", loc["Nav_ZipExtractor"]),
            ("splitrom", loc["Nav_SplitAssembler"]),
            ("decompress", loc["Nav_Decompressor"]));

        // Analysis & Verification
        AddNavSubmenu(nativeMenu, loc["Nav_AnalysisVerification"],
            ("checksum", loc["Nav_ChecksumCalc"]),
            ("comparer", loc["Nav_RomComparer"]),
            ("datverifier", loc["Nav_DatVerifier"]),
            ("datfilter", loc["Nav_DatFilter"]),
            ("dumpverifier", loc["Nav_DumpVerifier"]),
            ("duplicates", loc["Nav_DuplicateFinder"]),
            ("batchhash", loc["Nav_BatchHasher"]),
            ("security", loc["Nav_SecurityAnalysis"]),
            ("goodtools", loc["Nav_GoodToolsIdentifier"]));

        // Headers & Trimming
        AddNavSubmenu(nativeMenu, loc["Nav_HeadersTrimming"],
            ("export", loc["Nav_HeaderExport"]),
            ("snesheader", loc["Nav_SnesHeader"]),
            ("headerfixer", loc["Nav_HeaderFixer"]),
            ("trimmer", loc["Nav_RomTrimmer"]));

        // Utilities
        AddNavSubmenu(nativeMenu, loc["Nav_Utilities"],
            ("cheatcodes", loc["Nav_CheatCodes"]),
            ("emuconfig", loc["Nav_EmulatorConfig"]),
            ("metascraper", loc["Nav_MetadataScraper"]),
            ("romrenamer", loc["Nav_RomRenamer"]));

        // MAME
        AddNavSubmenu(nativeMenu, loc["Nav_Mame"],
            ("mameauditor", loc["Nav_MameAuditor"]),
            ("mamechd", loc["Nav_MameChd"]),
            ("mamerebuilder", loc["Nav_MameRebuilder"]),
            ("mamedir2dat", loc["Nav_MameDir2Dat"]),
            ("mamesamples", loc["Nav_MameSamples"]));

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
            ToolTipText = "Retro Multi Tools",
            Menu = trayMenu,
            IsVisible = false
        };

        _trayIcon.Clicked += (_, _) => _mainWindow?.RestoreFromTray();
    }

    private const int UpdateCheckDelayMs = 2000;

    private static async void CheckForUpdatesAsync(Window owner, IClassicDesktopStyleApplicationLifetime desktop)
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
                Width = 440,
                Height = 220,
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
            // The CTS is not disposed here because the async download may still
            // be processing the cancellation; the GC will finalize it.
            dialog.Closed += (_, _) => cts.Cancel();

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
