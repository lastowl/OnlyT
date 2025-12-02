using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnlyT.Avalonia.Models;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.Localization;
using OnlyT.Avalonia.Services.LogLevelSwitch;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Avalonia.Services.Overrun;
using OnlyT.Avalonia.Services.Reminders;
using OnlyT.Avalonia.Services.TalkSchedule;
using OnlyT.Avalonia.Services.Timer;
using OnlyT.Avalonia.Services.Report;
using OnlyT.Avalonia.EventArgsTypes;
using OnlyT.Avalonia.Utils;
using OnlyT.Common.Services.DateTime;
using OnlyT.Core.Abstractions;
using Serilog;

namespace OnlyT.Avalonia.ViewModels;

/// <summary>
/// View model for the Operator page (simplified MVP version)
/// </summary>
public partial class OperatorPageViewModel : ObservableObject
{
    private readonly ITalkTimerService _timerService;
    private readonly ITalkScheduleService _scheduleService;
    private readonly IOptionsService _optionsService;
    private readonly IBellService _bellService;
    private readonly IMonitorService _monitorService;
    private readonly IReminderService _reminderService;
    private readonly ILocalizationService _localizationService;
    private readonly ILocalTimingDataStoreService _timingDataService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IQueryWeekendService _queryWeekendService;
    private readonly IFirewallService _firewallService;
    private readonly IOverrunService _overrunService;
    private readonly ILogLevelSwitchService _logLevelSwitchService;
    private OnlyT.Avalonia.Views.TimerOutputWindow? _timerOutputWindow;
    private OnlyT.Avalonia.Views.CountdownWindow? _countdownWindow;

    [ObservableProperty]
    private ObservableCollection<TalkScheduleItem> _talks = [];

    [ObservableProperty]
    private TalkScheduleItem? _selectedTalk;

    [ObservableProperty]
    private string _timeDisplay = "00:00";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _startPauseButtonText = "Start";

    [ObservableProperty]
    private bool _showStartButton = true;

    [ObservableProperty]
    private bool _showPauseButton = false;

    [ObservableProperty]
    private bool _showResumeButton = false;

    [ObservableProperty]
    private int _targetSeconds;

    [ObservableProperty]
    private int _elapsedSeconds;

    [ObservableProperty]
    private bool _bellEnabled;

    [ObservableProperty]
    private bool _countUp;

    [ObservableProperty]
    private string _countUpButtonText = "⬆ Count Up";

    [ObservableProperty]
    private string _timerColor = "White";

    // Brush-based timer color for XAML binding
    [ObservableProperty]
    private IBrush _textColorBrush = Brushes.White;

    // Shrink mode properties
    [ObservableProperty]
    private bool _inShrinkMode;

    public bool NotInShrinkMode => !InShrinkMode;
    public int StartStopButtonRowSpan => InShrinkMode ? 2 : 1;
    public int StartStopButtonHeight => InShrinkMode ? 110 : 54;
    public int TimeDisplayColumnSpan => InShrinkMode ? 2 : 1;

    // Bell icon properties
    [ObservableProperty]
    private bool _isOvertime;

    private static readonly SolidColorBrush BellColorActive = new(Color.Parse("#f3dcbc"));
    private static readonly SolidColorBrush BellColorInactive = new(Colors.DarkGray);
    private static readonly SolidColorBrush BellColorManual = new(Colors.Red);

    public bool IsBellVisible => SelectedTalk?.BellApplicable == true && _optionsService.IsBellEnabled;

    public IBrush BellColour
    {
        get
        {
            if (SelectedTalk == null) return BellColorInactive;
            if (IsOvertime) return BellColorManual;
            return BellEnabled ? BellColorActive : BellColorInactive;
        }
    }

    public string BellTooltip
    {
        get
        {
            if (SelectedTalk == null) return string.Empty;
            if (IsOvertime) return _localizationService.GetString("SOUND_BELL") ?? "Click to sound bell";
            return BellEnabled
                ? _localizationService.GetString("ACTIVE_BELL") ?? "Bell is active"
                : _localizationService.GetString("INACTIVE_BELL") ?? "Bell is inactive";
        }
    }

