using Avalonia.Threading;

namespace RetroMultiTools.Services;

/// <summary>
/// Provides native game controller input for Big Picture Mode using the SDL2
/// Game Controller API.  Supports autoconfig (Plug &amp; Play) – controllers are
/// automatically recognised when plugged in or removed thanks to SDL2's
/// built-in controller database.  An optional <c>gamecontrollerdb.txt</c> file
/// placed next to the executable supplies additional mappings for full
/// RetroArch-compatible autoconfig.
/// </summary>
public sealed class GamepadService : IDisposable
{
    // ── Singleton ──────────────────────────────────────────────────────

    private static readonly Lazy<GamepadService> _lazy = new(() => new GamepadService());
    public static GamepadService Instance => _lazy.Value;

    // ── Public state ───────────────────────────────────────────────────

    /// <summary>Whether SDL2 was loaded and initialised successfully.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Whether at least one controller (mapped or fallback) is connected.</summary>
    public bool IsControllerConnected => _controllers.Count > 0 || _fallbackJoysticks.Count > 0;

    /// <summary>Display name of the first connected controller, or <c>null</c>.</summary>
    public string? ControllerName
    {
        get
        {
            foreach (var kv in _controllers)
            {
                string? name = SDL2Interop.GameControllerName(kv.Value);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            foreach (var kv in _fallbackJoysticks)
            {
                if (!string.IsNullOrEmpty(kv.Value.Name)) return kv.Value.Name;
            }
            return _controllers.Count > 0 || _fallbackJoysticks.Count > 0 ? DefaultControllerName : null;
        }
    }

    // ── Events (raised on the UI thread) ───────────────────────────────

    /// <summary>Raised when a recognised game controller is plugged in.</summary>
    public event Action<string>? ControllerConnected;

    /// <summary>Raised when a game controller is removed.</summary>
    public event Action? ControllerDisconnected;

    /// <summary>Raised when a mapped gamepad action should be executed.</summary>
    public event Action<GamepadAction>? ActionTriggered;

    // ── Private state ──────────────────────────────────────────────────

    /// <summary>
    /// Invokes <see cref="ActionTriggered"/> inside a try-catch so that a
    /// misbehaving subscriber cannot break the polling loop.
    /// </summary>
    private void RaiseAction(GamepadAction action)
    {
        try
        {
            ActionTriggered?.Invoke(action);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[GamepadService] ActionTriggered handler threw: {ex.Message}");
        }
    }

    private readonly Dictionary<int, IntPtr> _controllers = new(); // instance-id → handle
    private readonly Dictionary<int, FallbackJoystick> _fallbackJoysticks = new(); // instance-id → info
    private DispatcherTimer? _pollTimer;
    private bool _disposed;
    private bool _sdlInitialised;

    // Repeat-key emulation for held analog sticks
    private GamepadAction? _heldStickAction;
    private DateTime _heldSince;
    private DateTime _lastRepeat;

    // Right-stick zoom debouncing
    private GamepadAction? _lastZoomAction;
    private DateTime _lastZoomTime;

    /// <summary>Axis dead-zone as a fraction of the full range (0 – 1).</summary>
    private double _deadZone = 0.25;

    /// <summary>Initial delay before stick-repeat begins (ms).</summary>
    private int _repeatDelayMs = 400;

    /// <summary>Interval between repeated stick actions once held (ms).</summary>
    private int _repeatRateMs = 120;

    // ── Fallback joystick constants ────────────────────────────────────

    /// <summary>Default name shown when the controller name cannot be retrieved.</summary>
    private const string DefaultControllerName = "Controller";

    // Generic positional button indices for unmapped joysticks
    private const byte FallbackButtonConfirm = 0;
    private const byte FallbackButtonRomInfo = 1;
    private const byte FallbackButtonSearch = 2;
    private const byte FallbackButtonFavorite = 3;
    private const byte FallbackButtonRandomGame = 4;
    private const byte FallbackButtonBack = 5;
    private const byte FallbackButtonHelp = 6;
    private const byte FallbackButtonPageUp = 7;
    private const byte FallbackButtonPageDown = 8;
    private const byte FallbackButtonHome = 9;
    private const byte FallbackButtonEnd = 10;

    // Generic axis indices for unmapped joysticks
    private const byte FallbackAxisLeftX = 0;
    private const byte FallbackAxisLeftY = 1;
    private const byte FallbackAxisRightY = 3;

    // ── Initialisation ─────────────────────────────────────────────────

    private GamepadService() { }

    /// <summary>
    /// Attempts to initialise SDL2 and starts polling for gamepad events.
    /// Safe to call multiple times – subsequent calls are ignored.
    /// Can be called again after <see cref="Shutdown"/> to restart polling.
    /// </summary>
    public void Initialise()
    {
        if (IsAvailable || _disposed) return;

        try
        {
            SDL2Interop.RegisterResolver();

            // SDL_Init is reference-counted; safe to call when already initialised
            if (!_sdlInitialised)
            {
                int result = SDL2Interop.SDL_Init(
                    SDL2Interop.SDL_INIT_JOYSTICK
                    | SDL2Interop.SDL_INIT_GAMECONTROLLER
                    | SDL2Interop.SDL_INIT_EVENTS);

                if (result < 0)
                {
                    string error = SDL2Interop.GetError();
                    System.Diagnostics.Trace.WriteLine(
                        $"[GamepadService] SDL_Init failed: {error}");
                    return;
                }

                _sdlInitialised = true;

                // Enable joystick and game controller events so SDL_PollEvent
                // delivers hotplug and input notifications.
                SDL2Interop.SDL_JoystickEventState(SDL2Interop.SDL_ENABLE);
                SDL2Interop.SDL_GameControllerEventState(SDL2Interop.SDL_ENABLE);

                // Load additional mappings if a gamecontrollerdb.txt exists next to the exe
                LoadExternalMappings();

                // Load user-created custom mappings from app data
                LoadCustomMappings();
            }

            IsAvailable = true;

            // Pump any events that accumulated (e.g. initial device-added
            // notifications) so SDL2's internal state is fully up-to-date
            // before we enumerate devices.
            while (SDL2Interop.SDL_PollEvent(out _) != 0) { }

            // Open any controllers already connected at startup
            OpenExistingControllers();

            // Start a dispatcher timer that pumps SDL events on the UI thread
            _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 Hz
            };
            _pollTimer.Tick += PollTick;
            _pollTimer.Start();

            System.Diagnostics.Trace.WriteLine(
                $"[GamepadService] Initialised – {_controllers.Count} mapped + " +
                $"{_fallbackJoysticks.Count} fallback controller(s) detected.");
        }
        catch (DllNotFoundException)
        {
            System.Diagnostics.Trace.WriteLine(
                "[GamepadService] SDL2 native library not found – gamepad support disabled.");
        }
        catch (EntryPointNotFoundException ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[GamepadService] SDL2 entry point missing ({ex.Message}) – gamepad support disabled.");
        }
    }

