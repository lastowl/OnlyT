namespace OnlyT.Avalonia.Services;

using System;
using OnlyT.Avalonia.Services.Options;

/// <summary>
/// Service for managing application options/settings
/// </summary>
public interface IOptionsService
{
    /// <summary>
    /// Get the current options
    /// </summary>
    AppOptions GetOptions();

    /// <summary>
    /// Save options
    /// </summary>
    void SaveOptions(AppOptions options);

    /// <summary>
    /// Raised after options are successfully written. View models listen to
    /// this so settings changes take effect live instead of after a restart.
    /// </summary>
    event EventHandler? OptionsChanged;

    /// <summary>
    /// Whether bell is enabled
    /// </summary>
    bool IsBellEnabled { get; }

    /// <summary>
    /// Whether bell should play automatically
    /// </summary>
    bool AutoBell { get; }

    /// <summary>
    /// Current operating mode
    /// </summary>
    OperatingMode OperatingMode { get; }

    /// <summary>
    /// Type of meeting (Midweek or Weekend)
    /// </summary>
    MidWeekOrWeekend MidWeekOrWeekend { get; }

    /// <summary>
    /// Whether this is a circuit visit
    /// </summary>
    bool IsCircuitVisit { get; set; }

    /// <summary>
    /// Whether to show the circuit visit toggle control
    /// </summary>
    bool ShowCircuitVisitToggle { get; }

    /// <summary>
    /// Whether to allow count up/down toggle
    /// </summary>
    bool AllowCountUpToggle { get; }

    /// <summary>
    /// Whether to use flat clock style
    /// </summary>
    bool IsFlatClockStyle { get; }

    /// <summary>
    /// Whether the Web API is enabled
    /// </summary>
    bool IsApiEnabled { get; }

    /// <summary>
    /// Whether API throttling is enabled
    /// </summary>
    bool IsApiThrottled { get; }

    /// <summary>
    /// API access code for authentication (empty = no auth required)
    /// </summary>
    string ApiAccessCode { get; }

    /// <summary>
    /// Port for the HTTP server
    /// </summary>
    int HttpServerPort { get; }

    /// <summary>
    /// Whether to show time of day under the timer
    /// </summary>
    bool ShowTimeOfDayUnderTimer { get; }

    /// <summary>
    /// Whether to show seconds on digital clock display
    /// </summary>
    bool ShowDigitalSeconds { get; }

    /// <summary>
    /// Countdown duration in minutes before meeting
    /// </summary>
    int CountdownDurationMins { get; }

    /// <summary>
    /// Which elements to show on the countdown display (dial, digital, or both)
    /// </summary>
    Options.ElementsToShow CountdownElementsToShow { get; }

    /// <summary>
    /// Whether to show duration sector on analog clock
    /// </summary>
    bool ShowDurationSector { get; }

    /// <summary>
    /// Whether to flash the timer display during overtime
    /// </summary>
    bool FlashTimerWhenOvertime { get; }

    /// <summary>
    /// Whether to ring bell when entering overtime
    /// </summary>
    bool BellOnOvertime { get; }

    /// <summary>
    /// Whether to show mouse pointer in the timer display window
    /// </summary>
    bool ShowMousePointerInTimerDisplay { get; }

    /// <summary>
    /// Analogue clock width as percentage of window width (10-100)
    /// </summary>
    int AnalogueClockWidthPercent { get; }

    /// <summary>
    /// Clock display mode (Analogue, Digital, or Both)
    /// </summary>
    FullScreenClockMode FullScreenClockMode { get; }

    /// <summary>
    /// Bell volume percentage (0-100)
    /// </summary>
    int BellVolumePercent { get; }

    /// <summary>
    /// Clock hour display format
    /// </summary>
    ClockHourFormat ClockHourFormat { get; }

    /// <summary>
    /// Whether to use dark mode theme
    /// </summary>
    bool IsDarkMode { get; }

    /// <summary>
    /// Adaptive timer mode for midweek meetings
    /// </summary>
    AdaptiveMode MidWeekAdaptiveMode { get; }

    /// <summary>
    /// Adaptive timer mode for weekend meetings
    /// </summary>
    AdaptiveMode WeekendAdaptiveMode { get; }

    /// <summary>
    /// Whether to show reminders when too much time passes between talks
    /// </summary>
    bool TimerReminder { get; }

    /// <summary>
    /// Whether to show a gradient background on the timer display (when in full screen mode)
    /// </summary>
    bool ShowBackgroundOnTimer { get; }

    /// <summary>
    /// Whether the countdown window should be transparent (when in full screen mode)
    /// </summary>
    bool IsCountdownWindowTransparent { get; }

    /// <summary>
    /// Whether to persist final timer values for student talks
    /// </summary>
    bool PersistStudentTime { get; }

    /// <summary>
    /// Configured meeting start times
    /// </summary>
    MeetingStartTimes MeetingStartTimes { get; }

    /// <summary>
    /// Current language/culture code
    /// </summary>
    string Culture { get; }

