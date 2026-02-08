using System;
using System.IO;
using Newtonsoft.Json;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Avalonia.Utils;
using Serilog;

namespace OnlyT.Avalonia.Services;

/// <summary>
/// Simple options service using JSON file storage
/// </summary>
public class SimpleOptionsService : IOptionsService
{
    private readonly string _optionsFilePath;
    private AppOptions? _cachedOptions;

    public SimpleOptionsService()
    {
        _optionsFilePath = FileUtils.GetOptionsFilePath();
    }

    public bool IsBellEnabled => GetOptions().IsBellEnabled;
    public bool AutoBell => GetOptions().AutoBell;
    public OperatingMode OperatingMode => GetOptions().OperatingMode;
    public MidWeekOrWeekend MidWeekOrWeekend => GetOptions().MidWeekOrWeekend;

    public bool IsCircuitVisit
    {
        get => GetOptions().IsCircuitVisit;
        set
        {
            var options = GetOptions();
            options.IsCircuitVisit = value;
            SaveOptions(options);
        }
    }

    public bool ShowCircuitVisitToggle => GetOptions().ShowCircuitVisitToggle;
    public bool AllowCountUpToggle => GetOptions().AllowCountUpToggle;
    public bool IsFlatClockStyle => GetOptions().IsFlatClockStyle;
    public bool ShowAnalogueClockOnOutput => GetOptions().ShowAnalogueClockOnOutput;
    public bool UseAnalogClock => GetOptions().UseAnalogClock;
    public bool IsApiEnabled => GetOptions().IsApiEnabled;
    public bool IsApiThrottled => GetOptions().IsApiThrottled;
    public string ApiAccessCode => GetOptions().ApiAccessCode;
    public int HttpServerPort => GetOptions().HttpServerPort;
    public bool ShowTimeOfDayUnderTimer => GetOptions().ShowTimeOfDayUnderTimer;
    public bool ShowDigitalSeconds => GetOptions().ShowDigitalSeconds;
    public bool ShowDurationSector => GetOptions().ShowDurationSector;
    public int CountdownDurationMins => GetOptions().CountdownDurationMins;
    public Options.ElementsToShow CountdownElementsToShow => GetOptions().CountdownElementsToShow;
    public bool FlashTimerWhenOvertime => GetOptions().FlashTimerWhenOvertime;
    public bool BellOnOvertime => GetOptions().BellOnOvertime;
    public bool ShowMousePointerInTimerDisplay => GetOptions().ShowMousePointerInTimerDisplay;
    public int AnalogueClockWidthPercent => GetOptions().AnalogueClockWidthPercent;
    public FullScreenClockMode FullScreenClockMode => GetOptions().FullScreenClockMode;
    public int BellVolumePercent => GetOptions().BellVolumePercent;
    public ClockHourFormat ClockHourFormat => GetOptions().ClockHourFormat;
    public bool IsDarkMode => GetOptions().IsDarkMode;
    public AdaptiveMode MidWeekAdaptiveMode => GetOptions().MidWeekAdaptiveMode;
    public AdaptiveMode WeekendAdaptiveMode => GetOptions().WeekendAdaptiveMode;
    public bool TimerReminder => GetOptions().TimerReminder;
    public bool ShowBackgroundOnTimer => GetOptions().ShowBackgroundOnTimer;
    public bool IsCountdownWindowTransparent => GetOptions().IsCountdownWindowTransparent;
    public bool PersistStudentTime => GetOptions().PersistStudentTime;

    public bool CountUp => GetOptions().CountUp;
    public bool GenerateTimingReports => GetOptions().GenerateTimingReports;
    public string Culture => GetOptions().Culture;
    public bool ShrinkOnMinimise => GetOptions().ShrinkOnMinimise;
    public bool OverrunNotifications => GetOptions().OverrunNotifications;
    public string LogEventLevel => GetOptions().LogEventLevel;
    public bool WeekendIncludesFriday => GetOptions().WeekendIncludesFriday;

    /// <summary>
    /// Checks if the current day is a weekend day (Saturday, Sunday, or optionally Friday)
    /// </summary>
    public bool IsNowWeekend()
    {
        var today = DateTime.Now.DayOfWeek;
        return today == DayOfWeek.Saturday ||
               today == DayOfWeek.Sunday ||
               (WeekendIncludesFriday && today == DayOfWeek.Friday);
    }

    /// <summary>
    /// Sets the meeting type and saves options
    /// </summary>
    public void SetMidWeekOrWeekend(MidWeekOrWeekend value)
    {
        var options = GetOptions();
        if (options.MidWeekOrWeekend != value)
        {
            options.MidWeekOrWeekend = value;
            SaveOptions(options);
            Log.Information("Meeting type auto-switched to: {MeetingType}", value);
        }
    }

    /// <summary>
    /// Gets the current adaptive mode based on whether it's midweek or weekend
    /// </summary>
    public AdaptiveMode GetAdaptiveMode()
    {
        var options = GetOptions();
        return MidWeekOrWeekend == MidWeekOrWeekend.MidWeek
            ? options.MidWeekAdaptiveMode
            : options.WeekendAdaptiveMode;
    }

    private MeetingStartTimes? _meetingStartTimes;
    public MeetingStartTimes MeetingStartTimes
    {
        get
        {
            if (_meetingStartTimes == null)
            {
                _meetingStartTimes = new MeetingStartTimes();
                _meetingStartTimes.FromText(GetOptions().MeetingStartTimesText);
            }
            return _meetingStartTimes;
        }
    }

    public AppOptions GetOptions()
    {
        if (_cachedOptions != null)
        {
            return _cachedOptions;
        }

        try
        {
            if (File.Exists(_optionsFilePath))
            {
                var json = File.ReadAllText(_optionsFilePath);
                _cachedOptions = JsonConvert.DeserializeObject<AppOptions>(json);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load options");
        }

        _cachedOptions ??= new AppOptions();
        _cachedOptions.Sanitize();
        return _cachedOptions;
    }

    public void SaveOptions(AppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            _cachedOptions = options;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_optionsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Log.Information("Created options directory: {Directory}", directory);
            }

            var json = JsonConvert.SerializeObject(options, Formatting.Indented);

            // Write to temp file first, then move (atomic operation)
            var tempPath = _optionsFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _optionsFilePath, overwrite: true);

            Log.Debug("Options saved successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Error(ex, "Access denied when saving options to {Path}", _optionsFilePath);
            throw new InvalidOperationException($"Cannot save options: access denied to {_optionsFilePath}", ex);
        }
        catch (IOException ex)
        {
            Log.Error(ex, "IO error when saving options to {Path}", _optionsFilePath);
            throw new InvalidOperationException($"Cannot save options: IO error writing to {_optionsFilePath}", ex);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save options");
            throw;
        }
    }
}