    // ── Configuration ──────────────────────────────────────────────────

    /// <summary>
    /// Updates the analog stick dead-zone (0 – 1).  Values below 0.05 are
    /// clamped to avoid drift.
    /// </summary>
    public void SetDeadZone(double value) =>
        _deadZone = Math.Clamp(value, 0.05, 0.95);

    /// <summary>
    /// Configures how long an analog stick must be held before repeat
    /// navigation starts, and the interval between repeats.
    /// </summary>
    public void SetRepeatTiming(int delayMs, int rateMs)
    {
        _repeatDelayMs = Math.Max(100, delayMs);
        _repeatRateMs = Math.Max(30, rateMs);
    }

    // ── Polling ────────────────────────────────────────────────────────

    private void PollTick(object? sender, EventArgs e)
    {
        if (!IsAvailable) return;

        // Pump all pending SDL events
        while (SDL2Interop.SDL_PollEvent(out SDL2Interop.SDL_Event ev) != 0)
        {
            switch (ev.type)
            {
                case SDL2Interop.SDL_CONTROLLERDEVICEADDED:
                    OnControllerAdded(ev.cdevice_which);
                    break;

                case SDL2Interop.SDL_CONTROLLERDEVICEREMOVED:
                    OnControllerRemoved(ev.cdevice_which);
                    break;

                case SDL2Interop.SDL_CONTROLLERBUTTONDOWN:
                    OnButtonDown(ev.cbutton_button);
                    break;

                case SDL2Interop.SDL_CONTROLLERAXISMOTION:
                    OnAxisMotion(ev.caxis_axis, ev.caxis_value);
                    break;

                // Fallback joystick events for unmapped controllers
                case SDL2Interop.SDL_JOYBUTTONDOWN:
                    OnFallbackButtonDown(ev.jbutton_which, ev.jbutton_button);
                    break;

                case SDL2Interop.SDL_JOYAXISMOTION:
                    OnFallbackAxisMotion(ev.jaxis_which, ev.jaxis_axis, ev.jaxis_value);
                    break;

                case SDL2Interop.SDL_JOYHATMOTION:
                    OnFallbackHatMotion(ev.jhat_which, ev.jhat_value);
                    break;

                // Joystick hotplug for devices without a game controller mapping
                case SDL2Interop.SDL_JOYDEVICEADDED:
                    OnJoystickAdded(ev.cdevice_which);
                    break;

                case SDL2Interop.SDL_JOYDEVICEREMOVED:
                    OnJoystickRemoved(ev.cdevice_which);
                    break;
            }
        }

        // Handle analog-stick repeat
        ProcessStickRepeat();
    }

