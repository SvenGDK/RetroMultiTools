using Avalonia.Controls;
using Avalonia.Threading;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class GamepadMapperWindow : Window
{
    // ── SDL2 mapping element names (standard order) ────────────────────
    private static readonly (string SdlName, string DisplayName)[] MappingElements =
    [
        ("a", "A Button"),
        ("b", "B Button"),
        ("x", "X Button"),
        ("y", "Y Button"),
        ("back", "Back / Select"),
        ("guide", "Guide / Home"),
        ("start", "Start"),
        ("leftstick", "Left Stick Click (L3)"),
        ("rightstick", "Right Stick Click (R3)"),
        ("leftshoulder", "Left Shoulder (LB)"),
        ("rightshoulder", "Right Shoulder (RB)"),
        ("dpup", "D-Pad Up"),
        ("dpdown", "D-Pad Down"),
        ("dpleft", "D-Pad Left"),
        ("dpright", "D-Pad Right"),
        ("leftx", "Left Stick X Axis"),
        ("lefty", "Left Stick Y Axis"),
        ("rightx", "Right Stick X Axis"),
        ("righty", "Right Stick Y Axis"),
        ("lefttrigger", "Left Trigger"),
        ("righttrigger", "Right Trigger"),
    ];

    // ── State ──────────────────────────────────────────────────────────

    private bool _sdlInitialised;
    private bool _gamepadServiceWasRunning;
    private int _selectedDeviceIndex = -1;
    private IntPtr _activeJoystick;

    // Mapping wizard state
    private bool _isMapping;
    private int _currentStep;
    private readonly Dictionary<string, string> _capturedMappings = new();
    private string _capturedGuid = string.Empty;
    private string _capturedName = string.Empty;

    // Input capture state (for detecting changes)
    private readonly Dictionary<int, short> _baselineAxes = new();
    private readonly Dictionary<int, byte> _baselineButtons = new();
    private readonly Dictionary<int, byte> _baselineHats = new();

    private DispatcherTimer? _pollTimer;

    /// <summary>Axis must exceed this fraction of full range to register as input.</summary>
    private const double AxisActivationThreshold = 0.65;

    public GamepadMapperWindow()
    {
        InitializeComponent();
        TryInitSdl();
        RefreshControllerList();
        RefreshCustomMappingsList();
    }

    // ── SDL2 lifecycle ─────────────────────────────────────────────────

    private void TryInitSdl()
    {
        try
        {
            SDL2Interop.RegisterResolver();

            // If GamepadService already initialised SDL2, reuse its session
            uint already = SDL2Interop.SDL_WasInit(
                SDL2Interop.SDL_INIT_JOYSTICK | SDL2Interop.SDL_INIT_GAMECONTROLLER);

            if (already != 0)
            {
                _sdlInitialised = true;
            }
            else
            {
                int result = SDL2Interop.SDL_Init(
                    SDL2Interop.SDL_INIT_JOYSTICK | SDL2Interop.SDL_INIT_GAMECONTROLLER);
                _sdlInitialised = result >= 0;

                if (!_sdlInitialised)
                {
                    MappingStatusText.Text = LocalizationManager.Instance["Gamepad_SdlInitFailed"];
                    return;
                }
            }

            // Load custom mappings so GameControllerAddMapping recognises them
            GamepadMappingStorage.ApplyAllToSdl();

            // Pause GamepadService polling to avoid event contention
            _gamepadServiceWasRunning = GamepadService.Instance.IsAvailable;
            if (_gamepadServiceWasRunning)
                GamepadService.Instance.Shutdown();
        }
        catch (DllNotFoundException)
        {
            MappingStatusText.Text = LocalizationManager.Instance["Gamepad_SdlNotFound"];
        }
        catch (EntryPointNotFoundException ex)
        {
            MappingStatusText.Text = string.Format(LocalizationManager.Instance["Gamepad_SdlEntryPointMissing"], ex.Message);
        }
    }

    // ── Controller list ────────────────────────────────────────────────

    private void RefreshControllerList()
    {
        ControllerCombo.Items.Clear();
        _selectedDeviceIndex = -1;
        StartMappingButton.IsEnabled = false;

        if (!_sdlInitialised)
        {
            ControllerInfoText.Text = LocalizationManager.Instance["Gamepad_SdlNotAvailable"];
            return;
        }

        // Pump events so SDL sees newly connected devices
        while (SDL2Interop.SDL_PollEvent(out _) != 0) { }

        int count = SDL2Interop.SDL_NumJoysticks();

        if (count == 0)
        {
            ControllerInfoText.Text = LocalizationManager.Instance["Settings_GamepadToolNoControllers"];
            return;
        }

        for (int i = 0; i < count; i++)
        {
            string name = SDL2Interop.JoystickNameForIndex(i) ?? string.Format(LocalizationManager.Instance["Gamepad_JoystickFallback"], i);
            bool isMapped = SDL2Interop.SDL_IsGameController(i);
            string label = isMapped ? $"{name} (mapped)" : $"{name} (unmapped)";
            ControllerCombo.Items.Add(new ComboBoxItem { Content = label, Tag = i });
        }

        if (ControllerCombo.Items.Count > 0)
            ControllerCombo.SelectedIndex = 0;
    }

    private void RefreshButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isMapping) return;
        CloseActiveJoystick();
        RefreshControllerList();
    }

    private void ControllerCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ControllerCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int idx)
        {
            _selectedDeviceIndex = -1;
            StartMappingButton.IsEnabled = false;
            return;
        }

        _selectedDeviceIndex = idx;
        StartMappingButton.IsEnabled = true;

        string guid = SDL2Interop.JoystickDeviceGUIDString(idx);
        string name = SDL2Interop.JoystickNameForIndex(idx) ?? LocalizationManager.Instance["Common_Unknown"];
        bool isMapped = SDL2Interop.SDL_IsGameController(idx);

        var loc = LocalizationManager.Instance;
        ControllerInfoText.Text = string.Format(loc["Gamepad_ControllerGuid"], guid) + "\n" +
                                  string.Format(loc["Gamepad_ControllerName"], name) + "\n" +
                                  (isMapped ? loc["Gamepad_HasMapping"]
                                            : loc["Gamepad_NoMapping"]);
    }

    // ── Mapping wizard ─────────────────────────────────────────────────

    private void StartMappingButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_sdlInitialised || _selectedDeviceIndex < 0)
        {
            MappingStatusText.Text = LocalizationManager.Instance["Gamepad_SelectController"];
            return;
        }

        // Open the joystick for raw input
        CloseActiveJoystick();
        _activeJoystick = SDL2Interop.SDL_JoystickOpen(_selectedDeviceIndex);
        if (_activeJoystick == IntPtr.Zero)
        {
            MappingStatusText.Text = LocalizationManager.Instance["Gamepad_FailedToOpen"];
            return;
        }

        _capturedGuid = SDL2Interop.JoystickDeviceGUIDString(_selectedDeviceIndex);
        _capturedName = SDL2Interop.JoystickNameForIndex(_selectedDeviceIndex) ?? LocalizationManager.Instance["Gamepad_UnknownController"];
        _capturedMappings.Clear();
        _currentStep = 0;

        _isMapping = true;
        StartMappingButton.IsVisible = false;
        SkipButton.IsVisible = true;
        CancelMappingButton.IsVisible = true;
        MappingPromptPanel.IsVisible = true;
        MappingResultPanel.IsVisible = false;
        RefreshButton.IsEnabled = false;
        ControllerCombo.IsEnabled = false;

        CaptureBaseline();
        ShowCurrentStep();
        StartPolling();
    }

    private void SkipButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_isMapping) return;
        AdvanceStep();
    }

    private void CancelMappingButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        StopMapping(cancelled: true);
    }

    private void ShowCurrentStep()
    {
        if (_currentStep >= MappingElements.Length)
        {
            FinishMapping();
            return;
        }

        var (sdlName, displayName) = MappingElements[_currentStep];
        MappingStepText.Text = string.Format(LocalizationManager.Instance["Gamepad_MappingStep"], _currentStep + 1, MappingElements.Length);
        MappingPromptText.Text = string.Format(
            LocalizationManager.Instance["Settings_GamepadToolPressPrompt"], displayName);
        MappingInputText.Text = $"({sdlName})";
        MappingStatusText.Text = string.Empty;
    }

    private void AdvanceStep()
    {
        _currentStep++;
        CaptureBaseline();
        ShowCurrentStep();
    }

    private void FinishMapping()
    {
        // Build SDL mapping string
        string platform = GamepadMappingStorage.GetCurrentPlatform();

        // Sanitize controller name — commas would break the SDL mapping format
        string safeName = _capturedName.Replace(',', ' ');
        var parts = new List<string> { _capturedGuid, safeName };

        foreach (var (sdlName, _) in MappingElements)
        {
            if (_capturedMappings.TryGetValue(sdlName, out string? value))
                parts.Add($"{sdlName}:{value}");
        }

        parts.Add($"platform:{platform}");
        string mapping = string.Join(",", parts);

        MappingResultText.Text = mapping;
        MappingResultPanel.IsVisible = true;

        StopMapping(cancelled: false);
        MappingStatusText.Text = string.Format(
            LocalizationManager.Instance["Settings_GamepadToolMappingComplete"],
            _capturedMappings.Count, MappingElements.Length);
    }

    private void StopMapping(bool cancelled)
    {
        _isMapping = false;
        StopPolling();
        CloseActiveJoystick();

        StartMappingButton.IsVisible = true;
        SkipButton.IsVisible = false;
        CancelMappingButton.IsVisible = false;
        MappingPromptPanel.IsVisible = false;
        RefreshButton.IsEnabled = true;
        ControllerCombo.IsEnabled = true;

        if (cancelled)
        {
            MappingStatusText.Text = LocalizationManager.Instance["Gamepad_MappingCancelled"];
        }
    }

    // ── Input polling during mapping ───────────────────────────────────

    private void StartPolling()
    {
        StopPolling();
        _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _pollTimer.Tick += PollForInput;
        _pollTimer.Start();
    }

    private void StopPolling()
    {
        if (_pollTimer != null)
        {
            _pollTimer.Stop();
            _pollTimer.Tick -= PollForInput;
            _pollTimer = null;
        }
    }

    private void CaptureBaseline()
    {
        if (_activeJoystick == IntPtr.Zero) return;

        // Pump pending events first
        while (SDL2Interop.SDL_PollEvent(out _) != 0) { }

        _baselineAxes.Clear();
        _baselineButtons.Clear();
        _baselineHats.Clear();

        int numAxes = SDL2Interop.SDL_JoystickNumAxes(_activeJoystick);
        for (int i = 0; i < numAxes; i++)
            _baselineAxes[i] = SDL2Interop.SDL_JoystickGetAxis(_activeJoystick, i);

        int numButtons = SDL2Interop.SDL_JoystickNumButtons(_activeJoystick);
        for (int i = 0; i < numButtons; i++)
            _baselineButtons[i] = SDL2Interop.SDL_JoystickGetButton(_activeJoystick, i);

        int numHats = SDL2Interop.SDL_JoystickNumHats(_activeJoystick);
        for (int i = 0; i < numHats; i++)
            _baselineHats[i] = SDL2Interop.SDL_JoystickGetHat(_activeJoystick, i);
    }

    private void PollForInput(object? sender, EventArgs e)
    {
        if (!_isMapping || _activeJoystick == IntPtr.Zero) return;

        // Pump SDL events
        while (SDL2Interop.SDL_PollEvent(out _) != 0) { }

        bool isAxisElement = _currentStep < MappingElements.Length &&
            MappingElements[_currentStep].SdlName is "leftx" or "lefty" or "rightx" or "righty"
                or "lefttrigger" or "righttrigger";

        // Check buttons
        int numButtons = SDL2Interop.SDL_JoystickNumButtons(_activeJoystick);
        for (int i = 0; i < numButtons; i++)
        {
            byte current = SDL2Interop.SDL_JoystickGetButton(_activeJoystick, i);
            byte baseline = _baselineButtons.GetValueOrDefault(i, (byte)0);

            if (current == 1 && baseline == 0)
            {
                if (isAxisElement)
                    continue; // Don't accept buttons for axis elements

                RegisterInput($"b{i}");
                return;
            }
        }

        // Check axes
        int numAxes = SDL2Interop.SDL_JoystickNumAxes(_activeJoystick);
        for (int i = 0; i < numAxes; i++)
        {
            short current = SDL2Interop.SDL_JoystickGetAxis(_activeJoystick, i);
            short baseline = _baselineAxes.GetValueOrDefault(i, (short)0);

            double normalised = current / SDL2Interop.SDL_AXIS_MAX;
            double baselineNormalised = baseline / SDL2Interop.SDL_AXIS_MAX;

            if (Math.Abs(normalised) > AxisActivationThreshold &&
                Math.Abs(baselineNormalised) < AxisActivationThreshold)
            {
                if (isAxisElement)
                {
                    RegisterInput($"a{i}");
                }
                else
                {
                    // For button elements, an axis can be used with +/- prefix
                    string prefix = normalised > 0 ? "+" : "-";
                    RegisterInput($"{prefix}a{i}");
                }
                return;
            }
        }

        // Check hats
        int numHats = SDL2Interop.SDL_JoystickNumHats(_activeJoystick);
        for (int i = 0; i < numHats; i++)
        {
            byte current = SDL2Interop.SDL_JoystickGetHat(_activeJoystick, i);
            byte baseline = _baselineHats.GetValueOrDefault(i, SDL2Interop.SDL_HAT_CENTERED);

            if (current != SDL2Interop.SDL_HAT_CENTERED && baseline == SDL2Interop.SDL_HAT_CENTERED)
            {
                if (isAxisElement)
                    continue; // Don't accept hats for axis elements

                RegisterInput($"h{i}.{current}");
                return;
            }
        }
    }

    private void RegisterInput(string sdlInput)
    {
        if (_currentStep >= MappingElements.Length) return;

        var (sdlName, displayName) = MappingElements[_currentStep];
        _capturedMappings[sdlName] = sdlInput;

        MappingStatusText.Text = string.Format(LocalizationManager.Instance["Gamepad_MappingSuccess"], displayName, sdlInput);
        AdvanceStep();
    }

    // ── Save / Copy / Delete ───────────────────────────────────────────

    private void SaveMappingButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string? mapping = MappingResultText.Text;
        if (string.IsNullOrEmpty(mapping)) return;

        GamepadMappingStorage.SaveMapping(mapping);

        // Apply the mapping to the running SDL session
        try
        {
            SDL2Interop.SDL_GameControllerAddMapping(mapping);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[GamepadMapperWindow] Failed to apply mapping to SDL: {ex.Message}");
        }

        MappingStatusText.Text = LocalizationManager.Instance["Settings_GamepadToolSaved"];
        RefreshCustomMappingsList();
    }

    private async void CopyMappingButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string? text = MappingResultText.Text;
        if (string.IsNullOrEmpty(text)) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            try
            {
                await clipboard.SetTextAsync(text);
                MappingStatusText.Text = LocalizationManager.Instance["Gamepad_CopiedToClipboard"];
            }
            catch (Exception ex)
            {
                // Clipboard access can fail on some platforms (e.g. Wayland without focus)
                System.Diagnostics.Trace.WriteLine($"[GamepadMapper] Clipboard write failed: {ex.Message}");
            }
        }
    }

    private void DeleteMappingButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string guid }) return;

        string platform = GamepadMappingStorage.GetCurrentPlatform();
        if (GamepadMappingStorage.RemoveMapping(guid, platform))
        {
            MappingStatusText.Text = LocalizationManager.Instance["Gamepad_MappingRemoved"];
            RefreshCustomMappingsList();
        }
    }

    private void RefreshCustomMappingsList()
    {
        var mappings = GamepadMappingStorage.LoadMappings();

        if (mappings.Count == 0)
        {
            CustomMappingsInfoText.Text = LocalizationManager.Instance["Settings_GamepadToolNoCustomMappings"];
            CustomMappingsList.ItemsSource = null;
            return;
        }

        CustomMappingsInfoText.Text = string.Format(
            LocalizationManager.Instance["Settings_GamepadToolCustomMappingsCount"],
            mappings.Count);

        CustomMappingsList.ItemsSource = mappings.Select(m => new CustomMappingItem
        {
            Guid = GamepadMappingStorage.ExtractGuid(m),
            Name = GamepadMappingStorage.ExtractName(m),
            Details = $"GUID: {GamepadMappingStorage.ExtractGuid(m)} | Platform: {GamepadMappingStorage.ExtractPlatform(m)}"
        }).ToList();
    }

    // ── Cleanup ────────────────────────────────────────────────────────

    private void CloseActiveJoystick()
    {
        if (_activeJoystick != IntPtr.Zero)
        {
            SDL2Interop.SDL_JoystickClose(_activeJoystick);
            _activeJoystick = IntPtr.Zero;
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        StopPolling();
        CloseActiveJoystick();

        // Restart GamepadService polling only if it was running before we paused it
        if (_gamepadServiceWasRunning)
            GamepadService.Instance.Initialise();

        base.OnClosing(e);
    }

    // ── Display model ──────────────────────────────────────────────────

    private sealed class CustomMappingItem
    {
        public string Guid { get; set; } = "";
        public string Name { get; set; } = "";
        public string Details { get; set; } = "";
    }
}