    /// <summary>
    /// Whether to shrink window instead of minimizing
    /// </summary>
    bool ShrinkOnMinimise { get; }

    /// <summary>
    /// Whether to show overrun/underrun notifications when talks end
    /// </summary>
    bool OverrunNotifications { get; }

    /// <summary>
    /// Logging level (Verbose, Debug, Information, Warning, Error, Fatal)
    /// </summary>
    string LogEventLevel { get; }

    /// <summary>
    /// Whether Friday should be considered part of the weekend for auto-switching
    /// </summary>
    bool WeekendIncludesFriday { get; }

    /// <summary>
    /// Whether the timer counts up by default (false = count down)
    /// </summary>
    bool CountUp { get; }

    /// <summary>
    /// Whether to generate timing reports at end of meeting
    /// </summary>
    bool GenerateTimingReports { get; }

    /// <summary>
    /// Checks if the current day is a weekend day (for auto-switching between midweek/weekend schedules)
    /// </summary>
    bool IsNowWeekend();

    /// <summary>
    /// Sets the meeting type (midweek/weekend) and saves options
    /// </summary>
    void SetMidWeekOrWeekend(MidWeekOrWeekend value);

    /// <summary>
    /// Gets the current adaptive mode based on whether it's midweek or weekend
    /// </summary>
    AdaptiveMode GetAdaptiveMode();
}

/// <summary>
/// Application options for MVP
/// </summary>
public class AppOptions
{
    /// <summary>
    /// Selected monitor ID for timer display
    /// </summary>
    public string? MonitorId { get; set; }

    /// <summary>
    /// Whether to show timer in full screen
    /// </summary>
    public bool FullScreenMode { get; set; }

    /// <summary>
    /// Whether timer window is always on top
    /// </summary>
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>
    /// Whether to play sound when timer ends
    /// </summary>
    public bool IsBellEnabled { get; set; } = true;

    /// <summary>
    /// Whether bell should play automatically
    /// </summary>
    public bool AutoBell { get; set; } = true;

    /// <summary>
    /// Default timer duration in minutes
    /// </summary>
    public int DefaultDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Operating mode (Manual/Automatic/ScheduleFile)
    /// </summary>
    public OperatingMode OperatingMode { get; set; } = OperatingMode.Automatic;

    /// <summary>
    /// Type of meeting (Midweek or Weekend)
    /// </summary>
    public MidWeekOrWeekend MidWeekOrWeekend { get; set; } = MidWeekOrWeekend.MidWeek;

    /// <summary>
    /// Whether this is a circuit visit
    /// </summary>
    public bool IsCircuitVisit { get; set; }

    /// <summary>
    /// Whether to show the circuit visit toggle control
    /// </summary>
    public bool ShowCircuitVisitToggle { get; set; } = false;

    /// <summary>
    /// Whether to allow count up/down toggle
    /// </summary>
    public bool AllowCountUpToggle { get; set; } = false;

    /// <summary>
    /// Whether to use flat clock style
    /// </summary>
    public bool IsFlatClockStyle { get; set; }

    /// <summary>
    /// Whether the Web API is enabled
    /// </summary>
    public bool IsApiEnabled { get; set; } = false;

    /// <summary>
    /// Whether API throttling is enabled (rate limiting)
    /// </summary>
    public bool IsApiThrottled { get; set; } = true;

    /// <summary>
    /// API access code for authentication (empty = no authentication required)
    /// </summary>
    public string ApiAccessCode { get; set; } = string.Empty;

    /// <summary>
    /// Port for the HTTP server (default 8096)
    /// </summary>
    public int HttpServerPort { get; set; } = 8096;

    /// <summary>
    /// Whether to show time of day under the timer
    /// </summary>
    public bool ShowTimeOfDayUnderTimer { get; set; } = false;

    /// <summary>
    /// Whether to show seconds on digital clock display
    /// </summary>
    public bool ShowDigitalSeconds { get; set; } = true;

    /// <summary>
    /// Whether to show duration sector on analog clock
    /// </summary>
    public bool ShowDurationSector { get; set; } = true;

    /// <summary>
    /// Countdown duration in minutes before meeting (default 5 minutes)
    /// </summary>
    public int CountdownDurationMins { get; set; } = 5;

    /// <summary>
    /// Which elements to show on the countdown display
    /// </summary>
    public ElementsToShow CountdownElementsToShow { get; set; } = ElementsToShow.DialAndDigital;

    /// <summary>
    /// Whether to flash the timer display during overtime
    /// </summary>
    public bool FlashTimerWhenOvertime { get; set; } = true;

    /// <summary>
    /// Whether to ring bell when entering overtime
    /// </summary>
    public bool BellOnOvertime { get; set; } = false;

    /// <summary>
    /// Whether to show mouse pointer in the timer display window (default: false for clean presentation)
    /// </summary>
    public bool ShowMousePointerInTimerDisplay { get; set; } = false;