    // Duration indicator properties
    [ObservableProperty]
    private string? _duration1String;

    [ObservableProperty]
    private string? _duration2String;

    [ObservableProperty]
    private string? _duration3String;

    public string Duration1ArrowString => string.IsNullOrEmpty(Duration2String) ? string.Empty : "→";
    public string Duration2ArrowString => string.IsNullOrEmpty(Duration3String) ? string.Empty : "→";

    [ObservableProperty]
    private IBrush? _duration1Colour = Brushes.White;

    [ObservableProperty]
    private IBrush? _duration2Colour = Brushes.White;

    [ObservableProperty]
    private IBrush? _duration3Colour = Brushes.White;

    [ObservableProperty]
    private string? _duration1Tooltip;

    [ObservableProperty]
    private string? _duration2Tooltip;

    [ObservableProperty]
    private string? _duration3Tooltip;

    // Operating mode properties
    public bool IsManualMode => _optionsService.OperatingMode == OperatingMode.Manual;
    public bool IsNotManualMode => !IsManualMode;
    public bool IsAutoMode => _optionsService.OperatingMode == OperatingMode.Automatic;

    // Circuit visit
    public bool IsCircuitVisit
    {
        get => _optionsService.IsCircuitVisit && IsAutoMode;
        set
        {
            if (_optionsService.IsCircuitVisit != value)
            {
                _optionsService.IsCircuitVisit = value;
                OnPropertyChanged();
                RefreshTalks();
            }
        }
    }

    public bool ShouldShowCircuitVisitToggle => IsAutoMode && _optionsService.ShowCircuitVisitToggle;

    // Count up/down extended properties
    public bool AllowCountUpDownToggle => _optionsService.AllowCountUpToggle;

    public string CountUpOrDownTooltip => CountUp
        ? _localizationService.GetString("COUNTING_UP") ?? "Counting up"
        : _localizationService.GetString("COUNTING_DOWN") ?? "Counting down";

    public string CountUpOrDownImageData => CountUp
        ? "M 16,0 L 32,7.5 22,7.5 16,30 10,7.5 0,7.5z"
        : "M 16,0 L 22,22.5 32,22.5 16,30 0,22.5 10,22.5z";

    public bool ShowUpDownButton => SelectedTalk != null && NotInShrinkMode && AllowCountUpDownToggle;

    // Validation and state
    public bool IsValidTalk => SelectedTalk != null;
    public bool IsNotRunning => !IsRunning;

    // Animation trigger
    [ObservableProperty]
    private bool _runFlashAnimation;

    // New version / countdown state
    [ObservableProperty]
    private bool _isCountdownActive;

    [ObservableProperty]
    private bool _isNewVersionAvailable;

    // Reminder notification properties
    [ObservableProperty]
    private bool _isReminderShowing;

    [ObservableProperty]
    private string _reminderMessage = string.Empty;

    // TalkId for compatibility with WPF approach
    public int TalkId => SelectedTalk?.Id ?? 0;

    private bool _bellHasPlayed = false;

    public OperatorPageViewModel(
        ITalkTimerService timerService,
        ITalkScheduleService scheduleService,
        IOptionsService optionsService,
        IBellService bellService,
        IMonitorService monitorService,
        IReminderService reminderService,
        ILocalizationService localizationService,
        ILocalTimingDataStoreService timingDataService,
        IDateTimeService dateTimeService,
        IQueryWeekendService queryWeekendService,
        IFirewallService firewallService,
        IOverrunService overrunService,
        ILogLevelSwitchService logLevelSwitchService)
    {
        _timerService = timerService;
        _scheduleService = scheduleService;
        _optionsService = optionsService;
        _bellService = bellService;
        _monitorService = monitorService;
        _reminderService = reminderService;
        _localizationService = localizationService;
        _timingDataService = timingDataService;
        _dateTimeService = dateTimeService;
        _queryWeekendService = queryWeekendService;
        _firewallService = firewallService;
        _overrunService = overrunService;
        _logLevelSwitchService = logLevelSwitchService;

        // Subscribe to timer events
        _timerService.TimerChangedEvent += OnTimerChanged;

        // Subscribe to reminder events
        _reminderService.ReminderTriggered += OnReminderTriggered;

        LoadTalks();

        BellEnabled = _optionsService.IsBellEnabled && _optionsService.AutoBell;
        CountUp = false; // Default to countdown mode

        // Initialize localized status text
        StatusText = _localizationService.GetString("STATUS_READY") ?? "Ready";

        // Open timer output window on startup
        ShowTimerOutputWindow();
    }

