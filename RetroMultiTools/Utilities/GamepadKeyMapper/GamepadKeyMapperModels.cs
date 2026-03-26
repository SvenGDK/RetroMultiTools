using System.Text.Json.Serialization;

namespace RetroMultiTools.Utilities.GamepadKeyMapper;

// ── Action types ────────────────────────────────────────────────────────

/// <summary>Discriminator for the polymorphic <see cref="MappingAction"/> hierarchy.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MappingActionKind
{
    Keyboard,
    MouseButton,
    MouseMove,
    Script,
    Macro
}

/// <summary>Base class for all actions that a gamepad input can trigger.</summary>
[JsonDerivedType(typeof(KeyboardAction), "Keyboard")]
[JsonDerivedType(typeof(MouseButtonAction), "MouseButton")]
[JsonDerivedType(typeof(MouseMoveAction), "MouseMove")]
[JsonDerivedType(typeof(ScriptAction), "Script")]
[JsonDerivedType(typeof(MacroAction), "Macro")]
public abstract class MappingAction
{
    public abstract MappingActionKind Kind { get; }

    /// <summary>Human-readable summary shown in the UI.</summary>
    public abstract string DisplayText { get; }
}

/// <summary>Simulates a keyboard key press/release.</summary>
public sealed class KeyboardAction : MappingAction
{
    public override MappingActionKind Kind => MappingActionKind.Keyboard;

    /// <summary>Platform-independent key name (e.g. "A", "Space", "LeftShift", "F5").</summary>
    public string KeyName { get; set; } = string.Empty;

    public override string DisplayText => $"Key: {KeyName}";
}

/// <summary>Simulates a mouse button click.</summary>
public sealed class MouseButtonAction : MappingAction
{
    public override MappingActionKind Kind => MappingActionKind.MouseButton;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MouseBtn Button { get; set; }

    public override string DisplayText => $"Mouse: {Button}";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MouseBtn { Left, Right, Middle, Back, Forward }

/// <summary>Moves the mouse cursor by a relative offset (typically bound to an analog stick).</summary>
public sealed class MouseMoveAction : MappingAction
{
    public override MappingActionKind Kind => MappingActionKind.MouseMove;

    /// <summary>Horizontal pixels per tick (positive = right).</summary>
    public int DeltaX { get; set; }

    /// <summary>Vertical pixels per tick (positive = down).</summary>
    public int DeltaY { get; set; }

    /// <summary>Speed multiplier applied to analog stick deflection (1–20).</summary>
    public int Speed { get; set; } = 10;

    public override string DisplayText =>
        DeltaX != 0 || DeltaY != 0
            ? $"Mouse Move: ({DeltaX},{DeltaY})"
            : $"Mouse Move (speed {Speed})";
}

/// <summary>Launches an external script or executable.</summary>
public sealed class ScriptAction : MappingAction
{
    public override MappingActionKind Kind => MappingActionKind.Script;

    /// <summary>Full path to the script or executable.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Optional command-line arguments.</summary>
    public string Arguments { get; set; } = string.Empty;

    public override string DisplayText =>
        string.IsNullOrEmpty(Arguments)
            ? $"Run: {Path.GetFileName(FilePath)}"
            : $"Run: {Path.GetFileName(FilePath)} {Arguments}";
}

/// <summary>Executes a sequence of actions with optional delays between them.</summary>
public sealed class MacroAction : MappingAction
{
    public override MappingActionKind Kind => MappingActionKind.Macro;

    /// <summary>Ordered steps making up this macro.</summary>
    public List<MacroStep> Steps { get; set; } = [];

    public override string DisplayText => $"Macro ({Steps.Count} steps)";
}

/// <summary>A single step inside a <see cref="MacroAction"/>.</summary>
public sealed class MacroStep
{
    /// <summary>The action to execute at this step.</summary>
    public MappingAction Action { get; set; } = new KeyboardAction();

    /// <summary>Delay in milliseconds after this step before the next one executes.</summary>
    public int DelayMs { get; set; } = 50;
}

// ── Gamepad input identifiers ───────────────────────────────────────────

/// <summary>Identifies a specific physical input on a gamepad.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GamepadInput
{
    // Buttons
    ButtonA, ButtonB, ButtonX, ButtonY,
    ButtonBack, ButtonGuide, ButtonStart,
    ButtonLeftStick, ButtonRightStick,
    ButtonLeftShoulder, ButtonRightShoulder,
    DPadUp, DPadDown, DPadLeft, DPadRight,

    // Axes (each direction is a separate binding)
    LeftStickUp, LeftStickDown, LeftStickLeft, LeftStickRight,
    RightStickUp, RightStickDown, RightStickLeft, RightStickRight,
    LeftTrigger, RightTrigger
}

// ── Mapping, set, profile ───────────────────────────────────────────────

/// <summary>A single binding from a gamepad input to an action.</summary>
public sealed class GamepadKeyMapping
{
    public GamepadInput Input { get; set; }
    public MappingAction Action { get; set; } = new KeyboardAction();
}

/// <summary>
/// A named set of mappings. A profile can contain multiple sets that the
/// user can cycle through at runtime (e.g. with a hotkey).
/// </summary>
public sealed class GamepadMappingSet
{
    public string Name { get; set; } = "Set 1";
    public List<GamepadKeyMapping> Mappings { get; set; } = [];
}

/// <summary>
/// A complete gamepad mapping profile that can be saved, loaded, and
/// switched between. Each profile targets a specific controller GUID
/// (or "*" for any controller) and contains one or more sets.
/// </summary>
public sealed class GamepadKeyMapperProfile
{
    /// <summary>User-visible profile name.</summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Controller GUID this profile is designed for, or "*" for any controller.
    /// </summary>
    public string ControllerGuid { get; set; } = "*";

    /// <summary>The switchable mapping sets (at least one).</summary>
    public List<GamepadMappingSet> Sets { get; set; } = [new GamepadMappingSet()];

    /// <summary>Index of the currently active set (persisted across saves).</summary>
    public int ActiveSetIndex { get; set; }
}

// ── Auto-profile rules ─────────────────────────────────────────────────

/// <summary>
/// Binds a <see cref="GamepadKeyMapperProfile"/> to an application window
/// so the profile activates automatically when that window gains focus.
/// </summary>
public sealed class AutoProfileRule
{
    /// <summary>Window title substring to match (case-insensitive).</summary>
    public string WindowTitleMatch { get; set; } = string.Empty;

    /// <summary>Optional process name to match (without extension, case-insensitive).</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Name of the profile to activate when the rule matches.</summary>
    public string ProfileName { get; set; } = string.Empty;
}

/// <summary>
/// Top-level configuration persisted to disk. Contains all profiles and
/// auto-profile rules plus the name of the last active profile.
/// </summary>
public sealed class GamepadKeyMapperConfig
{
    public List<GamepadKeyMapperProfile> Profiles { get; set; } = [new GamepadKeyMapperProfile()];
    public List<AutoProfileRule> AutoProfileRules { get; set; } = [];
    public string LastActiveProfile { get; set; } = "Default";

    /// <summary>Persisted stick dead-zone value (0.05–0.95, default 0.25).</summary>
    public double DeadZone { get; set; } = 0.25;
}
