using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RetroMultiTools.Localization;
using RetroMultiTools.Utilities.GamepadKeyMapper;

namespace RetroMultiTools.Views;

public partial class GamepadKeyMapperView : UserControl
{
    private readonly GamepadKeyMapperEngine _engine = GamepadKeyMapperEngine.Instance;
    private GamepadInput _assigningInput;

    public GamepadKeyMapperView()
    {
        InitializeComponent();

        // Populate key combo (static data – only needs to happen once)
        KeyCombo.ItemsSource = InputSimulator.GetSupportedKeyNames();
        if (KeyCombo.ItemCount > 0)
            KeyCombo.SelectedIndex = 0;

        // Subscribe / unsubscribe engine events when the view enters /
        // leaves the visual tree so we avoid needless UI updates while
        // the view is hidden and prevent stale handler accumulation.
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
    }

    private void OnAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _engine.RunningStateChanged += OnRunningStateChanged;
        _engine.ProfileChanged += OnProfileChanged;
        _engine.InputDetected += OnInputDetected;
        _engine.ControllerStatusChanged += OnControllerStatusChanged;

        // Sync UI with current engine state (may have changed while detached)
        OnRunningStateChanged(_engine.IsRunning);
        DeadZoneSlider.Value = _engine.DeadZone;
        DeadZoneValueText.Text = _engine.DeadZone.ToString("F2");

