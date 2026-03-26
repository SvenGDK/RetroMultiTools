using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities.GamepadKeyMapper;

namespace RetroMultiTools.Views;

/// <summary>
/// Step-by-step wizard that guides the user through creating a new mapping
/// profile with common gamepad-to-keyboard/mouse bindings.
/// </summary>
public partial class GamepadKeyMapperWizardWindow : Window
{
    private const int TotalSteps = 5;
    private int _currentStep = 1;

    private readonly StackPanel[] _stepPanels;
    private readonly Ellipse[] _dots;

    // "None" sentinel prepended to every key combo so the user can skip a binding.
    private static string NoneLabel => LocalizationManager.Instance["GpMapper_WizardNone"];

    public GamepadKeyMapperWizardWindow()
    {
        InitializeComponent();

        _stepPanels = [Step1Panel, Step2Panel, Step3Panel, Step4Panel, Step5Panel];
        _dots = [Dot1, Dot2, Dot3, Dot4, Dot5];

        // Build key list with a "None" option at the top
        var keys = new List<string> { NoneLabel };
        keys.AddRange(InputSimulator.GetSupportedKeyNames());

        // Face buttons (Step 2)
        InitKeyCombo(BtnACombo, keys, "Space");
        InitKeyCombo(BtnBCombo, keys, "LeftShift");
        InitKeyCombo(BtnXCombo, keys, "E");
        InitKeyCombo(BtnYCombo, keys, "R");

        // Triggers & shoulders (Step 5)
        InitKeyCombo(LBCombo, keys, "Q");
        InitKeyCombo(RBCombo, keys, "E");
        InitKeyCombo(LTCombo, keys, "LeftControl");
        InitKeyCombo(RTCombo, keys, "Space");
        InitKeyCombo(StartCombo, keys, "Escape");
        InitKeyCombo(BackCombo, keys, "Tab");

        ShowStep(1);
    }

    // ── Step navigation ─────────────────────────────────────────────────

    private void ShowStep(int step)
    {
        _currentStep = step;

        for (int i = 0; i < TotalSteps; i++)
        {
            _stepPanels[i].IsVisible = i == step - 1;
            _dots[i].Fill = i < step
                ? new SolidColorBrush(Color.Parse("#89B4FA"))
                : new SolidColorBrush(Color.Parse("#45475A"));
        }

        var loc = LocalizationManager.Instance;
        StepTitle.Text = loc[$"GpMapper_WizardStep{step}Title"];
        StepDescription.Text = loc[$"GpMapper_WizardStep{step}Desc"];

        BackButton.IsVisible = step > 1;
        NextButton.IsVisible = step < TotalSteps;
        FinishButton.IsVisible = step == TotalSteps;
    }

    private void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        // Validate step 1: profile name must not be empty
        if (_currentStep == 1 && string.IsNullOrWhiteSpace(ProfileNameBox.Text))
            return;

        if (_currentStep < TotalSteps)
            ShowStep(_currentStep + 1);
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
            ShowStep(_currentStep - 1);
    }

    private void FinishButton_Click(object? sender, RoutedEventArgs e)
    {
        var profile = BuildProfile();
        Close(profile);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(null as GamepadKeyMapperProfile);
    }

    // ── Profile builder ─────────────────────────────────────────────────

    private GamepadKeyMapperProfile BuildProfile()
    {
        var mappings = new List<GamepadKeyMapping>();

        // Step 2: Face buttons
        AddKeyMapping(mappings, GamepadInput.ButtonA, BtnACombo);
        AddKeyMapping(mappings, GamepadInput.ButtonB, BtnBCombo);
        AddKeyMapping(mappings, GamepadInput.ButtonX, BtnXCombo);
        AddKeyMapping(mappings, GamepadInput.ButtonY, BtnYCombo);

        // Step 3: D-Pad
        if (DPadArrows.IsChecked == true)
        {
            AddFixedKeyMapping(mappings, GamepadInput.DPadUp, "Up");
            AddFixedKeyMapping(mappings, GamepadInput.DPadDown, "Down");
            AddFixedKeyMapping(mappings, GamepadInput.DPadLeft, "Left");
            AddFixedKeyMapping(mappings, GamepadInput.DPadRight, "Right");
        }
        else if (DPadWASD.IsChecked == true)
        {
            AddFixedKeyMapping(mappings, GamepadInput.DPadUp, "W");
            AddFixedKeyMapping(mappings, GamepadInput.DPadDown, "S");
            AddFixedKeyMapping(mappings, GamepadInput.DPadLeft, "A");
            AddFixedKeyMapping(mappings, GamepadInput.DPadRight, "D");
        }

        // Step 4: Left Stick
        if (StickWASD.IsChecked == true)
        {
            AddFixedKeyMapping(mappings, GamepadInput.LeftStickUp, "W");
            AddFixedKeyMapping(mappings, GamepadInput.LeftStickDown, "S");
            AddFixedKeyMapping(mappings, GamepadInput.LeftStickLeft, "A");
            AddFixedKeyMapping(mappings, GamepadInput.LeftStickRight, "D");
        }
        else if (StickArrows.IsChecked == true)
        {
            AddFixedKeyMapping(mappings, GamepadInput.LeftStickUp, "Up");
            AddFixedKeyMapping(mappings, GamepadInput.LeftStickDown, "Down");
            AddFixedKeyMapping(mappings, GamepadInput.LeftStickLeft, "Left");
            AddFixedKeyMapping(mappings, GamepadInput.LeftStickRight, "Right");
        }
        else if (StickMouse.IsChecked == true)
        {
            foreach (var stickDir in new[] { GamepadInput.LeftStickUp, GamepadInput.LeftStickDown,
                                              GamepadInput.LeftStickLeft, GamepadInput.LeftStickRight })
            {
                mappings.Add(new GamepadKeyMapping
                {
                    Input = stickDir,
                    Action = new MouseMoveAction { Speed = 10 }
                });
            }
        }

        // Step 5: Triggers & shoulders
        AddKeyMapping(mappings, GamepadInput.ButtonLeftShoulder, LBCombo);
        AddKeyMapping(mappings, GamepadInput.ButtonRightShoulder, RBCombo);
        AddKeyMapping(mappings, GamepadInput.LeftTrigger, LTCombo);
        AddKeyMapping(mappings, GamepadInput.RightTrigger, RTCombo);
        AddKeyMapping(mappings, GamepadInput.ButtonStart, StartCombo);
        AddKeyMapping(mappings, GamepadInput.ButtonBack, BackCombo);

        var profile = new GamepadKeyMapperProfile
        {
            Name = ProfileNameBox.Text?.Trim() ?? "My Profile",
            Sets =
            [
                new GamepadMappingSet
                {
                    Name = "Set 1",
                    Mappings = mappings
                }
            ]
        };

        return profile;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void InitKeyCombo(ComboBox combo, List<string> keys, string defaultKey)
    {
        combo.ItemsSource = keys;
        int idx = keys.IndexOf(defaultKey);
        combo.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void AddKeyMapping(List<GamepadKeyMapping> mappings, GamepadInput input, ComboBox combo)
    {
        if (combo.SelectedItem is not string key || key == NoneLabel) return;

        mappings.Add(new GamepadKeyMapping
        {
            Input = input,
            Action = new KeyboardAction { KeyName = key }
        });
    }

    private static void AddFixedKeyMapping(List<GamepadKeyMapping> mappings, GamepadInput input, string keyName)
    {
        mappings.Add(new GamepadKeyMapping
        {
            Input = input,
            Action = new KeyboardAction { KeyName = keyName }
        });
    }
}
