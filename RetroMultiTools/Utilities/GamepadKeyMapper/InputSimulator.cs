using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RetroMultiTools.Utilities.GamepadKeyMapper;

/// <summary>
/// Cross-platform input simulation for keyboard and mouse events.
/// Uses native APIs on each platform:
///   Windows – SendInput / SetCursorPos
///   Linux   – uinput or xdotool via /proc, falling back to xdotool CLI
///   macOS   – CGEvent API
/// </summary>
internal static class InputSimulator
{
    /// <summary>Simulates pressing a keyboard key down.</summary>
    internal static void KeyDown(string keyName)
    {
        if (OperatingSystem.IsLinux())
            LinuxKeyEvent(keyName, press: true);
        else if (OperatingSystem.IsWindows())
            WindowsKeyEvent(keyName, press: true);
        else if (OperatingSystem.IsMacOS())
            MacKeyEvent(keyName, press: true);
    }

    /// <summary>Simulates releasing a keyboard key.</summary>
    internal static void KeyUp(string keyName)
    {
        if (OperatingSystem.IsLinux())
            LinuxKeyEvent(keyName, press: false);
        else if (OperatingSystem.IsWindows())
            WindowsKeyEvent(keyName, press: false);
        else if (OperatingSystem.IsMacOS())
            MacKeyEvent(keyName, press: false);
    }

    /// <summary>Simulates a full key press (down + up).</summary>
    internal static void KeyPress(string keyName)
    {
        KeyDown(keyName);
        KeyUp(keyName);
    }

    /// <summary>Simulates a mouse button down event.</summary>
    internal static void MouseDown(MouseBtn button)
    {
        if (OperatingSystem.IsLinux())
            LinuxMouseButton(button, press: true);
        else if (OperatingSystem.IsWindows())
            WindowsMouseButton(button, press: true);
        else if (OperatingSystem.IsMacOS())
            MacMouseButton(button, press: true);
    }

    /// <summary>Simulates a mouse button up event.</summary>
    internal static void MouseUp(MouseBtn button)
    {
        if (OperatingSystem.IsLinux())
            LinuxMouseButton(button, press: false);
        else if (OperatingSystem.IsWindows())
            WindowsMouseButton(button, press: false);
        else if (OperatingSystem.IsMacOS())
            MacMouseButton(button, press: false);
    }

    /// <summary>Moves the mouse cursor by relative offset.</summary>
    internal static void MouseMoveRelative(int dx, int dy)
    {
        if (OperatingSystem.IsLinux())
            LinuxMouseMove(dx, dy);
        else if (OperatingSystem.IsWindows())
            WindowsMouseMove(dx, dy);
        else if (OperatingSystem.IsMacOS())
            MacMouseMove(dx, dy);
    }

