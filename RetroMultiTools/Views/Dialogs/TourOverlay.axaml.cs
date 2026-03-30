using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using RetroMultiTools.Localization;
using RetroMultiTools.Services;

namespace RetroMultiTools.Views.Dialogs;

public partial class TourOverlay : UserControl
{
    /// <summary>
    /// Raised when the tour is finished (completed or skipped).
    /// </summary>
    public event Action? TourFinished;

    private int _currentStep;
    private bool _isAnimating;

    /// <summary>
    /// Cached brush for inactive step-indicator dots.
    /// </summary>
    private static readonly SolidColorBrush InactiveDotBrush =
        new(Color.Parse("#585B70"));

    /// <summary>
    /// Cached accent-color brushes so we never allocate a new brush for the
    /// same color twice (one per unique <see cref="TourStepInfo.AccentColor"/>).
    /// </summary>
    private static readonly Dictionary<string, SolidColorBrush> AccentBrushCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Cached reference to the TranslateTransform on <see cref="TourCard"/>.
    /// </summary>
    private TranslateTransform CardTranslate =>
        (TranslateTransform)TourCard.RenderTransform!;

    private static readonly TourStepInfo[] Steps =
    [
        // 0  Welcome
        new("Tour_WelcomeTitle",       "Tour_WelcomeDesc",
            "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z",
            "#89B4FA"),

        // 1  Sidebar navigation
        new("Tour_SidebarTitle",       "Tour_SidebarDesc",
            "M3 13h2v-2H3v2zm0 4h2v-2H3v2zm0-8h2V7H3v2zm4 4h14v-2H7v2zm0 4h14v-2H7v2zM7 7v2h14V7H7z",
            "#A6E3A1"),

        // 2  ROM Browser
        new("Tour_BrowserTitle",       "Tour_BrowserDesc",
            "M1 3h6l2 2h10v12H1V3zm2 2v10h14V7H8.17L6.17 5H3z",
            "#F9E2AF"),

        // 3  ROM Inspector
        new("Tour_InspectorTitle",     "Tour_InspectorDesc",
            "M10 2a8 8 0 105.29 14.29l3.71 3.71 1.41-1.41-3.71-3.71A8 8 0 0010 2zm0 2a6 6 0 110 12 6 6 0 010-12z",
            "#CBA6F7"),

        // 4  Patching & Conversion
        new("Tour_PatchingTitle",      "Tour_PatchingDesc",
            "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1 1 0 000-1.41l-2.34-2.34a1 1 0 00-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z",
            "#FAB387"),

        // 5  Analysis & Verification
        new("Tour_AnalysisTitle",      "Tour_AnalysisDesc",
            "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z",
            "#94E2D5"),

        // 6  Headers & Trimming
        new("Tour_HeadersTitle",       "Tour_HeadersDesc",
            "M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm-1 7V3.5L18.5 9H13z",
            "#F38BA8"),

        // 7  Utilities
        new("Tour_UtilitiesTitle",     "Tour_UtilitiesDesc",
            "M22.7 19l-9.1-9.1c.9-2.3.4-5-1.5-6.9-2-2-5-2.4-7.4-1.3L9 6 6 9 1.6 4.7C.4 7.1.9 10.1 2.9 12.1c1.9 1.9 4.6 2.4 6.9 1.5l9.1 9.1c.4.4 1 .4 1.4 0l2.3-2.3c.5-.4.5-1.1.1-1.4z",
            "#F5C2E7"),

        // 8  RetroArch / MAME / Mednafen
        new("Tour_EmulatorsTitle",     "Tour_EmulatorsDesc",
            "M21 6H3c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 10H3V8h18v8zM6 15h2v-2h2v-2H8V9H6v2H4v2h2v2z",
            "#74C7EC"),

        // 9  Analogue FPGA
        new("Tour_AnalogueTitle",      "Tour_AnalogueDesc",
            "M7 4h10c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H7c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2zm0 2v8h10V6H7zm2 10v2h6v-2H9z",
            "#EBA0AC"),

        // 10 Big Picture Mode
        new("Tour_BigPictureTitle",    "Tour_BigPictureDesc",
            "M21 3H3c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H3V5h18v14zM5 15l3.5-4.5 2.5 3.01L14.5 9l4.5 6H5z",
            "#B4BEFE"),

        // 11 Settings
        new("Tour_SettingsTitle",      "Tour_SettingsDesc",
            "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94L14.4 2.81c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.07.62-.07.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z",
            "#89DCEB"),

        // 12 Finish
        new("Tour_FinishTitle",        "Tour_FinishDesc",
            "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17z",
            "#A6E3A1"),
    ];