    // ── Hotplug ────────────────────────────────────────────────────────

    private void OnControllerAdded(int deviceIndex)
    {
        if (!SDL2Interop.SDL_IsGameController(deviceIndex))
            return;

        IntPtr gc = SDL2Interop.SDL_GameControllerOpen(deviceIndex);
        if (gc == IntPtr.Zero) return;

        IntPtr joy = SDL2Interop.SDL_GameControllerGetJoystick(gc);
        int instanceId = SDL2Interop.SDL_JoystickInstanceID(joy);

        if (_controllers.ContainsKey(instanceId))
        {
            // Already tracked – close the extra reference to avoid a leak
            SDL2Interop.SDL_GameControllerClose(gc);
            return;
        }

        _controllers[instanceId] = gc;

        string name = SDL2Interop.GameControllerName(gc) ?? DefaultControllerName;
        System.Diagnostics.Trace.WriteLine(
            $"[GamepadService] Connected mapped controller: {name} (id {instanceId})");
        ControllerConnected?.Invoke(name);
    }

    private void OnControllerRemoved(int instanceId)
    {
        if (_controllers.TryGetValue(instanceId, out IntPtr gc))
        {
            SDL2Interop.SDL_GameControllerClose(gc);
            _controllers.Remove(instanceId);

            System.Diagnostics.Trace.WriteLine(
                $"[GamepadService] Disconnected controller id {instanceId}");
            ControllerDisconnected?.Invoke();
        }
    }

    /// <summary>
    /// Handles SDL_JOYDEVICEADDED for joysticks that have no game controller
    /// mapping.  Mapped controllers are handled by <see cref="OnControllerAdded"/>;
    /// this method only opens the fallback path.
    /// </summary>
    private void OnJoystickAdded(int deviceIndex)
    {
        // Skip devices that have a game controller mapping – they are
        // handled via SDL_CONTROLLERDEVICEADDED / OnControllerAdded instead.
        if (SDL2Interop.SDL_IsGameController(deviceIndex))
            return;

        OpenFallbackJoystick(deviceIndex);
    }

    /// <summary>
    /// Handles SDL_JOYDEVICEREMOVED.  This fires for every joystick,
    /// including those already handled as game controllers; the lookup
    /// into <c>_fallbackJoysticks</c> ensures we only act on fallback devices.
    /// </summary>
    private void OnJoystickRemoved(int instanceId)
    {
        if (_fallbackJoysticks.TryGetValue(instanceId, out var fb))
        {
            SDL2Interop.SDL_JoystickClose(fb.Handle);
            _fallbackJoysticks.Remove(instanceId);

            System.Diagnostics.Trace.WriteLine(
                $"[GamepadService] Disconnected fallback joystick id {instanceId}");
            ControllerDisconnected?.Invoke();
        }
    }

