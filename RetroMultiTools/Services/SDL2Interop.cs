using System.IO;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Services;

/// <summary>
/// Minimal P/Invoke bindings for the SDL2 Game Controller API.
/// Only the functions needed for gamepad input in Big Picture Mode are declared.
/// SDL2 must be installed on the system:
///   Windows – SDL2.dll next to the executable or on PATH
///   Linux   – libSDL2-2.0.so.0 (apt install libsdl2-2.0-0)
///   macOS   – libSDL2.dylib (brew install sdl2)
/// </summary>
internal static class SDL2Interop
{
    private const string LibName = "SDL2";

    // ── Initialization flags ───────────────────────────────────────────
    internal const uint SDL_INIT_JOYSTICK = 0x00000200;
    internal const uint SDL_INIT_GAMECONTROLLER = 0x00002000;
    internal const uint SDL_INIT_EVENTS = 0x00004000;

    // ── Event types ────────────────────────────────────────────────────
    internal const uint SDL_CONTROLLERAXISMOTION = 0x0650;
    internal const uint SDL_CONTROLLERBUTTONDOWN = 0x0651;
    internal const uint SDL_CONTROLLERBUTTONUP = 0x0652;
    internal const uint SDL_CONTROLLERDEVICEADDED = 0x0653;
    internal const uint SDL_CONTROLLERDEVICEREMOVED = 0x0654;

    // ── Game controller buttons (SDL_GameControllerButton) ─────────────
    internal const int SDL_CONTROLLER_BUTTON_A = 0;
    internal const int SDL_CONTROLLER_BUTTON_B = 1;
    internal const int SDL_CONTROLLER_BUTTON_X = 2;
    internal const int SDL_CONTROLLER_BUTTON_Y = 3;
    internal const int SDL_CONTROLLER_BUTTON_BACK = 4;
    internal const int SDL_CONTROLLER_BUTTON_GUIDE = 5;
    internal const int SDL_CONTROLLER_BUTTON_START = 6;
    internal const int SDL_CONTROLLER_BUTTON_LEFTSTICK = 7;
    internal const int SDL_CONTROLLER_BUTTON_RIGHTSTICK = 8;
    internal const int SDL_CONTROLLER_BUTTON_LEFTSHOULDER = 9;
    internal const int SDL_CONTROLLER_BUTTON_RIGHTSHOULDER = 10;
    internal const int SDL_CONTROLLER_BUTTON_DPAD_UP = 11;
    internal const int SDL_CONTROLLER_BUTTON_DPAD_DOWN = 12;
    internal const int SDL_CONTROLLER_BUTTON_DPAD_LEFT = 13;
    internal const int SDL_CONTROLLER_BUTTON_DPAD_RIGHT = 14;

    // ── Game controller axes (SDL_GameControllerAxis) ──────────────────
    internal const int SDL_CONTROLLER_AXIS_LEFTX = 0;
    internal const int SDL_CONTROLLER_AXIS_LEFTY = 1;
    internal const int SDL_CONTROLLER_AXIS_RIGHTX = 2;
    internal const int SDL_CONTROLLER_AXIS_RIGHTY = 3;
    internal const int SDL_CONTROLLER_AXIS_TRIGGERLEFT = 4;
    internal const int SDL_CONTROLLER_AXIS_TRIGGERRIGHT = 5;

    /// <summary>Maximum absolute value returned by SDL2 for axis readings.</summary>
    internal const double SDL_AXIS_MAX = 32767.0;

