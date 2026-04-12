using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Avalonia.Utils;
using Serilog;

namespace OnlyT.Avalonia.Services;

/// <summary>
/// Simple options service using JSON file storage
/// </summary>
public class SimpleOptionsService : IOptionsService
{
    // Deliberately permissive: MissingMemberHandling.Ignore tolerates any
    // extra keys we find in the file (e.g. fields from a newer build or
    // fields we inherited from the upstream WPF file during the first-run
    // seed). Error handler swallows per-property failures so one bad entry
    // never blocks the rest of the options from loading.
    private static readonly JsonSerializerSettings LenientReadSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Error = (_, args) =>
        {
            Log.Warning(args.ErrorContext.Error, "Ignoring bad options entry at {Path}", args.ErrorContext.Path);
            args.ErrorContext.Handled = true;
        }
    };

    private readonly string _optionsFilePath;
    private readonly string _wpfOptionsFilePath;
    private AppOptions? _cachedOptions;

    public SimpleOptionsService()
    {
        _optionsFilePath = FileUtils.GetOptionsFilePath();
        _wpfOptionsFilePath = FileUtils.GetWpfOptionsFilePath();
    }

    public event EventHandler? OptionsChanged;

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

        // First run: no Avalonia options file yet. Seed it from the upstream
        // WPF options.json if that exists so users upgrading from the WPF
        // version keep their settings. After this, the fork writes only to
        // its own file and never touches the WPF one again.
        if (!File.Exists(_optionsFilePath) && File.Exists(_wpfOptionsFilePath))
        {
            try
            {
                File.Copy(_wpfOptionsFilePath, _optionsFilePath);
                Log.Information(
                    "Seeded Avalonia options from WPF file: {Source} -> {Dest}",
                    _wpfOptionsFilePath,
                    _optionsFilePath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to seed Avalonia options from WPF file");
            }
        }

        try
        {
            if (File.Exists(_optionsFilePath))
            {
                var json = File.ReadAllText(_optionsFilePath);
                _cachedOptions = JsonConvert.DeserializeObject<AppOptions>(json, LenientReadSettings);
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

            // Invoke each subscriber individually so one failing handler
            // doesn't prevent subsequent handlers from running. Previously
            // a single Invoke + catch swallowed the first exception and
            // silently skipped all remaining subscribers.
            if (OptionsChanged != null)
            {
                foreach (var handler in OptionsChanged.GetInvocationList())
                {
                    try
                    {
                        ((EventHandler)handler)(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "OptionsChanged subscriber threw");
                    }
                }
            }
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