    // ── Button mapping ─────────────────────────────────────────────────
    // Standard Xbox-style layout used by SDL2:
    //   A  → Confirm / Launch          B  → ROM Info
    //   X  → Search                    Y  → Toggle favorite
    //   Start → Help overlay           Back/Select → Random game
    //   Guide → Exit Big Picture Mode
    //   LB → Page Up                   RB → Page Down
    //   D-pad → Navigate cards         Left stick → Navigate cards
    //   L3 → Home (first card)         R3 → End (last card)
    //   Right stick Y → Zoom in/out

    private void OnButtonDown(byte button)
    {
        GamepadAction? action = button switch
        {
            SDL2Interop.SDL_CONTROLLER_BUTTON_A => GamepadAction.Confirm,
            SDL2Interop.SDL_CONTROLLER_BUTTON_B => GamepadAction.RomInfo,
            SDL2Interop.SDL_CONTROLLER_BUTTON_X => GamepadAction.Search,
            SDL2Interop.SDL_CONTROLLER_BUTTON_Y => GamepadAction.ToggleFavorite,
            SDL2Interop.SDL_CONTROLLER_BUTTON_START => GamepadAction.Help,
            SDL2Interop.SDL_CONTROLLER_BUTTON_BACK => GamepadAction.RandomGame,
            SDL2Interop.SDL_CONTROLLER_BUTTON_LEFTSHOULDER => GamepadAction.PageUp,
            SDL2Interop.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER => GamepadAction.PageDown,
            SDL2Interop.SDL_CONTROLLER_BUTTON_DPAD_UP => GamepadAction.NavigateUp,
            SDL2Interop.SDL_CONTROLLER_BUTTON_DPAD_DOWN => GamepadAction.NavigateDown,
            SDL2Interop.SDL_CONTROLLER_BUTTON_DPAD_LEFT => GamepadAction.NavigateLeft,
            SDL2Interop.SDL_CONTROLLER_BUTTON_DPAD_RIGHT => GamepadAction.NavigateRight,
            SDL2Interop.SDL_CONTROLLER_BUTTON_LEFTSTICK => GamepadAction.Home,
            SDL2Interop.SDL_CONTROLLER_BUTTON_RIGHTSTICK => GamepadAction.End,
            SDL2Interop.SDL_CONTROLLER_BUTTON_GUIDE => GamepadAction.Back,
            _ => null
        };

        if (action.HasValue)
            RaiseAction(action.Value);
    }

    // ── Analog stick handling ──────────────────────────────────────────

    /// <summary>Minimum interval (ms) between right-stick zoom actions.</summary>
    private const int ZoomDebounceMs = 300;

    private void OnAxisMotion(byte axis, short value)
    {
        double normalized = value / SDL2Interop.SDL_AXIS_MAX;
        bool active = Math.Abs(normalized) > _deadZone;

        GamepadAction? action = null;

        switch (axis)
        {
            case SDL2Interop.SDL_CONTROLLER_AXIS_LEFTX:
                action = active
                    ? (normalized < 0 ? GamepadAction.NavigateLeft : GamepadAction.NavigateRight)
                    : null;
                break;

            case SDL2Interop.SDL_CONTROLLER_AXIS_LEFTY:
                action = active
                    ? (normalized < 0 ? GamepadAction.NavigateUp : GamepadAction.NavigateDown)
                    : null;
                break;

            case SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTY:
                if (active)
                    action = normalized < 0 ? GamepadAction.ZoomIn : GamepadAction.ZoomOut;
                break;
        }

        // Track held-direction for repeat navigation (left stick only)
        if (axis is SDL2Interop.SDL_CONTROLLER_AXIS_LEFTX or SDL2Interop.SDL_CONTROLLER_AXIS_LEFTY)
        {
            if (action.HasValue && action != _heldStickAction)
            {
                // New direction — reset repeat state and fire immediately
                _heldStickAction = action;
                _heldSince = DateTime.UtcNow;
                _lastRepeat = DateTime.UtcNow;
                RaiseAction(action.Value);
            }
            else if (!active)
            {
                // Stick returned to center on this axis — clear if the held action
                // matches the axis that went idle
                bool isXAction = _heldStickAction is GamepadAction.NavigateLeft or GamepadAction.NavigateRight;
                bool isYAction = _heldStickAction is GamepadAction.NavigateUp or GamepadAction.NavigateDown;

                if ((isXAction && axis == SDL2Interop.SDL_CONTROLLER_AXIS_LEFTX)
                    || (isYAction && axis == SDL2Interop.SDL_CONTROLLER_AXIS_LEFTY))
                {
                    _heldStickAction = null;
                }
            }
        }
        else if (axis == SDL2Interop.SDL_CONTROLLER_AXIS_RIGHTY)
        {
            // Debounce right-stick zoom: fire once per crossing and then honour
            // a minimum interval so holding the stick doesn't zoom at 60 Hz.
            if (action.HasValue)
            {
                DateTime now = DateTime.UtcNow;
                bool isNewDirection = action != _lastZoomAction;
                bool debounceElapsed = (now - _lastZoomTime).TotalMilliseconds >= ZoomDebounceMs;

                if (isNewDirection || debounceElapsed)
                {
                    _lastZoomAction = action;
                    _lastZoomTime = now;
                    RaiseAction(action.Value);
                }
            }
            else
            {
                // Stick returned to center — allow immediate zoom on next deflection
                _lastZoomAction = null;
            }
        }
    }