    // ── SDL_Event (simplified – only the fields we need) ───────────────
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    internal struct SDL_Event
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(4)] public uint timestamp;

        // Controller device events (added / removed)
        [FieldOffset(8)] public int cdevice_which;

        // Controller button events
        [FieldOffset(8)] public int cbutton_which;
        [FieldOffset(12)] public byte cbutton_button;
        [FieldOffset(13)] public byte cbutton_state;

        // Controller axis events
        [FieldOffset(8)] public int caxis_which;
        [FieldOffset(12)] public byte caxis_axis;
        [FieldOffset(16)] public short caxis_value;

        // Joystick button events (SDL_JoyButtonEvent)
        [FieldOffset(8)] public int jbutton_which;
        [FieldOffset(12)] public byte jbutton_button;

        // Joystick axis events (SDL_JoyAxisEvent)
        [FieldOffset(8)] public int jaxis_which;
        [FieldOffset(12)] public byte jaxis_axis;
        [FieldOffset(16)] public short jaxis_value;

        // Joystick hat events (SDL_JoyHatEvent)
        [FieldOffset(8)] public int jhat_which;
        [FieldOffset(12)] public byte jhat_hat;
        [FieldOffset(13)] public byte jhat_value;
    }

    // ── SDL lifecycle ──────────────────────────────────────────────────

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_Init(uint flags);

    /// <summary>Returns a mask of the specified subsystems that are currently initialised.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint SDL_WasInit(uint flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_Quit();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_PollEvent(out SDL_Event e);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetError();

    /// <summary>Returns the last SDL error message, or an empty string.</summary>
    internal static string GetError()
    {
        IntPtr ptr = SDL_GetError();
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    // ── Joystick enumeration ───────────────────────────────────────────

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_NumJoysticks();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SDL_IsGameController(int joystick_index);

    // ── Game Controller open / close / query ───────────────────────────

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_GameControllerOpen(int joystick_index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_GameControllerClose(IntPtr gamecontroller);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_GameControllerName(IntPtr gamecontroller);

    internal static string? GameControllerName(IntPtr gc)
    {
        IntPtr ptr = SDL_GameControllerName(gc);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    /// <summary>Returns the instance id of the joystick backing a game controller.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_JoystickInstanceID(IntPtr joystick);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_GameControllerGetJoystick(IntPtr gamecontroller);

    /// <summary>
    /// Loads additional controller mappings from a gamecontrollerdb.txt file.
    /// Note: The C SDL2 header defines SDL_GameControllerAddMappingsFromFile as
    /// a preprocessor macro, so it is not an exported symbol. This managed
    /// helper reads the file and calls <see cref="SDL_GameControllerAddMapping"/>
    /// for each valid mapping line, replicating the macro's behavior.
    /// </summary>
    internal static int SDL_GameControllerAddMappingsFromFile(string file)
    {
        if (!File.Exists(file)) return -1;

        int added = 0;
        foreach (string raw in File.ReadLines(file))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            int result = SDL_GameControllerAddMapping(line);
            if (result >= 0) added++;
        }
        return added;
    }

    // ── Button / axis state (polled) ───────────────────────────────────

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte SDL_GameControllerGetButton(IntPtr gamecontroller, int button);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern short SDL_GameControllerGetAxis(IntPtr gamecontroller, int axis);

    // ── Raw joystick API (for mapping unmapped controllers) ───────────

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_JoystickOpen(int device_index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_JoystickClose(IntPtr joystick);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_JoystickNameForIndex(int device_index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_JoystickNumButtons(IntPtr joystick);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_JoystickNumAxes(IntPtr joystick);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_JoystickNumHats(IntPtr joystick);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte SDL_JoystickGetButton(IntPtr joystick, int button);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern short SDL_JoystickGetAxis(IntPtr joystick, int axis);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte SDL_JoystickGetHat(IntPtr joystick, int hat);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SDL_JoystickGUID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal byte[] data;
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern SDL_JoystickGUID SDL_JoystickGetDeviceGUID(int device_index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_JoystickGetGUIDString(
        SDL_JoystickGUID guid, IntPtr pszGUID, int cbGUID);

    /// <summary>Returns the GUID of a joystick device as a 32-character hex string.</summary>
    internal static string JoystickDeviceGUIDString(int deviceIndex)
    {
        var guid = SDL_JoystickGetDeviceGUID(deviceIndex);
        IntPtr buffer = Marshal.AllocHGlobal(64);
        try
        {
            SDL_JoystickGetGUIDString(guid, buffer, 64);
            return Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Returns the name of a joystick at the given device index.</summary>
    internal static string? JoystickNameForIndex(int deviceIndex)
    {
        IntPtr ptr = SDL_JoystickNameForIndex(deviceIndex);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    /// <summary>Adds a single mapping string at runtime. Returns 1 if new, 0 if updated, -1 on error.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_GameControllerAddMapping(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string mappingString);

    /// <summary>Returns the current mapping string for a game controller, or null.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_GameControllerMapping(IntPtr gamecontroller);

    // ── Joystick event types ───────────────────────────────────────────
    internal const uint SDL_JOYAXISMOTION = 0x0600;
    internal const uint SDL_JOYHATMOTION = 0x0602;
    internal const uint SDL_JOYBUTTONDOWN = 0x0603;
    internal const uint SDL_JOYBUTTONUP = 0x0604;
    internal const uint SDL_JOYDEVICEADDED = 0x0605;
    internal const uint SDL_JOYDEVICEREMOVED = 0x0606;

    // ── Hat constants ──────────────────────────────────────────────────
    internal const byte SDL_HAT_CENTERED = 0x00;
    internal const byte SDL_HAT_UP = 0x01;
    internal const byte SDL_HAT_RIGHT = 0x02;
    internal const byte SDL_HAT_DOWN = 0x04;
    internal const byte SDL_HAT_LEFT = 0x08;

    // ── Hint / joystick events toggle ──────────────────────────────────

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_JoystickEventState(int state);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_GameControllerEventState(int state);

    internal const int SDL_ENABLE = 1;

    // ── DLL import resolver ────────────────────────────────────────────

    private static bool _resolverRegistered;

    /// <summary>
    /// Registers a platform-aware DLL import resolver so that
    /// <c>[DllImport("SDL2")]</c> resolves to the correct native library name on
    /// each OS.  Call once at startup before any SDL2 P/Invoke.
    /// </summary>
    internal static void RegisterResolver()
    {
        if (_resolverRegistered) return;
        _resolverRegistered = true;

        NativeLibrary.SetDllImportResolver(
            typeof(SDL2Interop).Assembly,
            static (name, assembly, searchPath) =>
            {
                if (name != LibName)
                    return IntPtr.Zero;

                // Try platform-specific names
                string[] candidates;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    candidates = ["SDL2.dll"];
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    candidates = ["libSDL2-2.0.so.0", "libSDL2.so"];
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    candidates = ["libSDL2.dylib", "libSDL2-2.0.0.dylib"];
                else
                    candidates = ["SDL2"];

                foreach (string candidate in candidates)
                {
                    if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out IntPtr handle))
                        return handle;
                }

                return IntPtr.Zero;
            });
    }
}