    private void OnReminderTriggered(object? sender, ReminderEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsReminderShowing = e.IsShowing;
            ReminderMessage = e.Message;
        });
    }

    private void LoadTalks()
    {
        var scheduleItems = _scheduleService.GetTalkScheduleItems().ToList();
        Talks = new ObservableCollection<TalkScheduleItem>(scheduleItems);

        if (Talks.Any())
        {
            SelectedTalk = Talks.First();
        }
    }

    /// <summary>
    /// Public method to refresh talks when settings change
    /// </summary>
    public void RefreshTalks()
    {
        // Reset the schedule service to clear cached schedule
        // This is needed when circuit visit toggle changes
        _scheduleService.Reset();
        LoadTalks();
        BellEnabled = _optionsService.IsBellEnabled && _optionsService.AutoBell;

        // Notify UI of settings-related property changes
        OnPropertyChanged(nameof(IsCircuitVisit));
        OnPropertyChanged(nameof(ShouldShowCircuitVisitToggle));
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(IsNotManualMode));
        OnPropertyChanged(nameof(IsAutoMode));

        // Refresh timer output settings (e.g., analog/digital clock switch)
        var timerOutputViewModel = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<TimerOutputViewModel>();
        timerOutputViewModel?.RefreshSettings();
    }

    partial void OnSelectedTalkChanged(TalkScheduleItem? value)
    {
        if (value != null && !IsRunning)
        {
            TargetSeconds = (int)value.ActualDuration.TotalSeconds;
            UpdateTimeDisplay(TargetSeconds, 0);
            SetDurationStringAttributes();

            // Notify bell-related properties
            OnPropertyChanged(nameof(IsBellVisible));
            OnPropertyChanged(nameof(BellColour));
            OnPropertyChanged(nameof(BellTooltip));
            OnPropertyChanged(nameof(ShowUpDownButton));
            OnPropertyChanged(nameof(IsValidTalk));

            IsOvertime = false;
        }
    }

    partial void OnInShrinkModeChanged(bool value)
    {
        OnPropertyChanged(nameof(NotInShrinkMode));
        OnPropertyChanged(nameof(StartStopButtonRowSpan));
        OnPropertyChanged(nameof(StartStopButtonHeight));
        OnPropertyChanged(nameof(TimeDisplayColumnSpan));
        OnPropertyChanged(nameof(ShowUpDownButton));
    }

    private void OnTimerChanged(object? sender, TimerChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsRunning = e.IsRunning;
            IsPaused = _timerService.IsPaused;
            TargetSeconds = e.TargetSecs;
            ElapsedSeconds = e.ElapsedSecs;

            // Update button visibility
            UpdateButtonStates();

            // Notify commands that their CanExecute state has changed
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
            ResumeCommand.NotifyCanExecuteChanged();

            var remaining = e.RemainingSecs;
            UpdateTimeDisplay(e.TargetSecs, e.ElapsedSecs);

            // Update status and colors
            if (IsPaused)
            {
                StatusText = _localizationService.GetString("STATUS_PAUSED") ?? "Paused";
                TimerColor = "Orange";
                TextColorBrush = Brushes.Orange;
            }
            else if (IsRunning)
            {
                StatusText = remaining < 0
                    ? _localizationService.GetString("STATUS_OVERTIME") ?? "Overtime"
                    : _localizationService.GetString("STATUS_RUNNING") ?? "Running";

                // Change color based on time remaining
                if (remaining < 0)
                {
                    TimerColor = "Red";
                    TextColorBrush = Brushes.Red;
                    if (!IsOvertime)
                    {
                        IsOvertime = true;
                        OnPropertyChanged(nameof(BellColour));
                        OnPropertyChanged(nameof(BellTooltip));
                    }
                }
                else if (remaining < e.ClosingSecs)
                {
                    TimerColor = "Orange";
                    TextColorBrush = Brushes.Orange;
                }
                else
                {
                    TimerColor = "LimeGreen";
                    TextColorBrush = Brushes.LimeGreen;
                }
            }
            else
            {
                StatusText = _localizationService.GetString("STATUS_STOPPED") ?? "Stopped";
                TimerColor = "White";
                TextColorBrush = Brushes.White;
            }

            // Ring bell when time reaches zero
            // Diagnostic logging to help debug bell issues
            if (e.IsRunning && remaining <= 0 && remaining > -2)
            {
                Log.Information("Bell check - IsRunning: {IsRunning}, BellEnabled: {BellEnabled}, BellHasPlayed: {BellHasPlayed}, Remaining: {Remaining}s, BellApplicable: {BellApplicable}",
                    e.IsRunning, BellEnabled, _bellHasPlayed, remaining, SelectedTalk?.BellApplicable ?? true);
            }

            // Play bell once when remaining hits 0 to -1 second range (catches exact zero-crossing)
            if (e.IsRunning && !_bellHasPlayed && BellEnabled && remaining <= 0 && remaining > -1 &&
                (SelectedTalk?.BellApplicable ?? true))
            {
                try
                {
                    Log.Information("Playing bell - time is up! (remaining: {Remaining}s)", remaining);
                    _bellService.Play(_optionsService.BellVolumePercent);
                    _bellHasPlayed = true;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to play bell");
                }
            }
        });
    }

    private void UpdateButtonStates()
    {
        if (IsRunning && !IsPaused)
        {
            // Timer is running - show Pause button
            ShowStartButton = false;
            ShowPauseButton = true;
            ShowResumeButton = false;
        }
        else if (IsPaused)
        {
            // Timer is paused - show Resume button
            ShowStartButton = false;
            ShowPauseButton = false;
            ShowResumeButton = true;
        }
        else
        {
            // Timer is stopped - show Start button
            ShowStartButton = true;
            ShowPauseButton = false;
            ShowResumeButton = false;
        }
    }

    private void UpdateTimeDisplay(int targetSecs, int elapsedSecs)
    {
        var remaining = targetSecs - elapsedSecs;

        if (CountUp)
        {
            // Count up mode - show elapsed time
            TimeDisplay = TimeFormatter.FormatTimerDisplayString(elapsedSecs);
        }
        else
        {
            // Countdown mode - show remaining time (can go negative for overtime)
            TimeDisplay = remaining < 0
                ? $"-{TimeFormatter.FormatTimerDisplayString(Math.Abs(remaining))}"
                : TimeFormatter.FormatTimerDisplayString(remaining);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (SelectedTalk == null) return;

        try
        {
            // Reset bell state for new timer run
            _bellHasPlayed = false;

            // Log bell configuration for debugging
            Log.Information("Starting timer for '{TalkName}' - BellEnabled: {BellEnabled}, BellApplicable: {BellApplicable}, Duration: {Duration}s",
                SelectedTalk.Name, BellEnabled, SelectedTalk.BellApplicable, (int)SelectedTalk.ActualDuration.TotalSeconds);

            _timerService.SetupTalk(
                SelectedTalk.Id,
                (int)SelectedTalk.ActualDuration.TotalSeconds,
                SelectedTalk.ClosingSecs);

            _timerService.Start(
                (int)SelectedTalk.ActualDuration.TotalSeconds,
                SelectedTalk.Id,
                CountUp);

            // Track timing for reports
            _timingDataService.InsertTimerStart(
                SelectedTalk.Name,
                false, // isSongSegment
                SelectedTalk.IsStudentTalk,
                SelectedTalk.PlannedDuration,
                SelectedTalk.AdaptedDuration ?? SelectedTalk.PlannedDuration);

            // Notify reminder service that timer started
            _reminderService.OnTimerStarted(SelectedTalk.Id);

            StatusText = _localizationService.GetString("STATUS_RUNNING") ?? "Running";

            // Notify commands
            StopCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
            StartCommand.NotifyCanExecuteChanged();

            Log.Information("Started timer for talk: {TalkName}", SelectedTalk.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start timer");
            StatusText = "Error starting timer";
        }
    }

    private bool CanStart() => !IsRunning && SelectedTalk != null;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        try
        {
            var stoppedTalkId = SelectedTalk?.Id ?? 0;

            // Calculate variance before stopping (target - elapsed = remaining)
            // Negative value means overrun, positive means finished early
            var varianceSeconds = TargetSeconds - ElapsedSeconds;
            var variance = TimeSpan.FromSeconds(varianceSeconds);

            _timerService.Stop();

            // Track timing for reports
            _timingDataService.InsertTimerStop();
            _timingDataService.Save();

            // Notify of overrun/underrun if significant
            _overrunService.NotifyOfBadTiming(variance);

            // Reset bell state
            _bellHasPlayed = false;

            // Notify reminder service that timer stopped
            _reminderService.OnTimerStopped(stoppedTalkId);

            StatusText = _localizationService.GetString("STATUS_STOPPED") ?? "Stopped";
            TimerColor = "White";

            // Update to next talk if in auto mode
            if (_optionsService.OperatingMode != Services.Options.OperatingMode.Manual && SelectedTalk != null)
            {
                var nextId = _scheduleService.GetNext(SelectedTalk.Id);
                SelectedTalk = Talks.FirstOrDefault(t => t.Id == nextId);
            }

            Log.Information("Stopped timer");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to stop timer");
        }
    }

    private bool CanStop() => IsRunning || IsPaused;

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        try
        {
            _timerService.Pause();
            IsPaused = true;
            UpdateButtonStates();
            StatusText = _localizationService.GetString("STATUS_PAUSED") ?? "Paused";

            // Notify commands
            PauseCommand.NotifyCanExecuteChanged();
            ResumeCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();

            Log.Information("Paused timer");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to pause timer");
        }
    }

    private bool CanPause() => IsRunning && !IsPaused;

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume()
    {
        try
        {
            _timerService.Resume();
            IsPaused = false;
            UpdateButtonStates();
            StatusText = _localizationService.GetString("STATUS_RUNNING") ?? "Running";

            // Notify commands
            ResumeCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();

            Log.Information("Resumed timer");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to resume timer");
        }
    }

    private bool CanResume() => IsPaused;

    [RelayCommand]
    private void ToggleBell()
    {
        BellEnabled = !BellEnabled;
        if (SelectedTalk != null)
        {
            SelectedTalk.AutoBell = BellEnabled;
        }

        // Play bell sound when enabling to give immediate feedback
        if (BellEnabled)
        {
            try
            {
                _bellService.Play(_optionsService.BellVolumePercent);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to play bell on toggle");
            }
        }
    }

    [RelayCommand]
    private void TestBell()
    {
        try
        {
            Log.Information("Manual bell test triggered");
            _bellService.Play(_optionsService.BellVolumePercent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to play test bell");
            StatusText = "Bell test failed - check logs";
        }
    }

    [RelayCommand]
    private void ToggleCountUp()
    {
        CountUp = !CountUp;
        CountUpButtonText = CountUp ? "⬇ Count Down" : "⬆ Count Up";
        UpdateTimeDisplay(TargetSeconds, ElapsedSeconds);
    }

    [RelayCommand(CanExecute = nameof(CanAdjustTime))]
    private void IncrementTime()
    {
        AdjustTimerInternal(60); // 1 minute
    }

    [RelayCommand(CanExecute = nameof(CanAdjustTime))]
    private void IncrementTime15()
    {
        AdjustTimerInternal(15); // 15 seconds
    }

    [RelayCommand(CanExecute = nameof(CanAdjustTime))]
    private void IncrementTime5()
    {
        AdjustTimerInternal(5 * 60); // 5 minutes
    }

    [RelayCommand(CanExecute = nameof(CanAdjustTime))]
    private void DecrementTime()
    {
        AdjustTimerInternal(-60); // -1 minute
    }

    [RelayCommand(CanExecute = nameof(CanAdjustTime))]
    private void DecrementTime15()
    {
        AdjustTimerInternal(-15); // -15 seconds
    }

    [RelayCommand(CanExecute = nameof(CanAdjustTime))]
    private void DecrementTime5()
    {
        AdjustTimerInternal(-5 * 60); // -5 minutes
    }

    private void AdjustTimerInternal(int seconds)
    {
        if (SelectedTalk == null) return;

        var maxTimerSecs = 99 * 60; // 99 minutes max
        var newSecs = Math.Max(TargetSeconds + seconds, 60); // Minimum 1 minute
        if (newSecs <= maxTimerSecs)
        {
            var newDuration = TimeSpan.FromSeconds(newSecs);
            SelectedTalk.ModifiedDuration = newDuration;
            TargetSeconds = newSecs;
            UpdateTimeDisplay(TargetSeconds, 0);
            SetDurationStringAttributes();
        }
    }

    private bool CanAdjustTime() => !IsRunning && SelectedTalk?.Editable == true;

    // Shrink mode commands
    [RelayCommand]
    private void ShrinkToCompact()
    {
        // Get the main window and shrink it
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is OnlyT.Avalonia.Views.MainWindow mainWindow)
            {
                mainWindow.ShrinkToCompact();
            }
        }
    }

    [RelayCommand]
    private void ExpandFromShrink()
    {
        // Get the main window and expand it
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is OnlyT.Avalonia.Views.MainWindow mainWindow)
            {
                mainWindow.ExpandFromShrink();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanCloseApp))]
    private void CloseApp()
    {
        // Close the application - used in shrink mode
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private bool CanCloseApp() => !IsRunning;

    [RelayCommand]
    private void Help()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "https://github.com/AntonyCorbett/OnlyT/wiki",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open help URL");
        }
    }

    [RelayCommand]
    private void CloseCountdown()
    {
        IsCountdownActive = false;
        // TODO: Send message to close countdown window when implemented
    }

    [RelayCommand]
    private void DismissReminder()
    {
        _reminderService.DismissReminder();
    }

    [RelayCommand]
    private void BellToggleClick()
    {
        if (IsOvertime)
        {
            // Manually sound the bell during overtime
            try
            {
                _bellService.Play(_optionsService.BellVolumePercent);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to play bell");
            }
        }
        else
        {
            // Toggle bell enabled state
            BellEnabled = !BellEnabled;
            if (SelectedTalk != null)
            {
                SelectedTalk.AutoBell = BellEnabled;
            }
            OnPropertyChanged(nameof(BellColour));
            OnPropertyChanged(nameof(BellTooltip));
        }
    }

    private void SetDurationStringAttributes()
    {
        if (SelectedTalk == null)
        {
            Duration1String = null;
            Duration2String = null;
            Duration3String = null;
            return;
        }

        // Calculate duration tiers based on talk duration
        var totalMins = (int)SelectedTalk.ActualDuration.TotalMinutes;

        if (totalMins <= 5)
        {
            Duration1String = TimeFormatter.FormatTimerDisplayString((int)SelectedTalk.ActualDuration.TotalSeconds);
            Duration2String = null;
            Duration3String = null;
        }
        else if (totalMins <= 15)
        {
            Duration1String = TimeFormatter.FormatTimerDisplayString(totalMins / 2 * 60);
            Duration2String = TimeFormatter.FormatTimerDisplayString((int)SelectedTalk.ActualDuration.TotalSeconds);
            Duration3String = null;
        }
        else
        {
            Duration1String = TimeFormatter.FormatTimerDisplayString(totalMins / 3 * 60);
            Duration2String = TimeFormatter.FormatTimerDisplayString(totalMins * 2 / 3 * 60);
            Duration3String = TimeFormatter.FormatTimerDisplayString((int)SelectedTalk.ActualDuration.TotalSeconds);
        }

        OnPropertyChanged(nameof(Duration1ArrowString));
        OnPropertyChanged(nameof(Duration2ArrowString));
    }

    [RelayCommand]
    private void ShowSettings()
    {
        var settingsViewModel = new SettingsViewModel(_optionsService, _monitorService, _localizationService, this, _bellService, _firewallService, _logLevelSwitchService);
        var settingsWindow = new OnlyT.Avalonia.Views.SettingsWindow
        {
            DataContext = settingsViewModel
        };
        settingsWindow.Show();
    }

    [RelayCommand]
    private void ShowCountdown()
    {
        if (_countdownWindow != null)
        {
            _countdownWindow.Activate();
            return;
        }

        var countdownViewModel = new CountdownViewModel(_optionsService);
        _countdownWindow = new OnlyT.Avalonia.Views.CountdownWindow
        {
            DataContext = countdownViewModel
        };

        countdownViewModel.Start(
            closeAction: () =>
            {
                _countdownWindow?.Close();
                _countdownWindow = null;
            },
            timeUpAction: () =>
            {
                // Optionally auto-close when countdown finishes
                Dispatcher.UIThread.Post(() =>
                {
                    _countdownWindow?.Close();
                    _countdownWindow = null;
                });
            });

        _countdownWindow.Show();
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        try
        {
            StatusText = "Generating report...";

            var reportPath = await TimingReportGeneration.ExecuteAsync(
                _timingDataService,
                _dateTimeService,
                _queryWeekendService,
                weekendIncludesFriday: false);

            if (!string.IsNullOrEmpty(reportPath))
            {
                StatusText = "Report generated";
                Log.Information("Generated timing report: {Path}", reportPath);

                // Open the generated report
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = reportPath,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not open report file");
                }
            }
            else
            {
                StatusText = "No valid timing data for report";
                Log.Warning("Could not generate timing report - no valid data");
            }
        }
        catch (Exception ex)
        {
            StatusText = "Report generation failed";
            Log.Error(ex, "Failed to generate timing report");
        }
    }

    private void ShowTimerOutputWindow()
    {
        if (_timerOutputWindow != null)
        {
            _timerOutputWindow.Activate();
            return;
        }

        var timerOutputViewModel = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<TimerOutputViewModel>();
        _timerOutputWindow = new OnlyT.Avalonia.Views.TimerOutputWindow
        {
            DataContext = timerOutputViewModel
        };

        var options = _optionsService.GetOptions();

        // Set always on top
        _timerOutputWindow.Topmost = options.AlwaysOnTop;

        // Position on selected monitor
        OnlyT.Core.Abstractions.MonitorInfo? targetMonitor = null;
        var monitors = _monitorService.GetMonitors();

        if (!string.IsNullOrEmpty(options.MonitorId))
        {
            targetMonitor = monitors.FirstOrDefault(m => m.MonitorId == options.MonitorId);
        }

        // Fall back to primary monitor if no specific selection
        targetMonitor ??= monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();

        if (targetMonitor != null)
        {
            // Position window on the target monitor
            _timerOutputWindow.Position = new global::Avalonia.PixelPoint(targetMonitor.Left, targetMonitor.Top);

            if (options.FullScreenMode)
            {
                // Set window size to monitor size before going fullscreen
                _timerOutputWindow.Width = targetMonitor.Width;
                _timerOutputWindow.Height = targetMonitor.Height;
                _timerOutputWindow.WindowState = global::Avalonia.Controls.WindowState.FullScreen;
            }
        }
        else if (options.FullScreenMode)
        {
            // No monitor info available, just go fullscreen
            _timerOutputWindow.WindowState = global::Avalonia.Controls.WindowState.FullScreen;
        }

        _timerOutputWindow.Show();
    }
}