    private void ProcessStickRepeat()
    {
        if (_heldStickAction is not { } action) return;

        DateTime now = DateTime.UtcNow;
        double heldMs = (now - _heldSince).TotalMilliseconds;
        double sinceRepeat = (now - _lastRepeat).TotalMilliseconds;

        if (heldMs >= _repeatDelayMs && sinceRepeat >= _repeatRateMs)
        {
            _lastRepeat = now;
            RaiseAction(action);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private void OpenExistingControllers()
    {
        int count = SDL2Interop.SDL_NumJoysticks();
        for (int i = 0; i < count; i++)
        {
            if (SDL2Interop.SDL_IsGameController(i))
            {
                IntPtr gc = SDL2Interop.SDL_GameControllerOpen(i);
                if (gc == IntPtr.Zero) continue;

                IntPtr joy = SDL2Interop.SDL_GameControllerGetJoystick(gc);
                int instanceId = SDL2Interop.SDL_JoystickInstanceID(joy);

                if (_controllers.ContainsKey(instanceId))
                {
                    // Already tracked – close the extra reference to avoid a leak
                    SDL2Interop.SDL_GameControllerClose(gc);
                    continue;
                }

                _controllers[instanceId] = gc;

                string name = SDL2Interop.GameControllerName(gc) ?? DefaultControllerName;
                System.Diagnostics.Trace.WriteLine(
                    $"[GamepadService] Connected mapped controller: {name} (id {instanceId})");
                ControllerConnected?.Invoke(name);
            }
            else
            {
                // Fallback: open as raw joystick so unmapped controllers still work
                OpenFallbackJoystick(i);
            }
        }

        if (count > 0)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[GamepadService] Found {count} joystick(s): " +
                $"{_controllers.Count} mapped, {_fallbackJoysticks.Count} fallback.");
        }
    }

    /// <summary>
    /// Opens a joystick that has no game controller mapping as a raw
    /// fallback device with a generic button/axis mapping.
    /// </summary>
    private void OpenFallbackJoystick(int deviceIndex)
    {
        IntPtr joy = SDL2Interop.SDL_JoystickOpen(deviceIndex);
        if (joy == IntPtr.Zero) return;

        int instanceId = SDL2Interop.SDL_JoystickInstanceID(joy);

        if (_fallbackJoysticks.ContainsKey(instanceId))
        {
            // Already tracked – close the extra reference to avoid a leak
            SDL2Interop.SDL_JoystickClose(joy);
            return;
        }

        string name = SDL2Interop.JoystickNameForIndex(deviceIndex) ?? DefaultControllerName;

        _fallbackJoysticks[instanceId] = new FallbackJoystick(joy, name);

        System.Diagnostics.Trace.WriteLine(
            $"[GamepadService] Connected fallback joystick: {name} (id {instanceId})");
        ControllerConnected?.Invoke(name);
    }