    public TourOverlay()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Begins (or restarts) the guided tour from step 0.
    /// </summary>
    public async void StartTour()
    {
        try
        {
            // Guard against re-entrant calls while the tour is already visible
            if (IsVisible && _isAnimating) return;

            _currentStep = 0;
            BuildDots();
            ApplyStep();

            if (IsVisible)
            {
                // Already visible (e.g. restart from settings), just reset to first step.
                // Clear the flag in case a prior animation didn't clean up.
                _isAnimating = false;
                return;
            }

            IsVisible = true;
            NextButton.Focus();
            await AnimateIn();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[TourOverlay] StartTour failed: {ex.Message}");
            _isAnimating = false;
            IsVisible = false;
        }
    }

    // ── Step rendering ─────────────────────────────────────────────────

    private void ApplyStep()
    {
        var step = Steps[_currentStep];
        var loc = LocalizationManager.Instance;

        StepTitle.Text = loc[step.TitleKey];
        StepDescription.Text = loc[step.DescriptionKey];

        try
        {
            StepIcon.Data = StreamGeometry.Parse(step.IconData);
        }
        catch (FormatException ex)
        {
            System.Diagnostics.Trace.WriteLine($"[TourOverlay] Invalid icon path for step {_currentStep}: {ex.Message}");
            StepIcon.Data = null;
        }

        if (Color.TryParse(step.AccentColor, out var color))
            StepIconBorder.Background = GetOrCreateAccentBrush(step.AccentColor, color);

        BackButton.IsVisible = _currentStep > 0;

        bool isLast = _currentStep == Steps.Length - 1;
        NextButtonText.Text = isLast ? loc["Tour_Finish"] : loc["Tour_Next"];
        SkipButton.IsVisible = !isLast;

        UpdateDots();
    }

