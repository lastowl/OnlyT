using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OnlyT.Avalonia.Services.Timer;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Avalonia.EventArgsTypes;
using OnlyT.Avalonia.Controls.AnalogueClock;
using Serilog;

namespace OnlyT.Avalonia.ViewModels;

/// <summary>
/// Timer output window view model - displays the countdown or clock
/// </summary>
public partial class TimerOutputViewModel : ObservableObject
{
    private readonly ITalkTimerService _timerService;
    private readonly IOptionsService _optionsService;
    private readonly IBellService? _bellService;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _flashTimer;
    private int _targetSecs;
    private int _closingSecs;
    private double _startAngle;
    private bool _hasPlayedOvertimeBell;
    private bool _isInOvertime;

    [ObservableProperty]
    private string _timeDisplay = "00:00";

    [ObservableProperty]
    private SolidColorBrush _textColor = new(Colors.White);

    [ObservableProperty]
    private IBrush _backgroundColor = new SolidColorBrush(Colors.Black);

    [ObservableProperty]
    private bool _showBackgroundGradient;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBothClocks))]
    [NotifyPropertyChangedFor(nameof(ShowDigitalClockOnly))]
    [NotifyPropertyChangedFor(nameof(ShowAnalogueClockOnly))]
    private bool _showDigitalClock = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBothClocks))]
    [NotifyPropertyChangedFor(nameof(ShowDigitalClockOnly))]
    [NotifyPropertyChangedFor(nameof(ShowAnalogueClockOnly))]
    private bool _showAnalogueClock;

    /// <summary>
    /// True when both analogue and digital clocks should be shown (stacked layout)
    /// </summary>
    public bool ShowBothClocks => ShowAnalogueClock && ShowDigitalClock;

    /// <summary>
    /// True when only the digital clock should be shown (full size)
    /// </summary>
    public bool ShowDigitalClockOnly => ShowDigitalClock && !ShowAnalogueClock;

    /// <summary>
    /// True when only the analogue clock should be shown (full size)
    /// </summary>
    public bool ShowAnalogueClockOnly => ShowAnalogueClock && !ShowDigitalClock;

    [ObservableProperty]
    private bool _isClockRunning = true;

    [ObservableProperty]
    private DurationSector? _durationSector;

    [ObservableProperty]
    private bool _isClockFlat;

    [ObservableProperty]
    private bool _digitalTimeFormat24Hours = true;

    [ObservableProperty]
    private bool _digitalTimeFormatShowLeadingZero = true;

    [ObservableProperty]
    private bool _digitalTimeFormatAMPM;

    [ObservableProperty]
    private string _currentTimeOfDay = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimeOfDayVisible))]
    private bool _showTimeOfDay;

    // Tracks whether the main display is currently showing the wall clock
    // (timer stopped) vs the talk timer (timer running). Observable so the
    // time-of-day row below the main display can hide itself when the main
    // display is already showing the clock — otherwise you see two
    // identical wall-clock readouts stacked on top of each other.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimeOfDayVisible))]
    private bool _isShowingClock = true;

    /// <summary>
    /// Time-of-day row under the main display is visible only when the
    /// main display is showing the timer (not the wall clock).
    /// </summary>
    public bool IsTimeOfDayVisible => ShowTimeOfDay && !IsShowingClock;

    [ObservableProperty]
    private bool _showDigitalSeconds;

    [ObservableProperty]
    private bool _showDurationSector;

    [ObservableProperty]
    private bool _isFlashing;

    [ObservableProperty]
    private double _flashOpacity = 1.0;

    [ObservableProperty]
    private bool _flashTimerEnabled;

    [ObservableProperty]
    private bool _bellOnOvertimeEnabled;

    [ObservableProperty]
    private int _analogueClockWidthPercent = 80;

    [ObservableProperty]
    private Thickness _frameBorderThickness = new(0);

    // Cursor for the timer output window. Hidden by default so the cursor
    // doesn't distract during a meeting; shown only if the user explicitly
    // enables ShowMousePointerInTimerDisplay.
    [ObservableProperty]
    private global::Avalonia.Input.Cursor _mousePointer =
        new(global::Avalonia.Input.StandardCursorType.None);

    public TimerOutputViewModel(ITalkTimerService timerService, IOptionsService optionsService, IBellService? bellService = null)
    {
        _timerService = timerService;
        _optionsService = optionsService;
        _bellService = bellService;
        _timerService.TimerChangedEvent += OnTimerChanged;
        _timerService.TimerStartedEvent += OnTimerStarted;
        _optionsService.OptionsChanged += (_, _) =>
            Dispatcher.UIThread.Post(RefreshSettings);

        // Pull initial values from options. RefreshSettings is the single
        // source of truth for option-derived state; it is also called whenever
        // IOptionsService.OptionsChanged fires so edits take effect live.
        RefreshSettings();

        // Set up clock timer to update every second
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();

        // Set up flash timer for overtime flashing effect
        _flashTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _flashTimer.Tick += OnFlashTick;

        // Show clock initially
        UpdateClockDisplay();
    }

    // Parameterless constructor for design-time
    public TimerOutputViewModel() : this(
        new TalkTimerService(),
        new SimpleOptionsService())
    {
    }

    public DateTime QueryDateTime()
    {
        return DateTime.Now;
    }

    /// <summary>
    /// Refresh display settings from options service
    /// </summary>
    public void RefreshSettings()
    {
        IsClockFlat = _optionsService.IsFlatClockStyle;
        ApplyClockMode(_optionsService.FullScreenClockMode);
        OnPropertyChanged(nameof(ShowBothClocks));
        OnPropertyChanged(nameof(ShowDigitalClockOnly));
        OnPropertyChanged(nameof(ShowAnalogueClockOnly));
        ShowTimeOfDay = _optionsService.ShowTimeOfDayUnderTimer;
        ShowDigitalSeconds = _optionsService.ShowDigitalSeconds;
        ShowDurationSector = _optionsService.ShowDurationSector;
        FlashTimerEnabled = _optionsService.FlashTimerWhenOvertime;
        BellOnOvertimeEnabled = _optionsService.BellOnOvertime;
        AnalogueClockWidthPercent = _optionsService.AnalogueClockWidthPercent;
        ApplyBackgroundSetting(_optionsService.ShowBackgroundOnTimer);
        FrameBorderThickness = new Thickness(_optionsService.ShowBackgroundOnTimer ? 3 : 0);
        MousePointer = _optionsService.ShowMousePointerInTimerDisplay
            ? new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow)
            : new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.None);

        ApplyClockHourFormat(_optionsService.ClockHourFormat);
    }

    private void ApplyClockHourFormat(ClockHourFormat format)
    {
        DigitalTimeFormat24Hours = format is ClockHourFormat.Format24 or ClockHourFormat.Format24LeadingZero;
        DigitalTimeFormatShowLeadingZero = format is ClockHourFormat.Format12LeadingZero
            or ClockHourFormat.Format24LeadingZero
            or ClockHourFormat.Format12LeadingZeroAMPM;
        DigitalTimeFormatAMPM = format is ClockHourFormat.Format12AMPM or ClockHourFormat.Format12LeadingZeroAMPM;
    }

    /// <summary>
    /// Apply the background setting (solid black or gradient)
    /// </summary>
    private void ApplyBackgroundSetting(bool showGradient)
    {
        ShowBackgroundGradient = showGradient;
        if (showGradient)
        {
            // Create a subtle gradient background
            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(30, 30, 40), 0),
                    new GradientStop(Color.FromRgb(10, 10, 15), 0.5),
                    new GradientStop(Color.FromRgb(20, 20, 30), 1)
                }
            };
            BackgroundColor = gradientBrush;
        }
        else
        {
            BackgroundColor = new SolidColorBrush(Colors.Black);
        }
    }

    /// <summary>
    /// Apply the clock display mode setting
    /// </summary>
    private static string FormatSignedTime(int seconds)
    {
        if (seconds < 0)
        {
            var abs = -seconds;
            return $"-{abs / 60:D2}:{abs % 60:D2}";
        }
        return $"{seconds / 60:D2}:{seconds % 60:D2}";
    }

    private void ApplyClockMode(FullScreenClockMode mode)
    {
        switch (mode)
        {
            case FullScreenClockMode.Analogue:
                ShowAnalogueClock = true;
                ShowDigitalClock = false;
                break;
            case FullScreenClockMode.Digital:
                ShowAnalogueClock = false;
                ShowDigitalClock = true;
                break;
            case FullScreenClockMode.AnalogueAndDigital:
                ShowAnalogueClock = true;
                ShowDigitalClock = true;
                break;
        }
    }

    private bool _isCountingUp;

    private void OnTimerStarted(object? sender, TimerStartedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _targetSecs = e.TargetSecs;
            _closingSecs = e.ClosingSecs;
            _isCountingUp = e.IsCountingUp;
            _startAngle = CalculateAngleFromTime(DateTime.Now);
            IsShowingClock = false;
            _hasPlayedOvertimeBell = false;
            _isInOvertime = false;
            StopFlashing();

            // Show the correct starting value immediately so the output
            // window doesn't flash the target time before the first tick
            // arrives. Matches WPF TimerOutputWindowViewModel.OnTimerStarted.
            TimeDisplay = _isCountingUp
                ? FormatSignedTime(0)
                : FormatSignedTime(e.TargetSecs);
        });
    }

    private void OnTimerChanged(object? sender, TimerChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!e.IsRunning)
            {
                // Timer stopped, show clock
                IsShowingClock = true;
                _isInOvertime = false;
                DurationSector = null;
                StopFlashing();
                UpdateClockDisplay();
                return;
            }

            // Timer is running
            IsShowingClock = false;
            _isCountingUp = e.IsCountingUp;
            var remaining = e.RemainingSecs;
            var elapsed = _targetSecs - remaining;

            // Update duration sector for analogue clock
            UpdateDurationSector(elapsed, remaining);

            // Pick which value to display based on count-up vs countdown.
            // Colour/flash/overtime logic still tracks remaining because
            // that's what 'overtime' is defined against.
            var displaySecs = _isCountingUp ? elapsed : remaining;
            TimeDisplay = FormatSignedTime(displaySecs);

            if (remaining < 0)
            {
                TextColor = new SolidColorBrush(Colors.Red);

                if (!_isInOvertime)
                {
                    _isInOvertime = true;
                    OnEnteredOvertime();
                }

                if (FlashTimerEnabled && !_flashTimer.IsEnabled)
                {
                    StartFlashing();
                }
            }
            else if (remaining <= e.ClosingSecs)
            {
                TextColor = new SolidColorBrush(Colors.Orange);
                _isInOvertime = false;
                StopFlashing();
            }
            else
            {
                TextColor = new SolidColorBrush(Colors.LimeGreen);
                _isInOvertime = false;
                StopFlashing();
            }
        });
    }

    private void UpdateDurationSector(int elapsedSecs, int remainingSecs)
    {
        if (_targetSecs == 0 || !ShowDurationSector)
        {
            DurationSector = null;
            return;
        }

        var now = DateTime.Now;
        var currentAngle = CalculateAngleFromTime(now);
        var targetAngle = _startAngle + (_targetSecs / 60.0) * 6.0; // 6 degrees per minute

        // Normalize target angle
        while (targetAngle >= 360) targetAngle -= 360;

        var isOvertime = remainingSecs < 0;

        DurationSector = new DurationSector
        {
            StartAngle = _startAngle,
            CurrentAngle = currentAngle,
            EndAngle = targetAngle,
            IsOvertime = isOvertime,
            ShowElapsedSector = !isOvertime
        };
    }

    private static double CalculateAngleFromTime(DateTime dt)
    {
        // Convert time to angle (12 o'clock = 0 degrees, clockwise)
        return (dt.Minute * 6) + (dt.Second * 0.1);
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        // Always update time of day if enabled
        if (ShowTimeOfDay)
        {
            CurrentTimeOfDay = FormatTimeOfDay(DateTime.Now, showSeconds: true);
        }

        if (IsShowingClock)
        {
            UpdateClockDisplay();
        }
    }

    private void UpdateClockDisplay()
    {
        var now = DateTime.Now;
        TimeDisplay = FormatTimeOfDay(now, ShowDigitalSeconds);
        TextColor = new SolidColorBrush(Colors.White);
    }

    private string FormatTimeOfDay(DateTime time, bool showSeconds)
    {
        var format = _optionsService.ClockHourFormat;
        var seconds = showSeconds ? ":ss" : "";

        return format switch
        {
            ClockHourFormat.Format12 => time.ToString($"h:mm{seconds}"),
            ClockHourFormat.Format12LeadingZero => time.ToString($"hh:mm{seconds}"),
            ClockHourFormat.Format24 => time.ToString($"H:mm{seconds}"),
            ClockHourFormat.Format24LeadingZero => time.ToString($"HH:mm{seconds}"),
            ClockHourFormat.Format12AMPM => time.ToString($"h:mm{seconds} tt"),
            ClockHourFormat.Format12LeadingZeroAMPM => time.ToString($"hh:mm{seconds} tt"),
            _ => time.ToString($"HH:mm{seconds}")
        };
    }

    private void OnEnteredOvertime()
    {
        // Play bell when entering overtime if enabled
        if (BellOnOvertimeEnabled && !_hasPlayedOvertimeBell)
        {
            _hasPlayedOvertimeBell = true;
            try
            {
                _bellService?.Play(_optionsService.BellVolumePercent);
                Log.Information("Playing overtime bell");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to play overtime bell");
            }
        }
    }

    private void OnFlashTick(object? sender, EventArgs e)
    {
        // Toggle flash opacity for pulsing effect
        FlashOpacity = FlashOpacity > 0.5 ? 0.3 : 1.0;
        IsFlashing = true;
    }

    private void StartFlashing()
    {
        IsFlashing = true;
        FlashOpacity = 1.0;
        _flashTimer.Start();
    }

    private void StopFlashing()
    {
        _flashTimer.Stop();
        IsFlashing = false;
        FlashOpacity = 1.0;
    }
}