    /// <summary>
    /// Loads additional SDL game-controller mappings from
    /// <c>gamecontrollerdb.txt</c> placed next to the executable, or from
    /// the RetroArch autoconfig directory if RetroArch is configured.
    /// </summary>
    private static void LoadExternalMappings()
    {
        // 1. gamecontrollerdb.txt next to the application
        string? exeDir = Path.GetDirectoryName(
            Environment.ProcessPath ?? AppContext.BaseDirectory);

        if (exeDir != null)
        {
            string dbPath = Path.Combine(exeDir, "gamecontrollerdb.txt");
            if (File.Exists(dbPath))
            {
                int added = SDL2Interop.SDL_GameControllerAddMappingsFromFile(dbPath);
                System.Diagnostics.Trace.WriteLine(
                    $"[GamepadService] Loaded {added} mapping(s) from {dbPath}");
            }
        }

        // 2. RetroArch autoconfig directory (SDL mappings are compatible)
        string retroArchPath = AppSettings.Instance.RetroArchPath;
        if (!string.IsNullOrEmpty(retroArchPath))
        {
            string? raDir = Path.GetDirectoryName(retroArchPath);
            if (raDir != null)
            {
                string raDbPath = Path.Combine(raDir, "autoconfig", "gamecontrollerdb.txt");
                if (File.Exists(raDbPath))
                {
                    int added = SDL2Interop.SDL_GameControllerAddMappingsFromFile(raDbPath);
                    System.Diagnostics.Trace.WriteLine(
                        $"[GamepadService] Loaded {added} RetroArch mapping(s) from {raDbPath}");
                }
            }
        }
    }

    /// <summary>
    /// Loads user-created custom controller mappings from the application
    /// data directory and applies them to the SDL2 runtime.
    /// </summary>
    private static void LoadCustomMappings()
    {
        int applied = Utilities.GamepadMappingStorage.ApplyAllToSdl();
        if (applied > 0)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[GamepadService] Applied {applied} custom mapping(s) from user storage.");
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Stops polling and releases controller handles but keeps SDL2
    /// initialised so that <see cref="Initialise"/> can restart the service.
    /// Call this when the gamepad consumer (e.g. Big Picture Mode) is no
    /// longer active.
    /// </summary>
    public void Shutdown()
    {
        if (!IsAvailable) return;

        if (_pollTimer != null)
        {
            _pollTimer.Stop();
            _pollTimer.Tick -= PollTick;
            _pollTimer = null;
        }
        _heldStickAction = null;
        _lastZoomAction = null;

        foreach (IntPtr gc in _controllers.Values)
            SDL2Interop.SDL_GameControllerClose(gc);
        _controllers.Clear();

        foreach (var fb in _fallbackJoysticks.Values)
            SDL2Interop.SDL_JoystickClose(fb.Handle);
        _fallbackJoysticks.Clear();

        IsAvailable = false;
        System.Diagnostics.Trace.WriteLine("[GamepadService] Shut down (SDL2 still loaded).");
    }

    /// <summary>Stops polling, releases all handles, and shuts down SDL2.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Shutdown();

        if (_sdlInitialised)
        {
            SDL2Interop.SDL_Quit();
            _sdlInitialised = false;
        }
    }

    // ── Fallback joystick input ───────────────────────────────────────

    /// <summary>
    /// Handles button presses from joysticks that have no SDL game controller
    /// mapping.  Uses a generic positional mapping:
    ///   0 → Confirm   1 → RomInfo   2 → Search   3 → ToggleFavorite
    ///   4 → RandomGame   5 → Back   6 → Help   7–8 → PageUp/Down
    ///   9 → Home   10 → End
    /// </summary>
    private void OnFallbackButtonDown(int instanceId, byte button)
    {
        if (!_fallbackJoysticks.ContainsKey(instanceId)) return;

        GamepadAction? action = button switch
        {
            FallbackButtonConfirm => GamepadAction.Confirm,
            FallbackButtonRomInfo => GamepadAction.RomInfo,
            FallbackButtonSearch => GamepadAction.Search,
            FallbackButtonFavorite => GamepadAction.ToggleFavorite,
            FallbackButtonRandomGame => GamepadAction.RandomGame,
            FallbackButtonBack => GamepadAction.Back,
            FallbackButtonHelp => GamepadAction.Help,
            FallbackButtonPageUp => GamepadAction.PageUp,
            FallbackButtonPageDown => GamepadAction.PageDown,
            FallbackButtonHome => GamepadAction.Home,
            FallbackButtonEnd => GamepadAction.End,
            _ => null
        };

        if (action.HasValue)
            RaiseAction(action.Value);
    }

