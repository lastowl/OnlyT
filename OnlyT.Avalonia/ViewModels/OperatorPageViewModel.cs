using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
using OnlyT.Avalonia.Services.CountdownTimer;
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
    private readonly IAdaptiveTimerService _adaptiveTimerService;
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
    private readonly CountdownTimerTriggerService _countdownTriggerService;
    private OnlyT.Avalonia.Views.TimerOutputWindow? _timerOutputWindow;
    private OnlyT.Avalonia.Views.CountdownWindow? _countdownWindow;
    private DispatcherTimer? _heartbeatTimer;
    private bool _isCountdownDone;

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
    [NotifyPropertyChangedFor(nameof(CountUpOrDownTooltip))]
    [NotifyPropertyChangedFor(nameof(CountUpOrDownImageData))]
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

    // Classic UI mode: when on, the operator page visually matches the
    // original WPF OnlyT. Stored in AppOptions and read live each time.
    public bool ClassicMode => _optionsService.GetOptions().ClassicMode;
    public bool NotClassicMode => !ClassicMode;
    public bool IsReminderShowingAndNotClassic => IsReminderShowing && NotClassicMode;

    /// <summary>
    /// Called when IOptionsService.OptionsChanged fires (typically after the
    /// user saves the Settings window). Refreshes anything the operator page
    /// derives from options so the UI updates live without a restart.
    /// </summary>
    private OperatingMode _lastOperatingMode;
    private MidWeekOrWeekend _lastMeetingType;

    private void OnOptionsChangedExternally()
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Classic-mode-derived bindings
            OnPropertyChanged(nameof(ClassicMode));
            OnPropertyChanged(nameof(NotClassicMode));
            OnPropertyChanged(nameof(IsReminderShowingAndNotClassic));

            // Bell group
            OnPropertyChanged(nameof(IsBellVisible));
            OnPropertyChanged(nameof(BellColour));
            OnPropertyChanged(nameof(BellTooltip));
            BellEnabled = _optionsService.IsBellEnabled && _optionsService.AutoBell;

            // Circuit visit toggle visibility + count-up button visibility
            OnPropertyChanged(nameof(ShouldShowCircuitVisitToggle));
            OnPropertyChanged(nameof(AllowCountUpDownToggle));
            OnPropertyChanged(nameof(ShowUpDownButton));
            OnPropertyChanged(nameof(ShowExportScheduleButton));

            // If OperatingMode or MidWeekOrWeekend changed, the entire talk
            // schedule needs rebuilding (different mode = different talk list).
            // RefreshTalks resets the schedule service, reloads talks, and
            // fires property-changed for IsManualMode / IsAutoMode etc.
            // Adaptive modes don't need this — they're read on-demand by
            // AdaptiveTimerService.CalculateAdaptedDuration each tick.
            var currentMode = _optionsService.OperatingMode;
            var currentMeeting = _optionsService.MidWeekOrWeekend;
            if (currentMode != _lastOperatingMode || currentMeeting != _lastMeetingType)
            {
                _lastOperatingMode = currentMode;
                _lastMeetingType = currentMeeting;
                RefreshTalks();
            }

            // Directly refresh the timer output VM so display-mode, clock
            // format, and all visual settings update on the output window.
            // This bypasses the OptionsChanged event subscription (which
            // was unreliable) and ensures the refresh runs on the UI thread.
            var timerOutputViewModel = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<TimerOutputViewModel>();
            timerOutputViewModel?.RefreshSettings();

            // Apply AlwaysOnTop + FullScreenMode + MonitorId to the output window
            ApplyWindowStateOptionsLive();
        });
    }

    /// <summary>
    /// Re-apply window-level options (AlwaysOnTop, FullScreenMode, MonitorId)
    /// to the timer output window whenever the user changes them in Settings.
    /// Previously these were only read when the window was first created,
    /// so flipping any of them in Settings required a restart.
    /// </summary>
    private void ApplyWindowStateOptionsLive()
    {
        if (_timerOutputWindow == null)
        {
            return;
        }

        var options = _optionsService.GetOptions();

        try
        {
            _timerOutputWindow.Topmost = options.AlwaysOnTop;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update timer output window topmost");
        }

        // Move the window to the selected monitor if it changed.
        try
        {
            var monitors = _monitorService.GetMonitors();
            OnlyT.Core.Abstractions.MonitorInfo? targetMonitor = null;
            if (!string.IsNullOrEmpty(options.MonitorId))
            {
                targetMonitor = monitors.FirstOrDefault(m => m.MonitorId == options.MonitorId);
            }
            targetMonitor ??= monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();

            if (targetMonitor != null)
            {
                var currentPosition = _timerOutputWindow.Position;
                var alreadyOnMonitor =
                    currentPosition.X >= targetMonitor.Left &&
                    currentPosition.X < targetMonitor.Left + targetMonitor.Width &&
                    currentPosition.Y >= targetMonitor.Top &&
                    currentPosition.Y < targetMonitor.Top + targetMonitor.Height;

                if (!alreadyOnMonitor)
                {
                    // Drop out of full screen before repositioning — some
                    // window managers refuse a move on a fullscreen window.
                    if (_timerOutputWindow.WindowState == global::Avalonia.Controls.WindowState.FullScreen)
                    {
                        _timerOutputWindow.WindowState = global::Avalonia.Controls.WindowState.Normal;
                    }
                    _timerOutputWindow.Position = new global::Avalonia.PixelPoint(targetMonitor.Left, targetMonitor.Top);
                    if (options.FullScreenMode)
                    {
                        _timerOutputWindow.Width = targetMonitor.Width;
                        _timerOutputWindow.Height = targetMonitor.Height;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update timer output window monitor");
        }

        try
        {
            var desiredState = options.FullScreenMode
                ? global::Avalonia.Controls.WindowState.FullScreen
                : global::Avalonia.Controls.WindowState.Normal;
            if (_timerOutputWindow.WindowState != desiredState)
            {
                _timerOutputWindow.WindowState = desiredState;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update timer output window state");
        }
    }

    partial void OnIsReminderShowingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReminderShowingAndNotClassic));
    }

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
    public bool IsFileBasedMode => _optionsService.OperatingMode == OperatingMode.ScheduleFile;
    public bool ShowExportScheduleButton => _optionsService.GetOptions().ShowExportScheduleButton;

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
        IAdaptiveTimerService adaptiveTimerService,
        IBellService bellService,
        IMonitorService monitorService,
        IReminderService reminderService,
        ILocalizationService localizationService,
        ILocalTimingDataStoreService timingDataService,
        IDateTimeService dateTimeService,
        IQueryWeekendService queryWeekendService,
        IFirewallService firewallService,
        IOverrunService overrunService,
        ILogLevelSwitchService logLevelSwitchService,
        CountdownTimerTriggerService countdownTriggerService)
    {
        _timerService = timerService;
        _scheduleService = scheduleService;
        _optionsService = optionsService;
        _optionsService.OptionsChanged += (_, _) => OnOptionsChangedExternally();
        _adaptiveTimerService = adaptiveTimerService;
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
        _countdownTriggerService = countdownTriggerService;

        // Subscribe to timer events
        _timerService.TimerChangedEvent += OnTimerChanged;
        _timerService.TimerStartStopFromApiEvent += HandleTimerStartStopFromApi;

        // Subscribe to reminder events
        _reminderService.ReminderTriggered += OnReminderTriggered;

        LoadTalks();

        BellEnabled = _optionsService.IsBellEnabled && _optionsService.AutoBell;
        CountUp = _optionsService.GetOptions().CountUp;
        _lastOperatingMode = _optionsService.OperatingMode;
        _lastMeetingType = _optionsService.MidWeekOrWeekend;

        // Initialize localized status text
        StatusText = _localizationService.GetString("STATUS_READY") ?? "Ready";

        // Open timer output window on startup
        ShowTimerOutputWindow();

        // Start heartbeat timer for countdown auto-trigger
        InitHeartbeatTimer();

        // Check for new version after a short delay
        CheckForNewVersion();
    }

    private async void CheckForNewVersion()
    {
        try
        {
            await Task.Delay(2000); // Wait 2 seconds before checking

            var newVersionAvailable = await VersionDetection.IsNewVersionAvailableAsync();
            if (newVersionAvailable)
            {
                Log.Information("New version available");
                IsNewVersionAvailable = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking for new version");
        }
    }

    [RelayCommand]
    private void OpenNewVersionPage()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = VersionDetection.LatestReleaseUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening releases page");
        }
    }

    private void OnReminderTriggered(object? sender, ReminderEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsReminderShowing = e.IsShowing;
            ReminderMessage = e.Message;
        });
    }

    /// <summary>
    /// Handles timer start/stop commands from the remote API
    /// </summary>
    private void HandleTimerStartStopFromApi(object? sender, TimerStartStopEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Always on UI thread to prevent synchronization issues
            Log.Debug("Handling timer control from API - TalkId: {TalkId}, Command: {Command}", e.TalkId, e.Command);

            // Check if the talk exists
            var requestedTalk = Talks.FirstOrDefault(t => t.Id == e.TalkId);
            if (requestedTalk == null)
            {
                Log.Warning("API timer control failed - talk ID {TalkId} does not exist", e.TalkId);
                e.Success = false;
                e.CurrentStatus = _timerService.GetStatus();
                return;
            }

            var success = TalkId == e.TalkId || IsNotRunning;

            if (success)
            {
                // Select the requested talk
                SelectedTalk = requestedTalk;
                success = TalkId == e.TalkId;

                if (success)
                {
                    switch (e.Command)
                    {
                        case Models.StartStopTimerCommands.Start:
                            success = IsNotRunning;
                            if (success)
                            {
                                Start();
                                Log.Information("Timer started via API for talk: {TalkName}", requestedTalk.Name);
                            }
                            break;

                        case Models.StartStopTimerCommands.Stop:
                            success = IsRunning;
                            if (success)
                            {
                                Stop();
                                Log.Information("Timer stopped via API for talk: {TalkName}", requestedTalk.Name);
                            }
                            break;
                    }
                }
            }

            e.CurrentStatus = _timerService.GetStatus();
            if (success)
            {
                e.CurrentStatus.IsRunning = e.Command == Models.StartStopTimerCommands.Start;
            }

            e.Success = success;
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
        OnPropertyChanged(nameof(IsFileBasedMode));

        // Refresh the schedule-file picker (may have new templates)
        if (IsFileBasedMode)
        {
            RefreshScheduleFiles();
        }

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

            // Sync BellEnabled with per-talk AutoBell
            BellEnabled = value.AutoBell;

            // Refresh CountUp from per-talk setting
            CountUp = value.CountUp ?? _optionsService.CountUp;

            // Notify bell-related properties
            OnPropertyChanged(nameof(IsBellVisible));
            OnPropertyChanged(nameof(BellColour));
            OnPropertyChanged(nameof(BellTooltip));
            OnPropertyChanged(nameof(ShowUpDownButton));
            OnPropertyChanged(nameof(IsValidTalk));
            OnPropertyChanged(nameof(CountUpOrDownTooltip));
            OnPropertyChanged(nameof(CountUpOrDownImageData));

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

                // Change color based on time remaining (matches WPF GreenYellowRedSelector)
                if (remaining <= 0)
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
                else if (remaining <= e.ClosingSecs)
                {
                    TimerColor = "Yellow";
                    TextColorBrush = Brushes.Yellow;
                }
                else
                {
                    TimerColor = "Chartreuse";
                    TextColorBrush = Brushes.Chartreuse;
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
            if (e.IsRunning && !_bellHasPlayed && remaining <= 0 && remaining > -1 &&
                _optionsService.IsBellEnabled &&
                (SelectedTalk?.BellApplicable ?? true) &&
                (SelectedTalk?.AutoBell ?? false))
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

    [ObservableProperty]
    private bool _showStopButton;

    private void UpdateButtonStates()
    {
        if (IsRunning && !IsPaused)
        {
            ShowStartButton = false;
            ShowPauseButton = true;
            ShowResumeButton = false;
            ShowStopButton = true;
        }
        else if (IsPaused)
        {
            ShowStartButton = false;
            ShowPauseButton = false;
            ShowResumeButton = true;
            ShowStopButton = true;
        }
        else
        {
            ShowStartButton = true;
            ShowPauseButton = false;
            ShowResumeButton = false;
            ShowStopButton = false;
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

            // Adjust duration for adaptive timing if enabled
            AdjustForAdaptiveTime();

            // Log bell configuration for debugging
            Log.Information("Starting timer for '{TalkName}' - BellEnabled: {BellEnabled}, BellApplicable: {BellApplicable}, Duration: {Duration}s",
                SelectedTalk.Name, BellEnabled, SelectedTalk.BellApplicable, (int)SelectedTalk.ActualDuration.TotalSeconds);

            _timerService.SetupTalk(
                SelectedTalk.Id,
                (int)SelectedTalk.ActualDuration.TotalSeconds,
                SelectedTalk.ClosingSecs);

            // Track timing for reports (meeting-level structure + individual talk)
            StoreTimerStartData();

            // Notify reminder service that timer started
            _reminderService.OnTimerStarted(SelectedTalk.Id);

            StatusText = _localizationService.GetString("STATUS_RUNNING") ?? "Running";

            // Notify commands
            StopCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
            StartCommand.NotifyCanExecuteChanged();

            // Sync timer start to second boundary (like WPF) so clock and countdown are in sync
            var targetSecs = (int)SelectedTalk.ActualDuration.TotalSeconds;
            var talkId = SelectedTalk.Id;
            var countUp = CountUp;

            Task.Run(async () =>
            {
                var ms = _dateTimeService.Now().Millisecond;
                if (ms > 100)
                {
                    await Task.Delay(1000 - ms);
                }

                _timerService.Start(targetSecs, talkId, countUp);
            });

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

            // Record completed time on the talk for overtime display
            _scheduleService.RecordTalkCompleted(stoppedTalkId, ElapsedSeconds);

            // Track timing for reports
            _timingDataService.InsertTimerStop();
            _timingDataService.Save();

            // Notify of overrun/underrun if significant (use adaptive calculation in auto mode)
            NotifyOfBadTimingIfRequired(variance);

            // Reset bell state
            _bellHasPlayed = false;

            // Notify reminder service that timer stopped
            _reminderService.OnTimerStopped(stoppedTalkId);

            StatusText = _localizationService.GetString("STATUS_STOPPED") ?? "Stopped";
            TimerColor = "White";

            // Auto-advance to next talk
            if (SelectedTalk != null)
            {
                var nextId = _scheduleService.GetNext(SelectedTalk.Id);
                if (nextId > 0)
                {
                    SelectedTalk = Talks.FirstOrDefault(t => t.Id == nextId);
                }
                else if (nextId == 0)
                {
                    // End of schedule - record meeting end and auto-generate report if enabled
                    StoreEndOfMeetingData();
                    if (_optionsService.GenerateTimingReports)
                    {
                        _ = GenerateReportAsync();
                    }
                }
            }

            Log.Information("Stopped timer");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to stop timer");
        }
    }

    private bool CanStop() => IsRunning || IsPaused;

    /// <summary>
    /// Adjusts the talk duration based on adaptive timing when in automatic mode.
    /// This helps keep meetings on schedule by proportionally adjusting remaining talks.
    /// </summary>
    private void AdjustForAdaptiveTime()
    {
        try
        {
            if (TalkId > 0 && IsAutoMode)
            {
                var newDuration = _adaptiveTimerService.CalculateAdaptedDuration(TalkId);
                if (newDuration != null && SelectedTalk != null)
                {
                    Log.Debug("Adaptive timer: Adjusting duration from {Original} to {Adapted}",
                        SelectedTalk.ActualDuration, newDuration.Value);

                    SelectedTalk.AdaptedDuration = newDuration.Value;
                    SetDurationStringAttributes();
                    TargetSeconds = (int)SelectedTalk.ActualDuration.TotalSeconds;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not adjust for adaptive time");
        }
    }

    /// <summary>
    /// Notifies of bad timing (overrun/underrun) using adaptive calculation in auto mode.
    /// </summary>
    private void NotifyOfBadTimingIfRequired(TimeSpan variance)
    {
        if (IsAutoMode)
        {
            // Use adaptive overrun calculation for more accurate meeting-level feedback
            var overrun = _adaptiveTimerService.CalculateMeetingOverrun(TalkId);
            if (overrun != null)
            {
                _overrunService.NotifyOfBadTiming(overrun.Value);
                return;
            }
        }

        // Fall back to simple variance calculation
        _overrunService.NotifyOfBadTiming(variance);
    }

    #region Meeting-level timing data (matching WPF report structure)

    /// <summary>
    /// Records meeting structure data when a timer starts (auto mode only).
    /// Detects first talk, first talk after interval, and records individual talk start.
    /// </summary>
    private void StoreTimerStartData()
    {
        if (SelectedTalk == null) return;

        if (IsAutoMode)
        {
            if (IsFirstTalk(SelectedTalk.Id))
            {
                StoreTimerDataForStartOfMeeting();
            }

            if (IsFirstTalkAfterInterval(SelectedTalk.Id))
            {
                var prevTalk = GetPreviousTalk(SelectedTalk.Id);
                StoreTimerDataForInterim(prevTalk?.IsStudentTalk ?? false);
            }
        }

        _timingDataService.InsertTimerStart(
            SelectedTalk.Name,
            false, // isSongSegment
            SelectedTalk.IsStudentTalk,
            SelectedTalk.PlannedDuration,
            SelectedTalk.AdaptedDuration ?? SelectedTalk.PlannedDuration);
    }

    /// <summary>
    /// Records meeting start time, planned end, and opening song segment.
    /// </summary>
    private void StoreTimerDataForStartOfMeeting()
    {
        var startTime = CalculateStartOfMeeting();
        _timingDataService.InsertMeetingStart(startTime);

        const int totalMtgLengthMins = 105;
        var plannedEndTime = startTime.AddMinutes(totalMtgLengthMins);
        _timingDataService.InsertPlannedMeetingEnd(plannedEndTime);

        _timingDataService.InsertSongSegment(
            startTime,
            _localizationService.GetString("INTRO_SEGMENT") ?? "Introductory Segment",
            TimeSpan.FromMinutes(5));

        Log.Debug("Stored meeting start data: start={Start}, plannedEnd={PlannedEnd}", startTime, plannedEndTime);
    }

    /// <summary>
    /// Records an interim song segment between meeting parts.
    /// </summary>
    private void StoreTimerDataForInterim(bool allowForCounselTime)
    {
        var lastItemStop = _timingDataService.LastTimerStop;
        var interimStart = lastItemStop.AddSeconds(allowForCounselTime ? 75 : 15);

        _timingDataService.InsertSongSegment(
            interimStart,
            _localizationService.GetString("INTERIM_SEGMENT") ?? "Interim Segment",
            new TimeSpan(0, 3, 20));

        Log.Debug("Stored interim segment data");
    }

    /// <summary>
    /// Records concluding song and actual meeting end time.
    /// </summary>
    private void StoreEndOfMeetingData()
    {
        if (!IsAutoMode) return;

        var songStart = _dateTimeService.Now().AddSeconds(5);
        var actualMeetingEnd = songStart.AddMinutes(5);

        _timingDataService.InsertConcludingSongSegment(
            songStart,
            actualMeetingEnd,
            _localizationService.GetString("CONCLUDING_SEGMENT") ?? "Concluding Segment",
            TimeSpan.FromMinutes(5));

        _timingDataService.InsertActualMeetingEnd(actualMeetingEnd);

        Log.Debug("Stored end of meeting data: songStart={SongStart}, meetingEnd={MeetingEnd}", songStart, actualMeetingEnd);
    }

    private DateTime CalculateStartOfMeeting()
    {
        return DateUtils.GetNearestQuarterOfAnHour(_dateTimeService.Now());
    }

    private bool IsFirstTalk(int talkId)
    {
        var talks = _scheduleService.GetTalkScheduleItems().ToArray();
        return talks.Length > 0 && talkId == talks.First().Id;
    }

    private static bool IsFirstTalkAfterInterval(int talkId)
    {
        var talkType = (TalkTypesAutoMode)talkId;
        return talkType == TalkTypesAutoMode.LivingPart1 ||
               talkType == TalkTypesAutoMode.Watchtower;
    }

    private TalkScheduleItem? GetPreviousTalk(int talkId)
    {
        var talks = _scheduleService.GetTalkScheduleItems();
        TalkScheduleItem? prevTalk = null;
        foreach (var talk in talks)
        {
            if (talk.Id == talkId)
            {
                break;
            }
            prevTalk = talk;
        }
        return prevTalk;
    }

    #endregion

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
        var newSecs = Math.Max(TargetSeconds + seconds, 0); // Minimum 0 seconds
        if (newSecs <= maxTimerSecs)
        {
            var newDuration = TimeSpan.FromSeconds(newSecs);
            SelectedTalk.ModifiedDuration = newDuration;
            _scheduleService.SetModifiedDuration(SelectedTalk.Id, newDuration);
            TargetSeconds = newSecs;

            if (IsRunning || IsPaused)
            {
                // Timer is active — tell the service about the new target
                // so remaining-time calculations are correct from the next
                // tick. Don't reset elapsed; just update the display with
                // the current elapsed value.
                _timerService.AdjustTarget(newSecs);
                UpdateTimeDisplay(TargetSeconds, ElapsedSeconds);
            }
            else
            {
                UpdateTimeDisplay(TargetSeconds, 0);
            }

            SetDurationStringAttributes();
        }
    }

    private bool CanAdjustTime() => SelectedTalk?.Editable == true;

    [RelayCommand(CanExecute = nameof(CanSkipTalk))]
    private void SkipTalk()
    {
        if (SelectedTalk == null) return;

        var nextId = _scheduleService.GetNext(SelectedTalk.Id);
        if (nextId > 0)
        {
            SelectedTalk = Talks.FirstOrDefault(t => t.Id == nextId);
            Log.Information("Skipped to next talk: {TalkName}", SelectedTalk?.Name);
        }
    }

    private bool CanSkipTalk() => !IsRunning && !IsPaused && SelectedTalk != null;

    // --- File-based schedule picker + export ---

    [ObservableProperty]
    private ObservableCollection<string> _scheduleFiles = [];

    [ObservableProperty]
    private string? _selectedScheduleFile;

    partial void OnSelectedScheduleFileChanged(string? value)
    {
        if (value == null) return;
        var options = _optionsService.GetOptions();
        options.SelectedScheduleFile = value;
        _optionsService.SaveOptions(options);
        RefreshTalks();
    }

    public void RefreshScheduleFiles()
    {
        var files = Utils.ScheduleExporter.GetAvailableTemplates();

        // First-use seeding: if there are no templates yet and we have
        // talks loaded (from a previous Auto/Manual session), export
        // the current schedule as a starting template so the user has
        // something to work with immediately.
        if (files.Length == 0 && Talks.Count > 0)
        {
            var seedPath = System.IO.Path.Combine(
                Utils.FileUtils.GetScheduleTemplatesFolder(), "default.xml");
            Utils.ScheduleExporter.Export(Talks, seedPath);
            files = Utils.ScheduleExporter.GetAvailableTemplates();
        }

        ScheduleFiles.Clear();
        foreach (var f in files)
        {
            ScheduleFiles.Add(System.IO.Path.GetFileName(f));
        }

        var current = _optionsService.GetOptions().SelectedScheduleFile;
        if (!string.IsNullOrEmpty(current) && ScheduleFiles.Contains(current))
        {
            SelectedScheduleFile = current;
        }
        else if (ScheduleFiles.Count > 0)
        {
            SelectedScheduleFile = ScheduleFiles[0];
        }

        OnPropertyChanged(nameof(IsFileBasedMode));
    }

    [RelayCommand]
    private void ExportScheduleAsTemplate()
    {
        var folder = Utils.FileUtils.GetScheduleTemplatesFolder();
        var talks = Talks;

        if (talks.Count == 0)
        {
            StatusText = "No talks to export";
            return;
        }

        // Generate a unique filename based on the current mode/meeting type
        var prefix = _optionsService.OperatingMode switch
        {
            OperatingMode.Automatic => _optionsService.MidWeekOrWeekend == MidWeekOrWeekend.MidWeek ? "midweek" : "weekend",
            OperatingMode.Manual => "manual",
            OperatingMode.ScheduleFile => "custom",
            _ => "schedule"
        };

        var timestamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var filename = $"{prefix}-{timestamp}.xml";
        var path = System.IO.Path.Combine(folder, filename);

        Utils.ScheduleExporter.Export(talks, path);
        StatusText = $"Saved: {filename}";

        RefreshScheduleFiles();
    }

    // Quick-set: user types minutes in manual mode and presses Enter.
    [ObservableProperty]
    private decimal _quickSetMinutes = 5;

    /// <summary>
    /// Called from the code-behind when Enter is pressed on the quick-set
    /// input. Sets the timer to the specified minutes and starts it.
    /// </summary>
    public void QuickSetAndStart()
    {
        if (SelectedTalk == null || IsRunning || IsPaused) return;

        var secs = (int)(QuickSetMinutes * 60);
        if (secs <= 0 || secs > 99 * 60) return;

        var duration = TimeSpan.FromSeconds(secs);
        SelectedTalk.ModifiedDuration = duration;
        _scheduleService.SetModifiedDuration(SelectedTalk.Id, duration);
        TargetSeconds = secs;
        UpdateTimeDisplay(TargetSeconds, 0);
        SetDurationStringAttributes();

        StartCommand.Execute(null);
    }

    /// <summary>
    /// Adjust timer duration via mouse wheel (called from code-behind)
    /// </summary>
    public void AdjustTimerByWheel(int seconds)
    {
        AdjustTimerInternal(seconds);
    }

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
        _countdownWindow?.Close();
        _countdownWindow = null;
        UpdateMainWindowTopmost();
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
        // Default colors
        var dimBrush = new SolidColorBrush(Color.Parse("#bba991"));
        var activeBrush = new SolidColorBrush(Color.Parse("#f3dcbc"));

        if (SelectedTalk == null)
        {
            Duration1String = null;
            Duration2String = null;
            Duration3String = null;
            Duration1Colour = dimBrush;
            Duration2Colour = dimBrush;
            Duration3Colour = dimBrush;
            return;
        }

        var adaptiveMode = _optionsService.GetAdaptiveMode();

        // Duration1 is always the original duration
        Duration1String = TimeFormatter.FormatTimerDisplayString((int)SelectedTalk.OriginalDuration.TotalSeconds);
        Duration1Tooltip = _localizationService.GetString("DURATION_ORIGINAL") ?? "Original";

        if (SelectedTalk.ModifiedDuration != null)
        {
            // User has modified the duration
            Duration2String = TimeFormatter.FormatTimerDisplayString((int)SelectedTalk.ModifiedDuration.Value.TotalSeconds);
            Duration2Tooltip = _localizationService.GetString("DURATION_MODIFIED") ?? "Modified";

            // Show adapted duration if available and applicable
            var showAdaptedDuration = SelectedTalk.AdaptedDuration != null &&
                                      (adaptiveMode == AdaptiveMode.TwoWay ||
                                       SelectedTalk.AdaptedDuration.Value < SelectedTalk.ModifiedDuration.Value);

            if (showAdaptedDuration)
            {
                Duration3String = TimeFormatter.FormatTimerDisplayString((int)SelectedTalk.AdaptedDuration!.Value.TotalSeconds);
                Duration3Tooltip = _localizationService.GetString("DURATION_ADAPTED") ?? "Adapted";
            }
            else
            {
                Duration3String = null;
            }
        }
        else if (SelectedTalk.AdaptedDuration != null)
        {
            // Only adapted duration (no modified)
            Duration2String = TimeFormatter.FormatTimerDisplayString((int)SelectedTalk.AdaptedDuration.Value.TotalSeconds);
            Duration2Tooltip = _localizationService.GetString("DURATION_ADAPTED") ?? "Adapted";
            Duration3String = null;
        }
        else
        {
            // Only original duration
            Duration2String = null;
            Duration3String = null;
        }

        // Set colors - highlight the active (rightmost non-empty) duration
        Duration1Colour = dimBrush;
        Duration2Colour = dimBrush;
        Duration3Colour = dimBrush;

        if (!string.IsNullOrEmpty(Duration3String))
        {
            Duration3Colour = activeBrush;
        }
        else if (!string.IsNullOrEmpty(Duration2String))
        {
            Duration2Colour = activeBrush;
        }
        else
        {
            Duration1Colour = activeBrush;
        }

        OnPropertyChanged(nameof(Duration1ArrowString));
        OnPropertyChanged(nameof(Duration2ArrowString));
    }

    private OnlyT.Avalonia.Views.SettingsWindow? _currentSettingsWindow;

    [RelayCommand]
    private void ShowSettings()
    {
        // If a settings window is already open, bring it to the front
        // instead of stacking another one on top. Clicking the settings
        // button multiple times previously created duplicate windows.
        if (_currentSettingsWindow != null)
        {
            try
            {
                _currentSettingsWindow.Activate();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to activate existing settings window");
            }
            return;
        }

        var settingsViewModel = new SettingsViewModel(_optionsService, _monitorService, _localizationService, this, _bellService, _firewallService, _logLevelSwitchService);
        var settingsWindow = new OnlyT.Avalonia.Views.SettingsWindow
        {
            DataContext = settingsViewModel
        };
        settingsViewModel.RequestClose += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try { settingsWindow.Close(); }
                catch (Exception ex) { Log.Warning(ex, "Failed to close settings on save"); }
            });
        };
        settingsWindow.Closed += OnSettingsWindowClosed;
        _currentSettingsWindow = settingsWindow;
        settingsWindow.Show();
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        if (sender is OnlyT.Avalonia.Views.SettingsWindow window)
        {
            window.Closed -= OnSettingsWindowClosed;
            if (ReferenceEquals(window, _currentSettingsWindow))
            {
                _currentSettingsWindow = null;
            }
        }

        // Refresh state from options after settings change (like WPF Activated callback)
        var options = _optionsService.GetOptions();
        CountUp = options.CountUp;

        OnPropertyChanged(nameof(AllowCountUpDownToggle));
        OnPropertyChanged(nameof(ShowUpDownButton));
        OnPropertyChanged(nameof(IsBellVisible));
        OnPropertyChanged(nameof(ShouldShowCircuitVisitToggle));
        OnPropertyChanged(nameof(IsCircuitVisit));

        // Refresh talks in case schedule-affecting settings changed
        RefreshTalks();

        // Update main window topmost in case AlwaysOnTop changed
        UpdateMainWindowTopmost();
    }

    [RelayCommand]
    private void ToggleCountdown()
    {
        if (_countdownWindow != null)
        {
            _countdownWindow.Close();
            _countdownWindow = null;
            UpdateMainWindowTopmost();
            return;
        }

        // Calculate actual seconds until next meeting
        _countdownTriggerService.UpdateTriggerPeriods();
        var secondsUntilMeeting = _countdownTriggerService.GetSecondsUntilNextMeeting();

        if (secondsUntilMeeting is > 0)
        {
            ShowCountdownWindow(secondsUntilMeeting.Value);
        }
        else
        {
            // No upcoming meeting configured, use configured duration as fallback
            ShowCountdownWindow(_optionsService.CountdownDurationMins * 60);
        }
    }

    private void ShowCountdownWindow(int secondsRemaining)
    {
        var countdownViewModel = new CountdownViewModel(_optionsService);
        countdownViewModel.CountdownTotalSeconds = secondsRemaining;

        _countdownWindow = new OnlyT.Avalonia.Views.CountdownWindow
        {
            DataContext = countdownViewModel,
            Topmost = true
        };

        countdownViewModel.SetCallbacks(
            closeAction: () =>
            {
                _countdownWindow?.Close();
                _countdownWindow = null;
                UpdateMainWindowTopmost();
            },
            timeUpAction: () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _countdownWindow?.Close();
                    _countdownWindow = null;
                    _isCountdownDone = true;
                    UpdateMainWindowTopmost();
                });
            });

        _countdownWindow.TimeUpEvent += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_countdownWindow != null)
                {
                    var vm = _countdownWindow.DataContext as CountdownViewModel;
                    vm?.OnTimeUp();
                }
            });
        };

        // Position on same monitor as the timer output window
        PositionCountdownOnTimerMonitor();

        _countdownWindow.Show();
        _countdownWindow.Start(secondsRemaining);
        UpdateMainWindowTopmost();
    }

    private void PositionCountdownOnTimerMonitor()
    {
        if (_countdownWindow == null) return;

        // If there's a saved placement, let the window restore it
        var savedPlacement = _optionsService.GetOptions().CountdownWindowPlacement;
        if (savedPlacement != null)
            return;

        // Use the same monitor as the timer output window
        if (_timerOutputWindow != null)
        {
            var timerPos = _timerOutputWindow.Position;
            var monitors = _monitorService.GetMonitors();

            // Find which monitor the timer output is on
            var timerMonitor = monitors.FirstOrDefault(m =>
                timerPos.X >= m.Left && timerPos.X < m.Left + m.Width &&
                timerPos.Y >= m.Top && timerPos.Y < m.Top + m.Height);

            if (timerMonitor != null)
            {
                // Center the countdown window on that monitor
                var x = timerMonitor.Left + (timerMonitor.Width - (int)_countdownWindow.Width) / 2;
                var y = timerMonitor.Top + (timerMonitor.Height - (int)_countdownWindow.Height) / 2;
                _countdownWindow.Position = new global::Avalonia.PixelPoint(x, y);
                return;
            }
        }

        // Fallback: use the configured monitor from settings
        var options = _optionsService.GetOptions();
        if (!string.IsNullOrEmpty(options.MonitorId))
        {
            var allMonitors = _monitorService.GetMonitors();
            var targetMonitor = allMonitors.FirstOrDefault(m => m.MonitorId == options.MonitorId)
                                ?? allMonitors.FirstOrDefault(m => m.IsPrimary)
                                ?? allMonitors.FirstOrDefault();

            if (targetMonitor != null)
            {
                var x = targetMonitor.Left + (targetMonitor.Width - (int)_countdownWindow.Width) / 2;
                var y = targetMonitor.Top + (targetMonitor.Height - (int)_countdownWindow.Height) / 2;
                _countdownWindow.Position = new global::Avalonia.PixelPoint(x, y);
            }
        }
    }

    private void InitHeartbeatTimer()
    {
        _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _heartbeatTimer.Tick += (_, _) => HeartbeatTimerTick();
        _heartbeatTimer.Start();
    }

    private void HeartbeatTimerTick()
    {
        ManageCountdownOnHeartbeat();
    }

    private void ManageCountdownOnHeartbeat()
    {
        if (_optionsService.CountdownDurationMins <= 0)
            return;

        if (_countdownWindow != null || _isCountdownDone)
            return;

        _countdownTriggerService.UpdateTriggerPeriods();

        if (_countdownTriggerService.IsInCountdownPeriod(out var secondsRemaining))
        {
            Log.Information("Auto-triggering countdown with {Remaining}s remaining", secondsRemaining);
            ShowCountdownWindow(secondsRemaining);
        }
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
                weekendIncludesFriday: _optionsService.GetOptions().WeekendIncludesFriday,
                commandLineIdentifier: Program.CommandLineArgs.OptionsIdentifier);

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

    /// <summary>
    /// Updates the main window's Topmost state based on options and output window visibility.
    /// In WPF, the main window is always-on-top when either the option is set or an output window is visible.
    /// </summary>
    private void UpdateMainWindowTopmost()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != null)
            {
                var options = _optionsService.GetOptions();
                desktop.MainWindow.Topmost = options.AlwaysOnTop ||
                    _timerOutputWindow != null ||
                    _countdownWindow != null;
            }
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
        UpdateMainWindowTopmost();
    }
}
