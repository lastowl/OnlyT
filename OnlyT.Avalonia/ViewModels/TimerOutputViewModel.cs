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
    private bool _isShowingClock = true;
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
    private bool _showTimeOfDay;

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

    public TimerOutputViewModel(ITalkTimerService timerService, IOptionsService optionsService, IBellService? bellService = null)
    {
        _timerService = timerService;
        _optionsService = optionsService;
        _bellService = bellService;
        _timerService.TimerChangedEvent += OnTimerChanged;
        _timerService.TimerStartedEvent += OnTimerStarted;

        // Get settings
        IsClockFlat = _optionsService.IsFlatClockStyle;
        ApplyClockMode(_optionsService.FullScreenClockMode);
        ShowTimeOfDay = _optionsService.ShowTimeOfDayUnderTimer;
        ShowDigitalSeconds = _optionsService.ShowDigitalSeconds;
        ShowDurationSector = _optionsService.ShowDurationSector;
        FlashTimerEnabled = _optionsService.FlashTimerWhenOvertime;
        BellOnOvertimeEnabled = _optionsService.BellOnOvertime;
        AnalogueClockWidthPercent = _optionsService.AnalogueClockWidthPercent;
        ApplyBackgroundSetting(_optionsService.ShowBackgroundOnTimer);

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
        ShowTimeOfDay = _optionsService.ShowTimeOfDayUnderTimer;
        ShowDigitalSeconds = _optionsService.ShowDigitalSeconds;
        ShowDurationSector = _optionsService.ShowDurationSector;
        FlashTimerEnabled = _optionsService.FlashTimerWhenOvertime;
        BellOnOvertimeEnabled = _optionsService.BellOnOvertime;
        AnalogueClockWidthPercent = _optionsService.AnalogueClockWidthPercent;
        ApplyBackgroundSetting(_optionsService.ShowBackgroundOnTimer);
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
            case FullScreenClockMode.Both:
                ShowAnalogueClock = true;
                ShowDigitalClock = true;
                break;
        }
    }

    private void OnTimerStarted(object? sender, TimerStartedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _targetSecs = e.TargetSecs;
            _closingSecs = e.ClosingSecs;
            _startAngle = CalculateAngleFromTime(DateTime.Now);
            _isShowingClock = false;
            _hasPlayedOvertimeBell = false;
            _isInOvertime = false;
            StopFlashing();
        });
    }

    private void OnTimerChanged(object? sender, TimerChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!e.IsRunning)
            {
                // Timer stopped, show clock
                _isShowingClock = true;
                _isInOvertime = false;
                DurationSector = null;
                StopFlashing();
                UpdateClockDisplay();
                return;
            }

            // Timer is running
            _isShowingClock = false;
            var remaining = e.RemainingSecs;
            var elapsed = _targetSecs - remaining;

            // Update duration sector for analogue clock
            UpdateDurationSector(elapsed, remaining);

            if (remaining < 0)
            {
                // Overtime - red
                TimeDisplay = $"-{Math.Abs(remaining) / 60:D2}:{Math.Abs(remaining) % 60:D2}";
                TextColor = new SolidColorBrush(Colors.Red);

                // Handle overtime notifications
                if (!_isInOvertime)
                {
                    // Just entered overtime
                    _isInOvertime = true;
                    OnEnteredOvertime();
                }

                // Start flashing if enabled
                if (FlashTimerEnabled && !_flashTimer.IsEnabled)
                {
                    StartFlashing();
                }
            }
            else if (remaining <= e.ClosingSecs)
            {
                // Closing seconds - orange/yellow
                TimeDisplay = $"{remaining / 60:D2}:{remaining % 60:D2}";
                TextColor = new SolidColorBrush(Colors.Orange);
                _isInOvertime = false;
                StopFlashing();
            }
            else
            {
                // Normal - green
                TimeDisplay = $"{remaining / 60:D2}:{remaining % 60:D2}";
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

        if (_isShowingClock)
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