    /// <summary>
    /// Handles axis motion from fallback joysticks.  Axes 0/1 map to
    /// left-stick navigation; axis 3 maps to right-stick zoom.
    /// </summary>
    private void OnFallbackAxisMotion(int instanceId, byte axis, short value)
    {
        if (!_fallbackJoysticks.ContainsKey(instanceId)) return;

        double normalized = value / SDL2Interop.SDL_AXIS_MAX;
        bool active = Math.Abs(normalized) > _deadZone;

        GamepadAction? action = null;

        switch (axis)
        {
            case FallbackAxisLeftX:
                action = active
                    ? (normalized < 0 ? GamepadAction.NavigateLeft : GamepadAction.NavigateRight)
                    : null;
                break;
            case FallbackAxisLeftY:
                action = active
                    ? (normalized < 0 ? GamepadAction.NavigateUp : GamepadAction.NavigateDown)
                    : null;
                break;
            case FallbackAxisRightY:
                if (active)
                    action = normalized < 0 ? GamepadAction.ZoomIn : GamepadAction.ZoomOut;
                break;
        }

        // Reuse the same repeat/debounce logic as mapped controllers
        if (axis is FallbackAxisLeftX or FallbackAxisLeftY)
        {
            if (action.HasValue && action != _heldStickAction)
            {
                _heldStickAction = action;
                _heldSince = DateTime.UtcNow;
                _lastRepeat = DateTime.UtcNow;
                RaiseAction(action.Value);
            }
            else if (!active)
            {
                bool isXAction = _heldStickAction is GamepadAction.NavigateLeft or GamepadAction.NavigateRight;
                bool isYAction = _heldStickAction is GamepadAction.NavigateUp or GamepadAction.NavigateDown;
                if ((isXAction && axis == FallbackAxisLeftX) || (isYAction && axis == FallbackAxisLeftY))
                    _heldStickAction = null;
            }
        }
        else if (axis == FallbackAxisRightY && action.HasValue)
        {
            DateTime now = DateTime.UtcNow;
            bool isNewDirection = action != _lastZoomAction;
            bool debounceElapsed = (now - _lastZoomTime).TotalMilliseconds >= ZoomDebounceMs;

            if (isNewDirection || debounceElapsed)
            {
                _lastZoomAction = action;
                _lastZoomTime = now;
                RaiseAction(action.Value);
            }
        }
        else if (axis == FallbackAxisRightY)
        {
            _lastZoomAction = null;
        }
    }

    /// <summary>
    /// Handles hat (D-pad) motion from fallback joysticks.
    /// </summary>
    private void OnFallbackHatMotion(int instanceId, byte hatValue)
    {
        if (!_fallbackJoysticks.ContainsKey(instanceId)) return;

        // Use bitwise checks so diagonal D-pad presses (e.g. UP+RIGHT)
        // still produce a navigation action instead of being silently ignored.
        // Priority: vertical before horizontal (natural for grid navigation).
        GamepadAction? action = null;

        if ((hatValue & SDL2Interop.SDL_HAT_UP) != 0)
            action = GamepadAction.NavigateUp;
        else if ((hatValue & SDL2Interop.SDL_HAT_DOWN) != 0)
            action = GamepadAction.NavigateDown;
        else if ((hatValue & SDL2Interop.SDL_HAT_LEFT) != 0)
            action = GamepadAction.NavigateLeft;
        else if ((hatValue & SDL2Interop.SDL_HAT_RIGHT) != 0)
            action = GamepadAction.NavigateRight;

        if (action.HasValue)
            RaiseAction(action.Value);
    }

    // ── Fallback joystick record ───────────────────────────────────────

    private sealed record FallbackJoystick(IntPtr Handle, string Name);
}

/// <summary>
/// Logical actions that a gamepad button/stick press maps to in Big Picture Mode.
/// </summary>
public enum GamepadAction
{
    NavigateLeft,
    NavigateRight,
    NavigateUp,
    NavigateDown,
    Confirm,
    Back,
    ToggleFavorite,
    Search,
    Help,
    RomInfo,
    RandomGame,
    PageUp,
    PageDown,
    Home,
    End,
    ZoomIn,
    ZoomOut
}