    /// <summary>
    /// Analogue clock width as percentage of window width (10-100, default: 80)
    /// </summary>
    public int AnalogueClockWidthPercent { get; set; } = 50;

    /// <summary>
    /// Clock display mode (Analogue, Digital, or Both) - default: Digital
    /// </summary>
    public FullScreenClockMode FullScreenClockMode { get; set; } = FullScreenClockMode.AnalogueAndDigital;

    /// <summary>
    /// Bell volume percentage (0-100, default: 70)
    /// </summary>
    public int BellVolumePercent { get; set; } = 70;

    /// <summary>
    /// Clock hour display format (default: 24hr with leading zero)
    /// </summary>
    public ClockHourFormat ClockHourFormat { get; set; } = ClockHourFormat.Format24LeadingZero;

    /// <summary>
    /// Whether to use dark mode theme (default: false = light mode)
    /// </summary>
    public bool IsDarkMode { get; set; } = false;

    /// <summary>
    /// Avalonia-only: render the operator page with a layout that matches
    /// the original WPF OnlyT. WPF ignores this field when reading its own
    /// options.json, so there is no compatibility risk. Default: false.
    /// </summary>
    public bool ClassicMode { get; set; } = false;

    /// <summary>
    /// Adaptive timer mode for midweek meetings (default: None)
    /// </summary>
    public AdaptiveMode MidWeekAdaptiveMode { get; set; } = AdaptiveMode.None;

    /// <summary>
    /// Adaptive timer mode for weekend meetings (default: None)
    /// </summary>
    public AdaptiveMode WeekendAdaptiveMode { get; set; } = AdaptiveMode.None;

    /// <summary>
    /// Whether to show reminders when too much time passes between talks (default: false)
    /// </summary>
    public bool TimerReminder { get; set; } = false;

    /// <summary>
    /// Whether to show a gradient background on the timer display (default: true)
    /// </summary>
    public bool ShowBackgroundOnTimer { get; set; } = false;

    /// <summary>
    /// Whether the countdown window should be transparent (default: false)
    /// </summary>
    public bool IsCountdownWindowTransparent { get; set; } = false;

    /// <summary>
    /// Whether to persist final timer values for student talks (default: true)
    /// </summary>
    public bool PersistStudentTime { get; set; } = true;

    /// <summary>
    /// Configured meeting start times (as text, one per line)
    /// </summary>
    public string MeetingStartTimesText { get; set; } = string.Empty;

    /// <summary>
    /// Current language/culture code (default: en-GB)
    /// </summary>
    public string Culture { get; set; } = "en-GB";

    /// <summary>
    /// Main window position and size (normal mode)
    /// </summary>
    public WindowPlacement? MainWindowPlacement { get; set; }

    /// <summary>
    /// Main window position and size (shrunk mode)
    /// </summary>
    public WindowPlacement? MainWindowPlacementShrunk { get; set; }

    /// <summary>
    /// Whether to use shrunk placement at startup
    /// </summary>
    public bool UseShrunkPlacementAtStart { get; set; }

    /// <summary>
    /// Whether to shrink window instead of minimizing (default: false)
    /// </summary>
    public bool ShrinkOnMinimise { get; set; }

    /// <summary>
    /// Whether to show overrun/underrun notifications when talks end (default: true)
    /// </summary>
    public bool OverrunNotifications { get; set; } = false;

    /// <summary>
    /// Logging level (default: Information)
    /// </summary>
    public string LogEventLevel { get; set; } = "Information";

    /// <summary>
    /// Timer output window position and size
    /// </summary>
    public WindowPlacement? TimerOutputWindowPlacement { get; set; }

    /// <summary>
    /// Countdown window position and size
    /// </summary>
    public WindowPlacement? CountdownWindowPlacement { get; set; }

    /// <summary>
    /// Whether Friday should be considered part of the weekend (default: false)
    /// </summary>
    public bool WeekendIncludesFriday { get; set; } = false;

    /// <summary>
    /// Whether the timer counts up by default (false = count down)
    /// </summary>
    public bool CountUp { get; set; } = false;

    /// <summary>
    /// Whether to generate timing reports at end of meeting
    /// </summary>
    public bool GenerateTimingReports { get; set; } = false;

    /// <summary>
    /// Sanitizes option values to ensure they are within valid ranges.
    /// </summary>
    public void Sanitize()
    {
        BellVolumePercent = Math.Clamp(BellVolumePercent, 0, 100);
        AnalogueClockWidthPercent = Math.Clamp(AnalogueClockWidthPercent, 0, 100);
        CountdownDurationMins = Math.Clamp(CountdownDurationMins, 1, 60);
        HttpServerPort = Math.Clamp(HttpServerPort, 1, 65535);

        if (string.IsNullOrEmpty(Culture))
        {
            Culture = "en-GB";
        }

        if (string.IsNullOrEmpty(LogEventLevel))
        {
            LogEventLevel = "Information";
        }
    }
}

/// <summary>
/// Window placement data for persistence
/// </summary>
public class WindowPlacement
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsMaximized { get; set; }
}
