using System.Diagnostics;
using System.Text.Json;
using Avalonia.Threading;
using RetroMultiTools.Services;

namespace RetroMultiTools.Utilities.GamepadKeyMapper;

/// <summary>
/// Core engine that reads gamepad input via SDL2 and executes the mapped
/// actions (keyboard/mouse simulation, scripts, macros).  Supports multiple
/// switchable mapping sets per profile and automatic profile switching
/// based on the active application window.
/// </summary>
public sealed class GamepadKeyMapperEngine : IDisposable
{
    // ── Singleton ───────────────────────────────────────────────────────

    private static readonly Lazy<GamepadKeyMapperEngine> _lazy = new(
        () => new GamepadKeyMapperEngine(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static GamepadKeyMapperEngine Instance => _lazy.Value;

    // ── State ───────────────────────────────────────────────────────────

    private GamepadKeyMapperConfig _config = new();
    private GamepadKeyMapperProfile? _activeProfile;
    private DispatcherTimer? _pollTimer;
    private DispatcherTimer? _autoProfileTimer;
    private bool _running;
    private bool _sdlOwned;         // true if we initialised SDL ourselves
    private IntPtr _controller = IntPtr.Zero;
    private IntPtr _joystick = IntPtr.Zero;
    private int _deviceIndex = -1;

    // Track held state for button release detection
    private readonly HashSet<GamepadInput> _heldInputs = [];

    // Axis dead-zone (same scale as GamepadService: 0.05–0.95, default 0.25)
    private double _deadZone = 0.25;

    // Cached enum values – avoids allocating a new array every poll tick (~60 Hz)
    private static readonly GamepadInput[] AllInputs = Enum.GetValues<GamepadInput>();

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RetroMultiTools");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "gamepad_keymapper.json");

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Events ──────────────────────────────────────────────────────────

    /// <summary>Raised when the active profile or set changes.</summary>
    public event Action<string>? ProfileChanged;

    /// <summary>Raised when the engine starts or stops.</summary>
    public event Action<bool>? RunningStateChanged;

    /// <summary>Raised when an input is detected (for the UI to highlight the button).</summary>
    public event Action<GamepadInput>? InputDetected;

    /// <summary>Raised when a controller is connected or disconnected while the engine is running.</summary>
    public event Action<bool>? ControllerStatusChanged;

    // ── Public properties ───────────────────────────────────────────────

    public bool IsRunning => _running;
    public bool IsControllerConnected => _controller != IntPtr.Zero || _joystick != IntPtr.Zero;
    public GamepadKeyMapperConfig Config => _config;

    public GamepadKeyMapperProfile? ActiveProfile
    {
        get => _activeProfile;
        private set
        {
            _activeProfile = value;
            ProfileChanged?.Invoke(value?.Name ?? string.Empty);
        }
    }

    public GamepadMappingSet? ActiveSet =>
        _activeProfile != null &&
        _activeProfile.ActiveSetIndex >= 0 &&
        _activeProfile.ActiveSetIndex < _activeProfile.Sets.Count
            ? _activeProfile.Sets[_activeProfile.ActiveSetIndex]
            : null;

    // ── Lifecycle ───────────────────────────────────────────────────────

    private GamepadKeyMapperEngine()
    {
        Directory.CreateDirectory(ConfigDir);
        LoadConfig();
    }

    /// <summary>
    /// Starts the mapping engine. If GamepadService is already running,
    /// we share its SDL2 session; otherwise we initialise SDL2 ourselves.
    /// </summary>
    public void Start()
    {
        if (_running) return;

        if (!TryInitSdl()) return;

        if (!TryOpenController()) return;

        _running = true;

        // Poll at ~60 Hz on the dispatcher thread
        _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _pollTimer.Tick += PollTimerTick;
        _pollTimer.Start();

        // Check active window every 2 seconds for auto-profile switching
        if (_config.AutoProfileRules.Count > 0)
        {
            _autoProfileTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _autoProfileTimer.Tick += AutoProfileTimerTick;
            _autoProfileTimer.Start();
        }

        // Activate last-used profile
        SwitchProfile(_config.LastActiveProfile);

        RunningStateChanged?.Invoke(true);
    }

    /// <summary>Stops the mapping engine and releases held keys.</summary>
    public void Stop()
    {
        if (!_running) return;

        if (_pollTimer != null)
        {
            _pollTimer.Stop();
            _pollTimer.Tick -= PollTimerTick;
            _pollTimer = null;
        }

        if (_autoProfileTimer != null)
        {
            _autoProfileTimer.Stop();
            _autoProfileTimer.Tick -= AutoProfileTimerTick;
            _autoProfileTimer = null;
        }

        // Release any held inputs
        ReleaseAllHeld();

        CloseController();

        if (_sdlOwned)
        {
            try
            {
                SDL2Interop.SDL_QuitSubSystem(
                    SDL2Interop.SDL_INIT_JOYSTICK | SDL2Interop.SDL_INIT_GAMECONTROLLER);
            }
            catch { }
            _sdlOwned = false;
        }

        _running = false;
        RunningStateChanged?.Invoke(false);
    }

    public void Dispose()
    {
        Stop();
    }

    // ── Profile management ──────────────────────────────────────────────

    public void SwitchProfile(string name)
    {
        var profile = _config.Profiles.Find(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (profile == null && _config.Profiles.Count > 0)
            profile = _config.Profiles[0];

        ReleaseAllHeld();
        ActiveProfile = profile;
        _config.LastActiveProfile = profile?.Name ?? string.Empty;
    }

    public void CycleSet(int direction = 1)
    {
        if (_activeProfile == null || _activeProfile.Sets.Count <= 1) return;

        ReleaseAllHeld();
        int count = _activeProfile.Sets.Count;
        _activeProfile.ActiveSetIndex =
            ((_activeProfile.ActiveSetIndex + direction) % count + count) % count;
        ProfileChanged?.Invoke(
            $"{_activeProfile.Name} – {_activeProfile.Sets[_activeProfile.ActiveSetIndex].Name}");
    }

    public void AddProfile(GamepadKeyMapperProfile profile)
    {
        _config.Profiles.Add(profile);
        SaveConfig();
    }

    public void RemoveProfile(string name)
    {
        _config.Profiles.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (_activeProfile?.Name.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
            SwitchProfile(_config.Profiles.Count > 0 ? _config.Profiles[0].Name : string.Empty);
        SaveConfig();
    }

    public void SetDeadZone(double value)
    {
        _deadZone = Math.Clamp(value, 0.05, 0.95);
        _config.DeadZone = _deadZone;
        SaveConfig();
    }

    /// <summary>Current dead-zone value (for UI display).</summary>
    public double DeadZone => _deadZone;

    /// <summary>
    /// Restarts or stops the auto-profile timer based on the current rule count.
    /// Call after adding or removing auto-profile rules while the engine is running.
    /// </summary>
    public void RefreshAutoProfileTimer()
    {
        if (!_running) return;

        if (_config.AutoProfileRules.Count > 0 && _autoProfileTimer == null)
        {
            _autoProfileTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _autoProfileTimer.Tick += AutoProfileTimerTick;
            _autoProfileTimer.Start();
        }
        else if (_config.AutoProfileRules.Count == 0 && _autoProfileTimer != null)
        {
            _autoProfileTimer.Stop();
            _autoProfileTimer.Tick -= AutoProfileTimerTick;
            _autoProfileTimer = null;
        }
    }

    // ── Config persistence ──────────────────────────────────────────────

    public void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                _config = JsonSerializer.Deserialize<GamepadKeyMapperConfig>(json, JsonOptions)
                          ?? new GamepadKeyMapperConfig();
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapperEngine] Failed to load config: {ex.Message}");
            _config = new GamepadKeyMapperConfig();
        }

        _deadZone = Math.Clamp(_config.DeadZone, 0.05, 0.95);
    }

    public void SaveConfig()
    {
        string tempPath = ConfigPath + ".tmp";
        try
        {
            string json = JsonSerializer.Serialize(_config, JsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, ConfigPath, overwrite: true);
        }
        catch (Exception ex)
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            Trace.WriteLine($"[GamepadKeyMapperEngine] Failed to save config: {ex.Message}");
        }
    }