    // ════════════════════════════════════════════════════════════════════
    // Windows implementation – SendInput API
    // ════════════════════════════════════════════════════════════════════

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public KEYBDINPUT ki;
        [FieldOffset(8)] public MOUSEINPUT mi;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint INPUT_MOUSE = 0;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_XDOWN = 0x0080;
    private const uint MOUSEEVENTF_XUP = 0x0100;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint XBUTTON1 = 0x0001;
    private const uint XBUTTON2 = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private static void WindowsKeyEvent(string keyName, bool press)
    {
        ushort vk = KeyNameToVirtualKey(keyName);
        if (vk == 0) return;

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = press ? 0u : KEYEVENTF_KEYUP
            }
        };

        try { SendInput(1, [input], Marshal.SizeOf<INPUT>()); }
        catch (DllNotFoundException) { }
    }

    private static void WindowsMouseButton(MouseBtn button, bool press)
    {
        uint flags = button switch
        {
            MouseBtn.Left => press ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
            MouseBtn.Right => press ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            MouseBtn.Middle => press ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            MouseBtn.Back => press ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP,
            MouseBtn.Forward => press ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP,
            _ => 0
        };

        uint mouseData = button switch
        {
            MouseBtn.Back => XBUTTON1,
            MouseBtn.Forward => XBUTTON2,
            _ => 0
        };

        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT { dwFlags = flags, mouseData = mouseData }
        };

        try { SendInput(1, [input], Marshal.SizeOf<INPUT>()); }
        catch (DllNotFoundException) { }
    }

    private static void WindowsMouseMove(int dx, int dy)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = MOUSEEVENTF_MOVE }
        };

        try { SendInput(1, [input], Marshal.SizeOf<INPUT>()); }
        catch (DllNotFoundException) { }
    }

    // ════════════════════════════════════════════════════════════════════
    // Linux implementation – xdotool CLI (widely available)
    // ════════════════════════════════════════════════════════════════════

    private static void LinuxKeyEvent(string keyName, bool press)
    {
        string xdoKey = KeyNameToXdotool(keyName);
        string action = press ? "keydown" : "keyup";
        RunProcess("xdotool", $"{action} {xdoKey}");
    }

    private static void LinuxMouseButton(MouseBtn button, bool press)
    {
        int btn = button switch
        {
            MouseBtn.Left => 1,
            MouseBtn.Middle => 2,
            MouseBtn.Right => 3,
            MouseBtn.Back => 8,
            MouseBtn.Forward => 9,
            _ => 1
        };
        string action = press ? "mousedown" : "mouseup";
        RunProcess("xdotool", $"{action} {btn}");
    }

    private static void LinuxMouseMove(int dx, int dy)
    {
        RunProcess("xdotool", $"mousemove_relative -- {dx} {dy}");
    }

    // ════════════════════════════════════════════════════════════════════
    // macOS implementation – CGEvent via CoreGraphics
    // ════════════════════════════════════════════════════════════════════

    // CGEventSourceRef, CGEventRef types
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.Bool)] bool keyDown);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventPost(int tap, IntPtr eventRef);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    private const int kCGHIDEventTap = 0;

    private static void MacKeyEvent(string keyName, bool press)
    {
        ushort code = KeyNameToMacKeyCode(keyName);
        try
        {
            IntPtr evt = CGEventCreateKeyboardEvent(IntPtr.Zero, code, press);
            if (evt != IntPtr.Zero)
            {
                CGEventPost(kCGHIDEventTap, evt);
                CFRelease(evt);
            }
        }
        catch (DllNotFoundException) { }
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreateMouseEvent(
        IntPtr source, int mouseType, CGPoint mouseCursorPosition, int mouseButton);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGPoint CGEventGetLocation(IntPtr eventRef);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double x;
        public double y;
    }

    private const int kCGEventLeftMouseDown = 1;
    private const int kCGEventLeftMouseUp = 2;
    private const int kCGEventRightMouseDown = 3;
    private const int kCGEventRightMouseUp = 4;
    private const int kCGEventOtherMouseDown = 25;
    private const int kCGEventOtherMouseUp = 26;
    private const int kCGEventMouseMoved = 5;

    private static void MacMouseButton(MouseBtn button, bool press)
    {
        try
        {
            // Get current cursor position
            IntPtr curEvt = CGEventCreate(IntPtr.Zero);
            if (curEvt == IntPtr.Zero) return;
            CGPoint pos = CGEventGetLocation(curEvt);
            CFRelease(curEvt);

            int mouseType = button switch
            {
                MouseBtn.Left => press ? kCGEventLeftMouseDown : kCGEventLeftMouseUp,
                MouseBtn.Right => press ? kCGEventRightMouseDown : kCGEventRightMouseUp,
                _ => press ? kCGEventOtherMouseDown : kCGEventOtherMouseUp
            };

            int btn = button switch
            {
                MouseBtn.Left => 0,
                MouseBtn.Right => 1,
                MouseBtn.Middle => 2,
                MouseBtn.Back => 3,
                MouseBtn.Forward => 4,
                _ => 0
            };

            IntPtr evt = CGEventCreateMouseEvent(IntPtr.Zero, mouseType, pos, btn);
            if (evt != IntPtr.Zero)
            {
                CGEventPost(kCGHIDEventTap, evt);
                CFRelease(evt);
            }
        }
        catch (DllNotFoundException) { }
    }

    private static void MacMouseMove(int dx, int dy)
    {
        try
        {
            IntPtr curEvt = CGEventCreate(IntPtr.Zero);
            if (curEvt == IntPtr.Zero) return;
            CGPoint pos = CGEventGetLocation(curEvt);
            CFRelease(curEvt);

            pos.x += dx;
            pos.y += dy;

            IntPtr evt = CGEventCreateMouseEvent(IntPtr.Zero, kCGEventMouseMoved, pos, 0);
            if (evt != IntPtr.Zero)
            {
                CGEventPost(kCGHIDEventTap, evt);
                CFRelease(evt);
            }
        }
        catch (DllNotFoundException) { }
    }

    // ════════════════════════════════════════════════════════════════════
    // Helper: run a process silently
    // ════════════════════════════════════════════════════════════════════

    private static void RunProcess(string fileName, string arguments)
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                },
                EnableRaisingEvents = true
            };
            proc.Exited += (_, _) => proc.Dispose();
            proc.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            Trace.WriteLine($"[InputSimulator] Failed to run {fileName}: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Key name translation tables
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Translates a platform-independent key name to a Windows Virtual Key code.
    /// Covers letters, numbers, function keys, modifiers, arrows, and common keys.
    /// </summary>
    internal static ushort KeyNameToVirtualKey(string keyName) => keyName.ToUpperInvariant() switch
    {
        // Letters A-Z (VK 0x41 – 0x5A)
        "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45,
        "F" => 0x46, "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A,
        "K" => 0x4B, "L" => 0x4C, "M" => 0x4D, "N" => 0x4E, "O" => 0x4F,
        "P" => 0x50, "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
        "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58, "Y" => 0x59, "Z" => 0x5A,

        // Numbers 0-9 (VK 0x30 – 0x39)
        "0" or "D0" => 0x30, "1" or "D1" => 0x31, "2" or "D2" => 0x32,
        "3" or "D3" => 0x33, "4" or "D4" => 0x34, "5" or "D5" => 0x35,
        "6" or "D6" => 0x36, "7" or "D7" => 0x37, "8" or "D8" => 0x38, "9" or "D9" => 0x39,

        // Function keys
        "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
        "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
        "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,

        // Modifiers
        "LEFTSHIFT" or "LSHIFT" => 0xA0, "RIGHTSHIFT" or "RSHIFT" => 0xA1,
        "LEFTCONTROL" or "LCTRL" or "LEFTCTRL" => 0xA2,
        "RIGHTCONTROL" or "RCTRL" or "RIGHTCTRL" => 0xA3,
        "LEFTALT" or "LALT" => 0xA4, "RIGHTALT" or "RALT" => 0xA5,
        "SHIFT" => 0x10, "CONTROL" or "CTRL" => 0x11, "ALT" => 0x12,

        // Navigation
        "UP" or "UPARROW" => 0x26, "DOWN" or "DOWNARROW" => 0x28,
        "LEFT" or "LEFTARROW" => 0x25, "RIGHT" or "RIGHTARROW" => 0x27,
        "HOME" => 0x24, "END" => 0x23,
        "PAGEUP" or "PGUP" => 0x21, "PAGEDOWN" or "PGDN" => 0x22,

        // Common
        "SPACE" => 0x20, "ENTER" or "RETURN" => 0x0D, "ESCAPE" or "ESC" => 0x1B,
        "TAB" => 0x09, "BACKSPACE" or "BACK" => 0x08, "DELETE" or "DEL" => 0x2E,
        "INSERT" or "INS" => 0x2D,
        "CAPSLOCK" => 0x14, "NUMLOCK" => 0x90, "SCROLLLOCK" => 0x91,
        "PRINTSCREEN" or "PRTSC" => 0x2C, "PAUSE" => 0x13,

        // Punctuation
        "SEMICOLON" or "OEM1" => 0xBA, "EQUALS" or "OEMPLUS" => 0xBB,
        "COMMA" or "OEMCOMMA" => 0xBC, "MINUS" or "OEMMINUS" => 0xBD,
        "PERIOD" or "OEMPERIOD" => 0xBE, "SLASH" or "OEM2" => 0xBF,
        "BACKQUOTE" or "TILDE" or "OEM3" => 0xC0,
        "LEFTBRACKET" or "OEM4" => 0xDB, "BACKSLASH" or "OEM5" => 0xDC,
        "RIGHTBRACKET" or "OEM6" => 0xDD, "QUOTE" or "OEM7" => 0xDE,

        // Numpad
        "NUMPAD0" => 0x60, "NUMPAD1" => 0x61, "NUMPAD2" => 0x62,
        "NUMPAD3" => 0x63, "NUMPAD4" => 0x64, "NUMPAD5" => 0x65,
        "NUMPAD6" => 0x66, "NUMPAD7" => 0x67, "NUMPAD8" => 0x68, "NUMPAD9" => 0x69,
        "NUMPADMULTIPLY" or "MULTIPLY" => 0x6A, "NUMPADADD" or "ADD" => 0x6B,
        "NUMPADSUBTRACT" or "SUBTRACT" => 0x6D, "NUMPADDECIMAL" or "DECIMAL" => 0x6E,
        "NUMPADDIVIDE" or "DIVIDE" => 0x6F,

        _ => 0
    };

    /// <summary>Translates a key name to xdotool key identifier.</summary>
    internal static string KeyNameToXdotool(string keyName) => keyName.ToUpperInvariant() switch
    {
        "SPACE" => "space", "ENTER" or "RETURN" => "Return", "ESCAPE" or "ESC" => "Escape",
        "TAB" => "Tab", "BACKSPACE" or "BACK" => "BackSpace", "DELETE" or "DEL" => "Delete",
        "INSERT" or "INS" => "Insert",
        "UP" or "UPARROW" => "Up", "DOWN" or "DOWNARROW" => "Down",
        "LEFT" or "LEFTARROW" => "Left", "RIGHT" or "RIGHTARROW" => "Right",
        "HOME" => "Home", "END" => "End",
        "PAGEUP" or "PGUP" => "Prior", "PAGEDOWN" or "PGDN" => "Next",
        "LEFTSHIFT" or "LSHIFT" or "SHIFT" => "Shift_L",
        "RIGHTSHIFT" or "RSHIFT" => "Shift_R",
        "LEFTCONTROL" or "LCTRL" or "LEFTCTRL" or "CONTROL" or "CTRL" => "Control_L",
        "RIGHTCONTROL" or "RCTRL" or "RIGHTCTRL" => "Control_R",
        "LEFTALT" or "LALT" or "ALT" => "Alt_L",
        "RIGHTALT" or "RALT" => "Alt_R",
        "CAPSLOCK" => "Caps_Lock", "NUMLOCK" => "Num_Lock", "SCROLLLOCK" => "Scroll_Lock",
        "PRINTSCREEN" or "PRTSC" => "Print", "PAUSE" => "Pause",
        "F1" => "F1", "F2" => "F2", "F3" => "F3", "F4" => "F4",
        "F5" => "F5", "F6" => "F6", "F7" => "F7", "F8" => "F8",
        "F9" => "F9", "F10" => "F10", "F11" => "F11", "F12" => "F12",
        "SEMICOLON" or "OEM1" => "semicolon", "EQUALS" or "OEMPLUS" => "equal",
        "COMMA" or "OEMCOMMA" => "comma", "MINUS" or "OEMMINUS" => "minus",
        "PERIOD" or "OEMPERIOD" => "period", "SLASH" or "OEM2" => "slash",
        "BACKQUOTE" or "TILDE" or "OEM3" => "grave",
        "LEFTBRACKET" or "OEM4" => "bracketleft", "BACKSLASH" or "OEM5" => "backslash",
        "RIGHTBRACKET" or "OEM6" => "bracketright", "QUOTE" or "OEM7" => "apostrophe",
        // Numpad
        "NUMPAD0" => "KP_0", "NUMPAD1" => "KP_1", "NUMPAD2" => "KP_2",
        "NUMPAD3" => "KP_3", "NUMPAD4" => "KP_4", "NUMPAD5" => "KP_5",
        "NUMPAD6" => "KP_6", "NUMPAD7" => "KP_7", "NUMPAD8" => "KP_8", "NUMPAD9" => "KP_9",
        "NUMPADMULTIPLY" or "MULTIPLY" => "KP_Multiply",
        "NUMPADADD" or "ADD" => "KP_Add",
        "NUMPADSUBTRACT" or "SUBTRACT" => "KP_Subtract",
        "NUMPADDECIMAL" or "DECIMAL" => "KP_Decimal",
        "NUMPADDIVIDE" or "DIVIDE" => "KP_Divide",
        _ => keyName.Length == 1 ? keyName.ToLowerInvariant() : keyName
    };

    /// <summary>Translates a key name to a macOS virtual key code.</summary>
    internal static ushort KeyNameToMacKeyCode(string keyName) => keyName.ToUpperInvariant() switch
    {
        "A" => 0x00, "B" => 0x0B, "C" => 0x08, "D" => 0x02, "E" => 0x0E,
        "F" => 0x03, "G" => 0x05, "H" => 0x04, "I" => 0x22, "J" => 0x26,
        "K" => 0x28, "L" => 0x25, "M" => 0x2E, "N" => 0x2D, "O" => 0x1F,
        "P" => 0x23, "Q" => 0x0C, "R" => 0x0F, "S" => 0x01, "T" => 0x11,
        "U" => 0x20, "V" => 0x09, "W" => 0x0D, "X" => 0x07, "Y" => 0x10, "Z" => 0x06,
        "0" or "D0" => 0x1D, "1" or "D1" => 0x12, "2" or "D2" => 0x13,
        "3" or "D3" => 0x14, "4" or "D4" => 0x15, "5" or "D5" => 0x17,
        "6" or "D6" => 0x16, "7" or "D7" => 0x1A, "8" or "D8" => 0x1C, "9" or "D9" => 0x19,
        "F1" => 0x7A, "F2" => 0x78, "F3" => 0x63, "F4" => 0x76,
        "F5" => 0x60, "F6" => 0x61, "F7" => 0x62, "F8" => 0x64,
        "F9" => 0x65, "F10" => 0x6D, "F11" => 0x67, "F12" => 0x6F,
        "SPACE" => 0x31, "ENTER" or "RETURN" => 0x24, "ESCAPE" or "ESC" => 0x35,
        "TAB" => 0x30, "BACKSPACE" or "BACK" => 0x33, "DELETE" or "DEL" => 0x75,
        "UP" or "UPARROW" => 0x7E, "DOWN" or "DOWNARROW" => 0x7D,
        "LEFT" or "LEFTARROW" => 0x7B, "RIGHT" or "RIGHTARROW" => 0x7C,
        "HOME" => 0x73, "END" => 0x77,
        "PAGEUP" or "PGUP" => 0x74, "PAGEDOWN" or "PGDN" => 0x79,
        "LEFTSHIFT" or "LSHIFT" or "SHIFT" => 0x38,
        "RIGHTSHIFT" or "RSHIFT" => 0x3C,
        "LEFTCONTROL" or "LCTRL" or "LEFTCTRL" or "CONTROL" or "CTRL" => 0x3B,
        "RIGHTCONTROL" or "RCTRL" or "RIGHTCTRL" => 0x3E,
        "LEFTALT" or "LALT" or "ALT" => 0x3A,
        "RIGHTALT" or "RALT" => 0x3D,
        "CAPSLOCK" => 0x39,
        "INSERT" or "INS" => 0x72,         // Fn+Delete on Mac keyboards
        "PRINTSCREEN" or "PRTSC" => 0x69,  // F13 (closest Mac equivalent)
        "SCROLLLOCK" => 0x6B,              // F14
        "PAUSE" => 0x71,                   // F15
        "NUMLOCK" => 0x47,                 // Clear key on Mac numpad
        // Numpad
        "NUMPAD0" => 0x52, "NUMPAD1" => 0x53, "NUMPAD2" => 0x54,
        "NUMPAD3" => 0x55, "NUMPAD4" => 0x56, "NUMPAD5" => 0x57,
        "NUMPAD6" => 0x58, "NUMPAD7" => 0x59, "NUMPAD8" => 0x5B, "NUMPAD9" => 0x5C,
        "NUMPADMULTIPLY" or "MULTIPLY" => 0x43,
        "NUMPADADD" or "ADD" => 0x45,
        "NUMPADSUBTRACT" or "SUBTRACT" => 0x4E,
        "NUMPADDECIMAL" or "DECIMAL" => 0x41,
        "NUMPADDIVIDE" or "DIVIDE" => 0x4B,
        _ => 0xFF
    };

    /// <summary>
    /// Returns the complete list of supported key names for UI display.
    /// </summary>
    internal static IReadOnlyList<string> GetSupportedKeyNames() =>
    [
        // Letters
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        // Numbers
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        // Function keys
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        // Modifiers
        "LeftShift", "RightShift", "LeftControl", "RightControl", "LeftAlt", "RightAlt",
        // Navigation
        "Up", "Down", "Left", "Right", "Home", "End", "PageUp", "PageDown",
        // Common
        "Space", "Enter", "Escape", "Tab", "Backspace", "Delete", "Insert",
        "CapsLock", "NumLock", "ScrollLock", "PrintScreen", "Pause",
        // Punctuation
        "Semicolon", "Equals", "Comma", "Minus", "Period", "Slash",
        "Backquote", "LeftBracket", "Backslash", "RightBracket", "Quote",
        // Numpad
        "Numpad0", "Numpad1", "Numpad2", "Numpad3", "Numpad4",
        "Numpad5", "Numpad6", "Numpad7", "Numpad8", "Numpad9",
        "NumpadMultiply", "NumpadAdd", "NumpadSubtract", "NumpadDecimal", "NumpadDivide"
    ];
}