        RefreshProfileCombo();
        RefreshSetList();
        RefreshMappingList();
        RefreshAutoProfileList();
    }

    private void OnDetached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _engine.RunningStateChanged -= OnRunningStateChanged;
        _engine.ProfileChanged -= OnProfileChanged;
        _engine.InputDetected -= OnInputDetected;
        _engine.ControllerStatusChanged -= OnControllerStatusChanged;
    }

    // ── Engine start / stop ─────────────────────────────────────────────

    private void StartStopButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_engine.IsRunning)
            _engine.Stop();
        else
            _engine.Start();
    }

    private void OnRunningStateChanged(bool running)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            StatusDot.Fill = running
                ? new SolidColorBrush(Color.Parse("#A6E3A1"))
                : new SolidColorBrush(Color.Parse("#F38BA8"));
            StatusText.Text = running
                ? LocalizationManager.Instance["GpMapper_Running"]
                : LocalizationManager.Instance["GpMapper_Stopped"];
            StartStopButton.Content = running
                ? LocalizationManager.Instance["GpMapper_Stop"]
                : LocalizationManager.Instance["GpMapper_Start"];
            CycleSetButton.IsEnabled = running;

            if (!running)
                InputFeedbackText.Text = string.Empty;
        });
    }

    private void OnInputDetected(GamepadInput input)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            string name = GamepadKeyMapperEngine.GetInputDisplayName(input);
            InputFeedbackText.Text = string.Format(
                LocalizationManager.Instance["GpMapper_InputDetected"], name);
        });
    }

    private void OnControllerStatusChanged(bool connected)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            InputFeedbackText.Text = connected
                ? LocalizationManager.Instance["GpMapper_ControllerConnected"]
                : LocalizationManager.Instance["GpMapper_ControllerDisconnected"];
        });
    }

    private void CycleSetButton_Click(object? sender, RoutedEventArgs e)
    {
        _engine.CycleSet();
        _engine.SaveConfig();
        RefreshSetList();
    }

    private void DeadZoneSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (DeadZoneValueText == null) return; // Guard during XAML init
        double value = Math.Round(e.NewValue, 2);
        DeadZoneValueText.Text = value.ToString("F2");
        _engine.SetDeadZone(value);
    }

    // ── Profile management ──────────────────────────────────────────────

    private void RefreshProfileCombo()
    {
        var items = _engine.Config.Profiles.Select(p => p.Name).ToList();
        ProfileCombo.ItemsSource = items;

        var active = _engine.ActiveProfile ?? _engine.Config.Profiles.FirstOrDefault();
        if (active != null)
        {
            int idx = items.IndexOf(active.Name);
            ProfileCombo.SelectedIndex = idx >= 0 ? idx : 0;
        }

        // Also update auto-profile combo
        AutoProfileCombo.ItemsSource = items;
        if (items.Count > 0)
            AutoProfileCombo.SelectedIndex = 0;
    }

    private void ProfileCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is string name)
        {
            _engine.SwitchProfile(name);
            RefreshSetList();
            RefreshMappingList();
        }
    }

    private async void AddProfileButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string name = await PromptTextAsync(
                LocalizationManager.Instance["GpMapper_AddProfile"],
                LocalizationManager.Instance["GpMapper_ProfileNamePrompt"],
                $"Profile {_engine.Config.Profiles.Count + 1}");

            if (string.IsNullOrWhiteSpace(name)) return;

            if (_engine.Config.Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                Trace.WriteLine($"[GamepadKeyMapper] Profile name '{name}' already exists - skipping add.");
                return;
            }

            _engine.AddProfile(new GamepadKeyMapperProfile { Name = name });
            RefreshProfileCombo();
            ProfileCombo.SelectedIndex = ProfileCombo.ItemCount - 1;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapper] Add profile failed: {ex.Message}");
        }
    }

    private async void RenameProfileButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var profile = _engine.ActiveProfile;
            if (profile == null) return;

            string newName = await PromptTextAsync(
                LocalizationManager.Instance["GpMapper_RenameProfile"],
                LocalizationManager.Instance["GpMapper_ProfileNamePrompt"],
                profile.Name);

            if (string.IsNullOrWhiteSpace(newName) || newName == profile.Name) return;

            if (_engine.Config.Profiles.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                Trace.WriteLine($"[GamepadKeyMapper] Profile name '{newName}' already exists - skipping rename.");
                return;
            }

            // Update auto-profile rules that reference this profile
            foreach (var rule in _engine.Config.AutoProfileRules)
            {
                if (rule.ProfileName.Equals(profile.Name, StringComparison.OrdinalIgnoreCase))
                    rule.ProfileName = newName;
            }

            profile.Name = newName;
            _engine.Config.LastActiveProfile = newName;
            _engine.SaveConfig();
            RefreshProfileCombo();
            RefreshAutoProfileList();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapper] Rename profile failed: {ex.Message}");
        }
    }

    private void DeleteProfileButton_Click(object? sender, RoutedEventArgs e)
    {
        var profile = _engine.ActiveProfile;
        if (profile == null || _engine.Config.Profiles.Count <= 1) return;

        _engine.RemoveProfile(profile.Name);
        RefreshProfileCombo();
        RefreshSetList();
        RefreshMappingList();
        RefreshAutoProfileList();
    }

    private async void ExportProfileButton_Click(object? sender, RoutedEventArgs e)
    {
        var profile = _engine.ActiveProfile;
        if (profile == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationManager.Instance["GpMapper_ExportProfile"],
            SuggestedFileName = $"{profile.Name}.json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON") { Patterns = ["*.json"] }
            ]
        });

        if (file == null) return;

        try
        {
            string json = JsonSerializer.Serialize(profile, GamepadKeyMapperEngine.JsonOptions);
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0); // Truncate to avoid leftover data on overwrite
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapper] Export failed: {ex.Message}");
        }
    }

    private async void ImportProfileButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["GpMapper_ImportProfile"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON") { Patterns = ["*.json"] }
            ]
        });

        if (files.Count == 0) return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();

            var profile = JsonSerializer.Deserialize<GamepadKeyMapperProfile>(json,
                GamepadKeyMapperEngine.JsonOptions);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Name)) return;

            EnsureUniqueProfileName(profile);

            _engine.AddProfile(profile);
            RefreshProfileCombo();
            ProfileCombo.SelectedIndex = ProfileCombo.ItemCount - 1;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapper] Import failed: {ex.Message}");
        }
    }

    // ── Mapping Wizard ──────────────────────────────────────────────────

    private async void WizardButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Window parentWindow) return;

            var wizard = new GamepadKeyMapperWizardWindow();
            var profile = await wizard.ShowDialog<GamepadKeyMapperProfile?>(parentWindow);

            if (profile == null) return;

            EnsureUniqueProfileName(profile);

            _engine.AddProfile(profile);
            RefreshProfileCombo();
            ProfileCombo.SelectedIndex = ProfileCombo.ItemCount - 1;
            RefreshSetList();
            RefreshMappingList();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GamepadKeyMapper] Wizard failed: {ex.Message}");
        }
    }

    // ── Set management ──────────────────────────────────────────────────

    private void RefreshSetList()
    {
        var profile = _engine.ActiveProfile;
        if (profile == null)
        {
            SetListBox.ItemsSource = null;
            return;
        }

        SetListBox.ItemsSource = profile.Sets.Select(s => s.Name).ToList();
        if (profile.ActiveSetIndex >= 0 && profile.ActiveSetIndex < profile.Sets.Count)
            SetListBox.SelectedIndex = profile.ActiveSetIndex;
    }

    private void SetListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var profile = _engine.ActiveProfile;
        if (profile == null || SetListBox.SelectedIndex < 0) return;

        profile.ActiveSetIndex = SetListBox.SelectedIndex;
        RefreshMappingList();
    }

    private void AddSetButton_Click(object? sender, RoutedEventArgs e)
    {
        var profile = _engine.ActiveProfile;
        if (profile == null) return;

        profile.Sets.Add(new GamepadMappingSet
        {
            Name = $"Set {profile.Sets.Count + 1}"
        });
        _engine.SaveConfig();
        RefreshSetList();
        SetListBox.SelectedIndex = profile.Sets.Count - 1;
    }

    private void RemoveSetButton_Click(object? sender, RoutedEventArgs e)
    {
        var profile = _engine.ActiveProfile;
        if (profile == null || profile.Sets.Count <= 1) return;

        int idx = SetListBox.SelectedIndex;
        if (idx < 0) return;

        profile.Sets.RemoveAt(idx);
        if (profile.ActiveSetIndex >= profile.Sets.Count)
            profile.ActiveSetIndex = profile.Sets.Count - 1;

        _engine.SaveConfig();
        RefreshSetList();
        RefreshMappingList();
    }

    // ── Mapping list ────────────────────────────────────────────────────

    private void RefreshMappingList()
    {
        var set = _engine.ActiveSet;
        var items = new List<MappingDisplayItem>();

        foreach (GamepadInput input in Enum.GetValues<GamepadInput>())
        {
            var mapping = set?.Mappings.Find(m => m.Input == input);
            items.Add(new MappingDisplayItem
            {
                InputName = GamepadKeyMapperEngine.GetInputDisplayName(input),
                InputValue = input,
                ActionText = mapping?.Action.DisplayText ?? "—"
            });
        }

        MappingList.ItemsSource = items;
    }

    // ── Assign action ───────────────────────────────────────────────────

    private void AssignButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GamepadInput input)
        {
            _assigningInput = input;
            AssignTitle.Text = string.Format(
                LocalizationManager.Instance["GpMapper_AssignTitle"],
                GamepadKeyMapperEngine.GetInputDisplayName(input));
            AssignPanel.IsVisible = true;

            // Pre-select current action type if already mapped
            var set = _engine.ActiveSet;
            var existing = set?.Mappings.Find(m => m.Input == input);
            if (existing != null)
            {
                int typeIdx = existing.Action.Kind switch
                {
                    MappingActionKind.Keyboard => 0,
                    MappingActionKind.MouseButton => 1,
                    MappingActionKind.MouseMove => 2,
                    MappingActionKind.Script => 3,
                    MappingActionKind.Macro => 4,
                    _ => 0
                };
                ActionTypeCombo.SelectedIndex = typeIdx;
                PreFillAssignPanel(existing.Action);
            }
            else
            {
                ActionTypeCombo.SelectedIndex = 0;
            }

            UpdateActionPanelVisibility();
        }
    }

    private void PreFillAssignPanel(MappingAction action)
    {
        switch (action)
        {
            case KeyboardAction ka:
                var keys = InputSimulator.GetSupportedKeyNames();
                int keyIdx = -1;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i].Equals(ka.KeyName, StringComparison.OrdinalIgnoreCase))
                    { keyIdx = i; break; }
                }
                if (keyIdx >= 0) KeyCombo.SelectedIndex = keyIdx;
                break;

            case MouseButtonAction mba:
                string targetTag = mba.Button.ToString();
                for (int i = 0; i < MouseBtnCombo.ItemCount; i++)
                {
                    if (MouseBtnCombo.Items[i] is ComboBoxItem item &&
                        string.Equals(item.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase))
                    {
                        MouseBtnCombo.SelectedIndex = i;
                        break;
                    }
                }
                break;

            case MouseMoveAction mma:
                SpeedSlider.Value = mma.Speed;
                break;

            case ScriptAction sa:
                ScriptPathBox.Text = sa.FilePath;
                ScriptArgsBox.Text = sa.Arguments;
                break;

            case MacroAction ma:
                MacroTextBox.Text = SerializeMacro(ma);
                break;
        }
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GamepadInput input)
        {
            var set = _engine.ActiveSet;
            set?.Mappings.RemoveAll(m => m.Input == input);
            _engine.SaveConfig();
            RefreshMappingList();
        }
    }

    private void ActionTypeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateActionPanelVisibility();
    }

    private void UpdateActionPanelVisibility()
    {
        // Guard: may be called during XAML initialisation before all controls are ready
        if (KeyboardPanel == null) return;

        string? tag = (ActionTypeCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        KeyboardPanel.IsVisible = tag == "Keyboard";
        MouseButtonPanel.IsVisible = tag == "MouseButton";
        MouseMovePanel.IsVisible = tag == "MouseMove";
        ScriptPanel.IsVisible = tag == "Script";
        MacroPanel.IsVisible = tag == "Macro";
    }

    private void SaveAssignment_Click(object? sender, RoutedEventArgs e)
    {
        var set = _engine.ActiveSet;
        if (set == null) return;

        MappingAction? action = BuildActionFromPanel();
        if (action == null) return;

        // Remove any existing mapping for this input
        set.Mappings.RemoveAll(m => m.Input == _assigningInput);
        set.Mappings.Add(new GamepadKeyMapping
        {
            Input = _assigningInput,
            Action = action
        });

        _engine.SaveConfig();
        AssignPanel.IsVisible = false;
        RefreshMappingList();
    }

    private void CancelAssignment_Click(object? sender, RoutedEventArgs e)
    {
        AssignPanel.IsVisible = false;
    }

    private MappingAction? BuildActionFromPanel()
    {
        string? tag = (ActionTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        return tag switch
        {
            "Keyboard" when KeyCombo.SelectedItem is string key =>
                new KeyboardAction { KeyName = key },

            "MouseButton" when MouseBtnCombo.SelectedItem is ComboBoxItem mbi =>
                new MouseButtonAction
                {
                    Button = Enum.TryParse<MouseBtn>(mbi.Tag?.ToString(), out var mb)
                        ? mb : MouseBtn.Left
                },

            "MouseMove" =>
                new MouseMoveAction { Speed = (int)SpeedSlider.Value },

            "Script" when !string.IsNullOrWhiteSpace(ScriptPathBox.Text) =>
                new ScriptAction
                {
                    FilePath = ScriptPathBox.Text ?? string.Empty,
                    Arguments = ScriptArgsBox.Text ?? string.Empty
                },

            "Macro" when !string.IsNullOrWhiteSpace(MacroTextBox.Text) =>
                ParseMacro(MacroTextBox.Text ?? string.Empty),

            _ => null
        };
    }

    private async void BrowseScript_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Instance["GpMapper_ScriptPath"],
            AllowMultiple = false
        });

        if (files.Count > 0)
            ScriptPathBox.Text = files[0].Path.LocalPath;
    }

    // ── Macro serialisation ─────────────────────────────────────────────

    private static MacroAction ParseMacro(string text)
    {
        var macro = new MacroAction();
        foreach (string rawLine in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            int delayMs = 50;
            // Extract delay if present: "Key:A 100ms"
            int msIdx = line.LastIndexOf("ms", StringComparison.OrdinalIgnoreCase);
            if (msIdx > 0)
            {
                int spaceIdx = line.LastIndexOf(' ', msIdx - 1);
                if (spaceIdx > 0 && int.TryParse(line[(spaceIdx + 1)..msIdx], out int d))
                {
                    delayMs = d;
                    line = line[..spaceIdx].Trim();
                }
            }

            MappingAction? stepAction = null;
            if (line.StartsWith("Key:", StringComparison.OrdinalIgnoreCase))
                stepAction = new KeyboardAction { KeyName = line[4..].Trim() };
            else if (line.StartsWith("Mouse:", StringComparison.OrdinalIgnoreCase))
            {
                string btn = line[6..].Trim();
                if (Enum.TryParse<MouseBtn>(btn, true, out var mb))
                    stepAction = new MouseButtonAction { Button = mb };
            }
            else if (line.StartsWith("Run:", StringComparison.OrdinalIgnoreCase))
            {
                string path = line[4..].Trim();
                string args = string.Empty;
                int sp = path.IndexOf(' ');
                if (sp > 0)
                {
                    args = path[(sp + 1)..];
                    path = path[..sp];
                }
                stepAction = new ScriptAction { FilePath = path, Arguments = args };
            }

            if (stepAction != null)
                macro.Steps.Add(new MacroStep { Action = stepAction, DelayMs = delayMs });
        }
        return macro;
    }

    private static string SerializeMacro(MacroAction macro)
    {
        var lines = new List<string>();
        foreach (var step in macro.Steps)
        {
            string prefix = step.Action switch
            {
                KeyboardAction ka => $"Key:{ka.KeyName}",
                MouseButtonAction mba => $"Mouse:{mba.Button}",
                ScriptAction sa => string.IsNullOrEmpty(sa.Arguments)
                    ? $"Run:{sa.FilePath}"
                    : $"Run:{sa.FilePath} {sa.Arguments}",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(prefix))
                lines.Add($"{prefix} {step.DelayMs}ms");
        }
        return string.Join(Environment.NewLine, lines);
    }

    // ── Auto-profile rules ──────────────────────────────────────────────

    private void RefreshAutoProfileList()
    {
        var items = new List<AutoProfileDisplayItem>();
        for (int i = 0; i < _engine.Config.AutoProfileRules.Count; i++)
        {
            var rule = _engine.Config.AutoProfileRules[i];
            items.Add(new AutoProfileDisplayItem
            {
                Index = i,
                DisplayText = FormatRuleDisplay(rule),
                ProfileName = rule.ProfileName
            });
        }
        AutoProfileList.ItemsSource = items;
    }

    private static string FormatRuleDisplay(AutoProfileRule rule)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(rule.WindowTitleMatch))
            parts.Add($"Title: \"{rule.WindowTitleMatch}\"");
        if (!string.IsNullOrWhiteSpace(rule.ProcessName))
            parts.Add($"Process: {rule.ProcessName}");
        return string.Join(" + ", parts);
    }

    private void AddAutoRule_Click(object? sender, RoutedEventArgs e)
    {
        string title = AutoWindowTitle.Text?.Trim() ?? string.Empty;
        string proc = AutoProcessName.Text?.Trim() ?? string.Empty;
        string? profileName = AutoProfileCombo.SelectedItem as string;

        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(proc)) return;
        if (string.IsNullOrEmpty(profileName)) return;

        _engine.Config.AutoProfileRules.Add(new AutoProfileRule
        {
            WindowTitleMatch = title,
            ProcessName = proc,
            ProfileName = profileName
        });

        _engine.SaveConfig();
        _engine.RefreshAutoProfileTimer();
        AutoWindowTitle.Text = string.Empty;
        AutoProcessName.Text = string.Empty;
        RefreshAutoProfileList();
    }

    private void DeleteAutoRule_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int index &&
            index >= 0 && index < _engine.Config.AutoProfileRules.Count)
        {
            _engine.Config.AutoProfileRules.RemoveAt(index);
            _engine.SaveConfig();
            _engine.RefreshAutoProfileTimer();
            RefreshAutoProfileList();
        }
    }

    private void OnProfileChanged(string name)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Update profile combo to reflect auto-profile switch
            if (_engine.ActiveProfile != null && ProfileCombo.ItemsSource is IList<string> items)
            {
                int idx = items.IndexOf(_engine.ActiveProfile.Name);
                if (idx >= 0 && ProfileCombo.SelectedIndex != idx)
                    ProfileCombo.SelectedIndex = idx;
            }

            RefreshSetList();
            RefreshMappingList();
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the profile name is unique among existing profiles by
    /// appending a numeric suffix if a collision is detected.
    /// </summary>
    private void EnsureUniqueProfileName(GamepadKeyMapperProfile profile)
    {
        string baseName = profile.Name;
        int suffix = 2;
        while (_engine.Config.Profiles.Any(p =>
                   p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
        {
            profile.Name = $"{baseName} ({suffix++})";
        }
    }

    // ── Prompt helper ───────────────────────────────────────────────────

    private async Task<string> PromptTextAsync(string title, string message, string defaultValue)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var textBox = new TextBox
        {
            Text = defaultValue,
            Margin = new Avalonia.Thickness(16, 8, 16, 8),
            Padding = new Avalonia.Thickness(8, 6)
        };

        string result = string.Empty;

        var okButton = new Button
        {
            Content = LocalizationManager.Instance["Common_OK"],
            Padding = new Avalonia.Thickness(16, 6),
            Margin = new Avalonia.Thickness(0, 0, 4, 0)
        };
        okButton.Click += (_, _) => { result = textBox.Text ?? string.Empty; dialog.Close(); };

        var cancelButton = new Button
        {
            Content = LocalizationManager.Instance["Common_Cancel"],
            Padding = new Avalonia.Thickness(16, 6),
            Margin = new Avalonia.Thickness(4, 0, 0, 0)
        };
        cancelButton.Click += (_, _) => { result = string.Empty; dialog.Close(); };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 0, 16, 16),
            Spacing = 4,
            Children = { okButton, cancelButton }
        };

        var panel = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    Margin = new Avalonia.Thickness(16, 16, 16, 0),
                    Foreground = new SolidColorBrush(Color.Parse("#CDD6F4"))
                },
                textBox,
                buttonPanel
            }
        };

        dialog.Content = panel;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
            await dialog.ShowDialog(parentWindow);
        else
            dialog.Show();

        return result;
    }

    // ── Display models ──────────────────────────────────────────────────

    private sealed class MappingDisplayItem
    {
        public string InputName { get; set; } = string.Empty;
        public GamepadInput InputValue { get; set; }
        public string ActionText { get; set; } = "—";
    }

    private sealed class AutoProfileDisplayItem
    {
        public int Index { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
    }
}