    // ── Input polling ───────────────────────────────────────────────────

    private void PollTimerTick(object? sender, EventArgs e) => PollTick();
    private void AutoProfileTimerTick(object? sender, EventArgs e) => CheckAutoProfile();

    private void PollTick()
    {
        try
        {
            // Process SDL events to detect device add/remove
            while (SDL2Interop.SDL_PollEvent(out SDL2Interop.SDL_Event ev) != 0)
            {
                switch (ev.type)
                {
                    case SDL2Interop.SDL_CONTROLLERDEVICEREMOVED:
                    case SDL2Interop.SDL_JOYDEVICEREMOVED:
                        HandleDeviceRemoved();
                        break;

                    case SDL2Interop.SDL_CONTROLLERDEVICEADDED:
                    case SDL2Interop.SDL_JOYDEVICEADDED:
                        HandleDeviceAdded();
                        break;
                }
            }

            // If no controller is connected, skip input processing
            if (_controller == IntPtr.Zero && _joystick == IntPtr.Zero) return;

            if (_activeProfile == null) return;
            var set = ActiveSet;
            if (set == null) return;

            // Check each possible input
            foreach (GamepadInput input in AllInputs)
            {
                bool active = IsInputActive(input);

                if (active && !_heldInputs.Contains(input))
                {
                    // Newly pressed
                    _heldInputs.Add(input);
                    InputDetected?.Invoke(input);
                    ExecuteInputDown(input, set);
                }
                else if (!active && _heldInputs.Contains(input))
                {
                    // Released
                    _heldInputs.Remove(input);
                    ExecuteInputUp(input, set);
                }
                else if (active && _heldInputs.Contains(input))
                {
                    // Still held – for continuous actions (mouse move)
                    ExecuteInputHeld(input, set);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapperEngine] Poll error: {ex.Message}");
        }
    }

    /// <summary>Handles a controller/joystick removal event.</summary>
    private void HandleDeviceRemoved()
    {
        // Only act if we currently have a controller open
        if (_controller == IntPtr.Zero && _joystick == IntPtr.Zero) return;

        // Release held keys before closing
        ReleaseAllHeld();
        CloseController();

        Trace.WriteLine("[GamepadKeyMapperEngine] Controller disconnected.");
        ControllerStatusChanged?.Invoke(false);
    }

    /// <summary>Handles a controller/joystick added event – attempts to reconnect.</summary>
    private void HandleDeviceAdded()
    {
        // Only act if we don't already have a controller open
        if (_controller != IntPtr.Zero || _joystick != IntPtr.Zero) return;

        if (TryOpenController())
        {
            Trace.WriteLine("[GamepadKeyMapperEngine] Controller reconnected.");
            ControllerStatusChanged?.Invoke(true);
        }
    }

    private bool IsInputActive(GamepadInput input)
    {
        if (_controller != IntPtr.Zero)
            return IsControllerInputActive(input);
        if (_joystick != IntPtr.Zero)
            return IsFallbackInputActive(input);
        return false;
    }

    private bool IsControllerInputActive(GamepadInput input)
    {
        return input switch
        {
            GamepadInput.ButtonA => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_A) != 0,
            GamepadInput.ButtonB => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_B) != 0,
            GamepadInput.ButtonX => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_X) != 0,
            GamepadInput.ButtonY => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_Y) != 0,
            GamepadInput.ButtonBack => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_BACK) != 0,
            GamepadInput.ButtonGuide => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_GUIDE) != 0,
            GamepadInput.ButtonStart => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_START) != 0,
            GamepadInput.ButtonLeftStick => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_LEFTSTICK) != 0,
            GamepadInput.ButtonRightStick => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_RIGHTSTICK) != 0,
            GamepadInput.ButtonLeftShoulder => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_LEFTSHOULDER) != 0,
            GamepadInput.ButtonRightShoulder => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER) != 0,
            GamepadInput.DPadUp => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_DPAD_UP) != 0,
            GamepadInput.DPadDown => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_DPAD_DOWN) != 0,
            GamepadInput.DPadLeft => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_DPAD_LEFT) != 0,
            GamepadInput.DPadRight => SDL2Interop.SDL_GameControllerGetButton(_controller, SDL2Interop.SDL_CONTROLLER_BUTTON_DPAD_RIGHT) != 0,

            GamepadInput.LeftStickUp => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_LEFTY) < -_deadZone,
            GamepadInput.LeftStickDown => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_LEFTY) > _deadZone,
            GamepadInput.LeftStickLeft => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_LEFTX) < -_deadZone,
            GamepadInput.LeftStickRight => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_LEFTX) > _deadZone,
            GamepadInput.RightStickUp => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTY) < -_deadZone,
            GamepadInput.RightStickDown => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTY) > _deadZone,
            GamepadInput.RightStickLeft => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTX) < -_deadZone,
            GamepadInput.RightStickRight => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTX) > _deadZone,
            GamepadInput.LeftTrigger => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_TRIGGERLEFT) > _deadZone,
            GamepadInput.RightTrigger => NormalizedAxis(SDL2Interop.SDL_CONTROLLER_AXIS_TRIGGERRIGHT) > _deadZone,

            _ => false
        };
    }

    private double NormalizedAxis(int axis)
    {
        return SDL2Interop.SDL_GameControllerGetAxis(_controller, axis) / SDL2Interop.SDL_AXIS_MAX;
    }

    private bool IsFallbackInputActive(GamepadInput input)
    {
        // Map first 16 buttons directly, axes to stick directions
        return input switch
        {
            GamepadInput.ButtonA => FallbackButton(0),
            GamepadInput.ButtonB => FallbackButton(1),
            GamepadInput.ButtonX => FallbackButton(2),
            GamepadInput.ButtonY => FallbackButton(3),
            GamepadInput.ButtonLeftShoulder => FallbackButton(4),
            GamepadInput.ButtonRightShoulder => FallbackButton(5),
            GamepadInput.ButtonBack => FallbackButton(6),
            GamepadInput.ButtonStart => FallbackButton(7),
            GamepadInput.ButtonGuide => FallbackButton(8),
            GamepadInput.ButtonLeftStick => FallbackButton(9),
            GamepadInput.ButtonRightStick => FallbackButton(10),
            GamepadInput.DPadUp => FallbackHat(SDL2Interop.SDL_HAT_UP),
            GamepadInput.DPadDown => FallbackHat(SDL2Interop.SDL_HAT_DOWN),
            GamepadInput.DPadLeft => FallbackHat(SDL2Interop.SDL_HAT_LEFT),
            GamepadInput.DPadRight => FallbackHat(SDL2Interop.SDL_HAT_RIGHT),
            GamepadInput.LeftStickUp => FallbackAxis(1) < -_deadZone,
            GamepadInput.LeftStickDown => FallbackAxis(1) > _deadZone,
            GamepadInput.LeftStickLeft => FallbackAxis(0) < -_deadZone,
            GamepadInput.LeftStickRight => FallbackAxis(0) > _deadZone,
            GamepadInput.RightStickUp => FallbackAxis(3) < -_deadZone,
            GamepadInput.RightStickDown => FallbackAxis(3) > _deadZone,
            GamepadInput.RightStickLeft => FallbackAxis(2) < -_deadZone,
            GamepadInput.RightStickRight => FallbackAxis(2) > _deadZone,
            GamepadInput.LeftTrigger => FallbackAxis(4) > _deadZone,
            GamepadInput.RightTrigger => FallbackAxis(5) > _deadZone,
            _ => false
        };
    }

    private bool FallbackButton(int index)
    {
        return SDL2Interop.SDL_JoystickGetButton(_joystick, index) != 0;
    }

    private bool FallbackHat(byte direction)
    {
        byte hat = SDL2Interop.SDL_JoystickGetHat(_joystick, 0);
        return (hat & direction) != 0;
    }

    private double FallbackAxis(int index)
    {
        return SDL2Interop.SDL_JoystickGetAxis(_joystick, index) / SDL2Interop.SDL_AXIS_MAX;
    }

    // ── Action execution ────────────────────────────────────────────────

    private void ExecuteInputDown(GamepadInput input, GamepadMappingSet set)
    {
        var mapping = set.Mappings.Find(m => m.Input == input);
        if (mapping == null) return;

        ExecuteActionDown(mapping.Action);
    }

    private void ExecuteInputUp(GamepadInput input, GamepadMappingSet set)
    {
        var mapping = set.Mappings.Find(m => m.Input == input);
        if (mapping == null) return;

        ExecuteActionUp(mapping.Action);
    }

    private void ExecuteInputHeld(GamepadInput input, GamepadMappingSet set)
    {
        var mapping = set.Mappings.Find(m => m.Input == input);
        if (mapping == null) return;

        // Only continuous mouse-move actions execute while held
        if (mapping.Action is MouseMoveAction moveAction)
        {
            int dx = moveAction.DeltaX;
            int dy = moveAction.DeltaY;

            // Scale by analog stick deflection for proportional movement
            if (dx == 0 && dy == 0)
            {
                double axisX = GetAxisDeflection(input, horizontal: true);
                double axisY = GetAxisDeflection(input, horizontal: false);
                dx = (int)(axisX * moveAction.Speed);
                dy = (int)(axisY * moveAction.Speed);
            }

            if (dx != 0 || dy != 0)
                InputSimulator.MouseMoveRelative(dx, dy);
        }
    }

    private double GetAxisDeflection(GamepadInput input, bool horizontal)
    {
        if (_controller != IntPtr.Zero)
        {
            int axis = input switch
            {
                GamepadInput.LeftStickUp or GamepadInput.LeftStickDown =>
                    horizontal ? SDL2Interop.SDL_CONTROLLER_AXIS_LEFTX : SDL2Interop.SDL_CONTROLLER_AXIS_LEFTY,
                GamepadInput.LeftStickLeft or GamepadInput.LeftStickRight =>
                    horizontal ? SDL2Interop.SDL_CONTROLLER_AXIS_LEFTX : SDL2Interop.SDL_CONTROLLER_AXIS_LEFTY,
                GamepadInput.RightStickUp or GamepadInput.RightStickDown =>
                    horizontal ? SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTX : SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTY,
                GamepadInput.RightStickLeft or GamepadInput.RightStickRight =>
                    horizontal ? SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTX : SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTY,
                _ => -1
            };
            if (axis < 0) return 0;
            return SDL2Interop.SDL_GameControllerGetAxis(_controller, axis) / SDL2Interop.SDL_AXIS_MAX;
        }

        if (_joystick != IntPtr.Zero)
        {
            int axis = input switch
            {
                GamepadInput.LeftStickUp or GamepadInput.LeftStickDown =>
                    horizontal ? 0 : 1,
                GamepadInput.LeftStickLeft or GamepadInput.LeftStickRight =>
                    horizontal ? 0 : 1,
                GamepadInput.RightStickUp or GamepadInput.RightStickDown =>
                    horizontal ? 2 : 3,
                GamepadInput.RightStickLeft or GamepadInput.RightStickRight =>
                    horizontal ? 2 : 3,
                _ => -1
            };
            if (axis < 0) return 0;
            return SDL2Interop.SDL_JoystickGetAxis(_joystick, axis) / SDL2Interop.SDL_AXIS_MAX;
        }

        return 0;
    }

    private static void ExecuteActionDown(MappingAction action)
    {
        switch (action)
        {
            case KeyboardAction ka:
                InputSimulator.KeyDown(ka.KeyName);
                break;

            case MouseButtonAction mba:
                InputSimulator.MouseDown(mba.Button);
                break;

            case MouseMoveAction:
                // Handled by ExecuteInputHeld
                break;

            case ScriptAction sa:
                LaunchScript(sa);
                break;

            case MacroAction ma:
                _ = ExecuteMacroAsync(ma).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Trace.WriteLine($"[GamepadKeyMapperEngine] Macro execution failed: {t.Exception?.GetBaseException().Message}");
                }, TaskScheduler.Default);
                break;
        }
    }

    private static void ExecuteActionUp(MappingAction action)
    {
        switch (action)
        {
            case KeyboardAction ka:
                InputSimulator.KeyUp(ka.KeyName);
                break;

            case MouseButtonAction mba:
                InputSimulator.MouseUp(mba.Button);
                break;
        }
    }

    private void ReleaseAllHeld()
    {
        if (_activeProfile == null) return;
        var set = ActiveSet;
        if (set == null) return;

        foreach (var input in _heldInputs.ToList())
        {
            ExecuteInputUp(input, set);
        }
        _heldInputs.Clear();
    }

    private static void LaunchScript(ScriptAction action)
    {
        if (string.IsNullOrWhiteSpace(action.FilePath)) return;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = action.FilePath,
                Arguments = action.Arguments,
                UseShellExecute = true,
                CreateNoWindow = false
            };
            Process.Start(psi)?.Dispose();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapperEngine] Script launch failed: {ex.Message}");
        }
    }

    private static async Task ExecuteMacroAsync(MacroAction macro, int depth = 0)
    {
        // Guard against recursive macros (a macro step containing another macro)
        if (depth > 8) return;

        foreach (var step in macro.Steps)
        {
            if (step.Action is MacroAction nested)
            {
                await ExecuteMacroAsync(nested, depth + 1).ConfigureAwait(false);
            }
            else
            {
                ExecuteActionDown(step.Action);
                // Brief hold for key events
                if (step.Action is KeyboardAction or MouseButtonAction)
                {
                    await Task.Delay(30).ConfigureAwait(false);
                    ExecuteActionUp(step.Action);
                }
            }

            if (step.DelayMs > 0)
                await Task.Delay(step.DelayMs).ConfigureAwait(false);
        }
    }

    // ── Auto-profile ────────────────────────────────────────────────────

    private void CheckAutoProfile()
    {
        foreach (var rule in _config.AutoProfileRules)
        {
            if (ActiveWindowMonitor.Matches(rule))
            {
                if (_activeProfile == null ||
                    !_activeProfile.Name.Equals(rule.ProfileName, StringComparison.OrdinalIgnoreCase))
                {
                    SwitchProfile(rule.ProfileName);
                }
                return;
            }
        }
    }

    // ── SDL2 helpers ────────────────────────────────────────────────────

    private bool TryInitSdl()
    {
        try
        {
            SDL2Interop.RegisterResolver();

            uint needed = SDL2Interop.SDL_INIT_JOYSTICK | SDL2Interop.SDL_INIT_GAMECONTROLLER;
            uint already = SDL2Interop.SDL_WasInit(needed);

            if ((already & needed) == needed)
            {
                _sdlOwned = false;
                return true;
            }

            if (SDL2Interop.SDL_Init(needed) < 0)
            {
                Trace.WriteLine($"[GamepadKeyMapperEngine] SDL_Init failed: {SDL2Interop.GetError()}");
                return false;
            }

            _sdlOwned = true;
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapperEngine] SDL2 not available: {ex.Message}");
            return false;
        }
    }

    private bool TryOpenController()
    {
        try
        {
            int count = SDL2Interop.SDL_NumJoysticks();
            for (int i = 0; i < count; i++)
            {
                if (SDL2Interop.SDL_IsGameController(i))
                {
                    _controller = SDL2Interop.SDL_GameControllerOpen(i);
                    _deviceIndex = i;
                    return _controller != IntPtr.Zero;
                }
            }

            // Fallback: open first joystick
            if (count > 0)
            {
                _joystick = SDL2Interop.SDL_JoystickOpen(0);
                _deviceIndex = 0;
                return _joystick != IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapperEngine] Controller open failed: {ex.Message}");
        }
        return false;
    }

    private void CloseController()
    {
        if (_controller != IntPtr.Zero)
        {
            try { SDL2Interop.SDL_GameControllerClose(_controller); } catch { }
            _controller = IntPtr.Zero;
        }
        if (_joystick != IntPtr.Zero)
        {
            try { SDL2Interop.SDL_JoystickClose(_joystick); } catch { }
            _joystick = IntPtr.Zero;
        }
        _deviceIndex = -1;
    }

    /// <summary>
    /// Returns a human-readable label for a <see cref="GamepadInput"/> value.
    /// </summary>
    public static string GetInputDisplayName(GamepadInput input) => input switch
    {
        GamepadInput.ButtonA => "A",
        GamepadInput.ButtonB => "B",
        GamepadInput.ButtonX => "X",
        GamepadInput.ButtonY => "Y",
        GamepadInput.ButtonBack => "Back",
        GamepadInput.ButtonGuide => "Guide",
        GamepadInput.ButtonStart => "Start",
        GamepadInput.ButtonLeftStick => "L3",
        GamepadInput.ButtonRightStick => "R3",
        GamepadInput.ButtonLeftShoulder => "LB",
        GamepadInput.ButtonRightShoulder => "RB",
        GamepadInput.DPadUp => "D-Pad ↑",
        GamepadInput.DPadDown => "D-Pad ↓",
        GamepadInput.DPadLeft => "D-Pad ←",
        GamepadInput.DPadRight => "D-Pad →",
        GamepadInput.LeftStickUp => "L-Stick ↑",
        GamepadInput.LeftStickDown => "L-Stick ↓",
        GamepadInput.LeftStickLeft => "L-Stick ←",
        GamepadInput.LeftStickRight => "L-Stick →",
        GamepadInput.RightStickUp => "R-Stick ↑",
        GamepadInput.RightStickDown => "R-Stick ↓",
        GamepadInput.RightStickLeft => "R-Stick ←",
        GamepadInput.RightStickRight => "R-Stick →",
        GamepadInput.LeftTrigger => "LT",
        GamepadInput.RightTrigger => "RT",
        _ => input.ToString()
    };
}