    private void BuildDots()
    {
        StepDots.Children.Clear();
        for (int i = 0; i < Steps.Length; i++)
        {
            StepDots.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brushes.Gray
            });
        }
    }

    private void UpdateDots()
    {
        for (int i = 0; i < StepDots.Children.Count; i++)
        {
            if (StepDots.Children[i] is Ellipse dot)
            {
                if (i == _currentStep)
                {
                    dot.Width = 10;
                    dot.Height = 10;
                    var step = Steps[_currentStep];
                    if (Color.TryParse(step.AccentColor, out var c))
                        dot.Fill = GetOrCreateAccentBrush(step.AccentColor, c);
                    else
                        dot.Fill = Brushes.White;
                }
                else
                {
                    dot.Width = 8;
                    dot.Height = 8;
                    dot.Fill = InactiveDotBrush;
                }
            }
        }
    }

    // ── Animations ─────────────────────────────────────────────────────
    //
    // Slide animations use Transitions on the TranslateTransform instead
    // of Animation.RunAsync, because RunAsync requires a Visual target and
    // TranslateTransform is only an Animatable (casting it to Visual threw
    // InvalidCastException).  Opacity animations still use RunAsync on
    // TourCard / Backdrop which are Visuals.

    private async Task AnimateIn()
    {
        _isAnimating = true;
        try
        {
            // Fade in backdrop
            await CreateFadeAnimation(0.0, 1.0, 300, new CubicEaseOut()).RunAsync(Backdrop);

            // Slide + fade in card
            await AnimateCardIn();
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task AnimateCardIn()
    {
        var translate = CardTranslate;
        translate.Transitions = null;
        translate.Y = 30;
        translate.X = 0;
        TourCard.Opacity = 0;

        const int durationMs = 350;
        var easing = new CubicEaseOut();

        translate.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Easing = easing
            }
        };

        // Trigger slide transition (animates Y from 30 → 0)
        translate.Y = 0;

        // Run opacity animation in parallel (same duration)
        await CreateFadeAnimation(0.0, 1.0, durationMs, easing).RunAsync(TourCard);
        translate.Transitions = null;
    }

    private async Task AnimateCardSwitch(int direction)
    {
        _isAnimating = true;
        var translate = CardTranslate;
        try
        {
            double exitX = direction > 0 ? -40.0 : 40.0;
            double enterX = direction > 0 ? 40.0 : -40.0;

            // ── Slide out + fade out ──
            const int exitMs = 200;
            var exitEasing = new CubicEaseIn();

            translate.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = TranslateTransform.XProperty,
                    Duration = TimeSpan.FromMilliseconds(exitMs),
                    Easing = exitEasing
                }
            };

            translate.X = exitX;
            await CreateFadeAnimation(1.0, 0.0, exitMs, exitEasing).RunAsync(TourCard);
            translate.Transitions = null;

            // Update content while invisible
            ApplyStep();

            // ── Slide in from opposite side + fade in ──
            translate.X = enterX;
            translate.Y = 0;

            const int enterMs = 250;
            var enterEasing = new CubicEaseOut();

            translate.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = TranslateTransform.XProperty,
                    Duration = TimeSpan.FromMilliseconds(enterMs),
                    Easing = enterEasing
                }
            };

            translate.X = 0;
            await CreateFadeAnimation(0.0, 1.0, enterMs, enterEasing).RunAsync(TourCard);
        }
        finally
        {
            translate.Transitions = null;
            _isAnimating = false;
        }
    }

    private async Task AnimateOut()
    {
        _isAnimating = true;
        var translate = CardTranslate;
        try
        {
            const int cardMs = 250;
            var cardEasing = new CubicEaseIn();

            translate.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = TranslateTransform.YProperty,
                    Duration = TimeSpan.FromMilliseconds(cardMs),
                    Easing = cardEasing
                }
            };

            translate.Y = 20;
            await CreateFadeAnimation(1.0, 0.0, cardMs, cardEasing).RunAsync(TourCard);
            translate.Transitions = null;

            // Fade out backdrop
            await CreateFadeAnimation(1.0, 0.0, 200, new CubicEaseIn()).RunAsync(Backdrop);
        }
        finally
        {
            translate.Transitions = null;
            IsVisible = false;
            _isAnimating = false;
        }
    }

    /// <summary>
    /// Creates a reusable opacity <see cref="Animation"/> between two values.
    /// </summary>
    private static Animation CreateFadeAnimation(double from, double to, int durationMs, Easing easing)
    {
        return new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            Easing = easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(OpacityProperty, from) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(OpacityProperty, to) }
                }
            }
        };
    }

    // ── Button handlers ────────────────────────────────────────────────

    private async void NextButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isAnimating) return;

        if (_currentStep >= Steps.Length - 1)
        {
            // Finish
            await FinishTour();
            return;
        }

        _currentStep++;
        await AnimateCardSwitch(direction: 1);
    }

    private async void BackButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isAnimating || _currentStep <= 0) return;

        _currentStep--;
        await AnimateCardSwitch(direction: -1);
    }

    private async void SkipButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isAnimating) return;
        await FinishTour();
    }

    private async Task FinishTour()
    {
        AppSettings.Instance.HasCompletedTour = true;
        await AnimateOut();
        TourFinished?.Invoke();
    }

    // ── Keyboard navigation ────────────────────────────────────────────

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isAnimating) return;

        switch (e.Key)
        {
            case Key.Escape:
                SkipButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Enter:
                NextButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Right:
                NextButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Left when _currentStep > 0:
                BackButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    // ── Tour step data ─────────────────────────────────────────────────

    /// <summary>
    /// Returns a cached <see cref="SolidColorBrush"/> for the given accent
    /// color string, creating one on first use.
    /// </summary>
    private static SolidColorBrush GetOrCreateAccentBrush(string key, Color color)
    {
        if (!AccentBrushCache.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(color);
            AccentBrushCache[key] = brush;
        }
        return brush;
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        KeyDown -= OnKeyDown;
    }

    private sealed record TourStepInfo(
        string TitleKey,
        string DescriptionKey,
        string IconData,
        string AccentColor);
}
