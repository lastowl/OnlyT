using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnlyT.Avalonia.Resources;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.Localization;
using OnlyT.Avalonia.Services.LogLevelSwitch;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Avalonia.Utils;
using OnlyT.Core.Abstractions;
using Serilog.Events;

using ClockMode = OnlyT.Avalonia.Services.Options.FullScreenClockMode;

namespace OnlyT.Avalonia.ViewModels;

/// <summary>
/// Settings window view model
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    /// <summary>
    /// Raised after a successful Save so the host window can close itself.
    /// Keeps the view model view-agnostic while still letting the window
    /// dismiss on save without the user having to click the close button.
    /// </summary>
    public event System.EventHandler? RequestClose;

    private readonly IOptionsService _optionsService;
    private readonly IMonitorService _monitorService;
    private readonly ILocalizationService? _localizationService;
    private readonly OperatorPageViewModel? _operatorPageViewModel;
    private readonly IBellService? _bellService;
    private readonly IFirewallService? _firewallService;
    private readonly ILogLevelSwitchService? _logLevelSwitchService;

    [ObservableProperty]
    private bool _classicMode;

    [ObservableProperty]
    private bool _fullScreenMode;

    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private bool _isBellEnabled;

    [ObservableProperty]
    private bool _autoBell;

    [ObservableProperty]
    private bool _isCircuitVisit;

    [ObservableProperty]
    private OperatingMode _operatingMode;

    [ObservableProperty]
    private MidWeekOrWeekend _midWeekOrWeekend;

    [ObservableProperty]
    private bool _showTimeOfDayUnderTimer;

    [ObservableProperty]
    private bool _showDigitalSeconds;

    [ObservableProperty]
    private bool _showDurationSector;

    [ObservableProperty]
    private bool _isApiThrottled;

    [ObservableProperty]
    private string _apiAccessCode = string.Empty;

    [ObservableProperty]
    private bool _flashTimerWhenOvertime;

    [ObservableProperty]
    private bool _bellOnOvertime;

    [ObservableProperty]
    private bool _showMousePointerInTimerDisplay;

    [ObservableProperty]
    private int _analogueClockWidthPercent = 80;

    [ObservableProperty]
    private ClockMode _fullScreenClockMode = ClockMode.Digital;

    [ObservableProperty]
    private int _bellVolumePercent = 70;

    [ObservableProperty]
    private ClockHourFormatItem? _selectedClockHourFormat;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private AdaptiveMode _midWeekAdaptiveMode;

    [ObservableProperty]
    private AdaptiveMode _weekendAdaptiveMode;

    [ObservableProperty]
    private bool _timerReminder;

    [ObservableProperty]
    private bool _showBackgroundOnTimer;

    [ObservableProperty]
    private bool _isCountdownWindowTransparent;

    [ObservableProperty]
    private bool _persistStudentTime;

    [ObservableProperty]
    private bool _shrinkOnMinimise;

    [ObservableProperty]
    private bool _isApiEnabled;

    [ObservableProperty]
    private bool _showCircuitVisitToggle;

    [ObservableProperty]
    private bool _allowCountUpToggle;

    [ObservableProperty]
    private bool _countUp;

    [ObservableProperty]
    private bool _weekendIncludesFriday;

    [ObservableProperty]
    private bool _overrunNotifications;

    [ObservableProperty]
    private bool _generateTimingReports;

    [ObservableProperty]
    private bool _showExportScheduleButton;

    [ObservableProperty]
    private int _httpServerPort = 8096;

    [ObservableProperty]
    private string _meetingStartTimesText = string.Empty;

    [ObservableProperty]
    private int _countdownDurationMins = 5;

    [ObservableProperty]
    private ElementsToShow _countdownElementsToShow = ElementsToShow.DialAndDigital;

    public ElementsToShow[] CountdownElementsToShowOptions { get; } =
    [
        ElementsToShow.DialAndDigital,
        ElementsToShow.Dial,
        ElementsToShow.Digital
    ];

    [ObservableProperty]
    private LanguageItem? _selectedLanguage;

    [ObservableProperty]
    private MonitorItem? _selectedMonitor;

    [ObservableProperty]
    private Bitmap? _qrCodeImage;

    [ObservableProperty]
    private string _webClockUrl = string.Empty;

    [ObservableProperty]
    private string _firewallStatusText = string.Empty;

    [ObservableProperty]
    private string _firewallPlatformName = string.Empty;

    [ObservableProperty]
    private bool _isFirewallConfigured;

    [ObservableProperty]
    private bool _canConfigureFirewall;

    [ObservableProperty]
    private bool _isMacOS;

    [ObservableProperty]
    private LogEventLevelItem? _selectedLogLevel;

    public ObservableCollection<MonitorItem> AvailableMonitors { get; } = new();
    public OperatingMode[] OperatingModes { get; } = [OperatingMode.Manual, OperatingMode.Automatic, OperatingMode.ScheduleFile];
    public MidWeekOrWeekend[] MeetingTypes { get; } = [MidWeekOrWeekend.MidWeek, MidWeekOrWeekend.Weekend];
    public ClockMode[] ClockModes { get; } = [ClockMode.Digital, ClockMode.Analogue, ClockMode.AnalogueAndDigital];
    public ClockHourFormatItem[] ClockHourFormats { get; } =
    [
        new ClockHourFormatItem(Strings.CLOCK_FORMAT_12, ClockHourFormat.Format12, "3:00"),
        new ClockHourFormatItem(Strings.CLOCK_FORMAT_12Z, ClockHourFormat.Format12LeadingZero, "03:00"),
        new ClockHourFormatItem(Strings.CLOCK_FORMAT_24, ClockHourFormat.Format24, "15:00"),
        new ClockHourFormatItem(Strings.CLOCK_FORMAT_24Z, ClockHourFormat.Format24LeadingZero, "06:00"),
        new ClockHourFormatItem(Strings.CLOCK_FORMAT_12AMPM, ClockHourFormat.Format12AMPM, "3:00 PM"),
        new ClockHourFormatItem(Strings.CLOCK_FORMAT_12ZAMPM, ClockHourFormat.Format12LeadingZeroAMPM, "03:00 PM")
    ];
    public AdaptiveMode[] AdaptiveModes { get; } = [AdaptiveMode.None, AdaptiveMode.OneWay, AdaptiveMode.TwoWay];
    public ObservableCollection<LanguageItem> AvailableLanguages { get; } = new();
    public LogEventLevelItem[] LogLevels { get; } =
    [
        new LogEventLevelItem(Strings.LOG_LEVEL_VERBOSE ?? "Verbose", LogEventLevel.Verbose),
        new LogEventLevelItem(Strings.LOG_LEVEL_DEBUG ?? "Debug", LogEventLevel.Debug),
        new LogEventLevelItem(Strings.LOG_LEVEL_INFORMATION ?? "Information", LogEventLevel.Information),
        new LogEventLevelItem(Strings.LOG_LEVEL_WARNING ?? "Warning", LogEventLevel.Warning),
        new LogEventLevelItem(Strings.LOG_LEVEL_ERROR ?? "Error", LogEventLevel.Error),
        new LogEventLevelItem(Strings.LOG_LEVEL_FATAL ?? "Fatal", LogEventLevel.Fatal)
    ];

    public bool IsAutomaticMode => OperatingMode == OperatingMode.Automatic;
    public bool IsFileBasedMode => OperatingMode == OperatingMode.ScheduleFile;

    [ObservableProperty]
    private ObservableCollection<string> _availableScheduleFiles = [];

    [ObservableProperty]
    private string? _selectedScheduleFile;

    public string FirewallStatusDisplay => IsFirewallConfigured
        ? _localizationService?.GetString("STATUS_CONFIGURED") ?? "Configured"
        : _localizationService?.GetString("STATUS_NOT_CONFIGURED") ?? "Not Configured";

    public IBrush FirewallStatusColor => IsFirewallConfigured
        ? new SolidColorBrush(Color.Parse("#4CAF50"))  // Green
        : new SolidColorBrush(Color.Parse("#FF9800")); // Orange

    public bool IsAnalogueSliderEnabled => FullScreenClockMode != ClockMode.Digital && HorizontalClockLayout;

    [ObservableProperty]
    private bool _horizontalClockLayout;

    partial void OnFullScreenClockModeChanged(ClockMode value)
    {
        OnPropertyChanged(nameof(IsAnalogueSliderEnabled));
        var timerOutputVm = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default
            .GetService<TimerOutputViewModel>();
        timerOutputVm?.RefreshClockMode(value);
    }

    partial void OnHorizontalClockLayoutChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAnalogueSliderEnabled));
    }

    partial void OnOperatingModeChanged(OperatingMode value)
    {
        OnPropertyChanged(nameof(IsAutomaticMode));
        OnPropertyChanged(nameof(IsFileBasedMode));
        if (IsFileBasedMode)
        {
            LoadScheduleFiles();
        }
    }

    private void LoadScheduleFiles()
    {
        var files = OnlyT.Avalonia.Utils.ScheduleExporter.GetAvailableTemplates();
        AvailableScheduleFiles.Clear();
        foreach (var f in files)
        {
            AvailableScheduleFiles.Add(System.IO.Path.GetFileName(f));
        }
        var current = _optionsService.GetOptions().SelectedScheduleFile;
        if (!string.IsNullOrEmpty(current) && AvailableScheduleFiles.Contains(current))
        {
            SelectedScheduleFile = current;
        }
        else if (AvailableScheduleFiles.Count > 0)
        {
            SelectedScheduleFile = AvailableScheduleFiles[0];
        }
    }

    partial void OnIsFirewallConfiguredChanged(bool value)
    {
        OnPropertyChanged(nameof(FirewallStatusDisplay));
        OnPropertyChanged(nameof(FirewallStatusColor));
    }

    public SettingsViewModel(IOptionsService optionsService, IMonitorService monitorService, ILocalizationService? localizationService = null, OperatorPageViewModel? operatorPageViewModel = null, IBellService? bellService = null, IFirewallService? firewallService = null, ILogLevelSwitchService? logLevelSwitchService = null)
    {
        _optionsService = optionsService;
        _monitorService = monitorService;
        _localizationService = localizationService;
        _operatorPageViewModel = operatorPageViewModel;
        _bellService = bellService;
        _firewallService = firewallService;
        _logLevelSwitchService = logLevelSwitchService;

        LoadSettings();
        LoadMonitors();
        LoadLanguages();
        GenerateQRCode();
        LoadFirewallStatus();
    }

    [RelayCommand]
    private void Save()
    {
        var options = _optionsService.GetOptions();
        options.FullScreenMode = FullScreenMode;
        options.AlwaysOnTop = AlwaysOnTop;
        options.IsBellEnabled = IsBellEnabled;
        options.AutoBell = AutoBell;
        options.IsCircuitVisit = IsCircuitVisit;
        options.OperatingMode = OperatingMode;
        options.SelectedScheduleFile = SelectedScheduleFile ?? string.Empty;
        options.MidWeekOrWeekend = MidWeekOrWeekend;
        options.ShowTimeOfDayUnderTimer = ShowTimeOfDayUnderTimer;
        options.ShowDigitalSeconds = ShowDigitalSeconds;
        options.ShowDurationSector = ShowDurationSector;
        options.IsApiThrottled = IsApiThrottled;
        options.ApiAccessCode = ApiAccessCode;
        options.FlashTimerWhenOvertime = FlashTimerWhenOvertime;
        options.BellOnOvertime = BellOnOvertime;
        options.ShowMousePointerInTimerDisplay = ShowMousePointerInTimerDisplay;
        options.AnalogueClockWidthPercent = AnalogueClockWidthPercent;
        options.FullScreenClockMode = FullScreenClockMode;
        options.BellVolumePercent = BellVolumePercent;
        options.ClockHourFormat = SelectedClockHourFormat?.Format ?? ClockHourFormat.Format24LeadingZero;
        options.IsDarkMode = IsDarkMode;
        options.MidWeekAdaptiveMode = MidWeekAdaptiveMode;
        options.WeekendAdaptiveMode = WeekendAdaptiveMode;
        options.TimerReminder = TimerReminder;
        options.ShowBackgroundOnTimer = ShowBackgroundOnTimer;
        options.IsCountdownWindowTransparent = IsCountdownWindowTransparent;
        options.PersistStudentTime = PersistStudentTime;
        options.ShrinkOnMinimise = ShrinkOnMinimise;
        options.IsApiEnabled = IsApiEnabled;
        options.ShowCircuitVisitToggle = ShowCircuitVisitToggle;
        options.AllowCountUpToggle = AllowCountUpToggle;
        options.CountUp = CountUp;
        options.WeekendIncludesFriday = WeekendIncludesFriday;
        options.OverrunNotifications = OverrunNotifications;
        options.GenerateTimingReports = GenerateTimingReports;
        options.ShowExportScheduleButton = ShowExportScheduleButton;
        options.HorizontalClockLayout = HorizontalClockLayout;
        options.HttpServerPort = HttpServerPort;
        options.MeetingStartTimesText = MeetingStartTimesText;
        options.CountdownDurationMins = CountdownDurationMins;
        options.CountdownElementsToShow = CountdownElementsToShow;
        options.Culture = SelectedLanguage?.CultureCode ?? "en-GB";
        options.MonitorId = SelectedMonitor?.MonitorId;
        options.LogEventLevel = SelectedLogLevel?.Level.ToString() ?? "Information";
        options.ClassicMode = ClassicMode;

        _optionsService.SaveOptions(options);

        // Apply log level change immediately
        if (SelectedLogLevel != null)
        {
            _logLevelSwitchService?.SetMinimumLevel(SelectedLogLevel.Level);
        }

        // Apply language change (requires restart to take full effect)
        if (SelectedLanguage != null)
        {
            _localizationService?.SetCulture(SelectedLanguage.CultureCode);
        }

        // Refresh talks in OperatorPageViewModel if available
        _operatorPageViewModel?.RefreshTalks();

        // Dismiss the window on successful save so the user doesn't have to
        // click Close separately. Without this, long save paths felt 'stuck'.
        RequestClose?.Invoke(this, System.EventArgs.Empty);
    }

    [RelayCommand]
    private void TestBell()
    {
        // Play the bell at current volume setting
        _bellService?.Play(BellVolumePercent);
    }

    [RelayCommand]
    private void ConfigureFirewall()
    {
        if (_firewallService == null)
            return;

        var options = _optionsService.GetOptions();
        var port = options.HttpServerPort > 0 ? options.HttpServerPort : 8096;

        var result = _firewallService.ConfigureFirewall(port);

        // Refresh status after configuration attempt
        LoadFirewallStatus();
    }

    [RelayCommand]
    private void RequestNetworkAccess()
    {
        _firewallService?.RequestLocalNetworkAccess();

        // Refresh status after request
        LoadFirewallStatus();
    }

    [RelayCommand]
    private void RefreshFirewallStatus()
    {
        LoadFirewallStatus();
    }

    private void LoadSettings()
    {
        var options = _optionsService.GetOptions();
        FullScreenMode = options.FullScreenMode;
        AlwaysOnTop = options.AlwaysOnTop;
        IsBellEnabled = options.IsBellEnabled;
        AutoBell = options.AutoBell;
        IsCircuitVisit = options.IsCircuitVisit;
        OperatingMode = options.OperatingMode;
        if (OperatingMode == OperatingMode.ScheduleFile)
        {
            LoadScheduleFiles();
        }
        ShowTimeOfDayUnderTimer = options.ShowTimeOfDayUnderTimer;
        ShowDigitalSeconds = options.ShowDigitalSeconds;
        ShowDurationSector = options.ShowDurationSector;
        IsApiThrottled = options.IsApiThrottled;
        ApiAccessCode = options.ApiAccessCode;
        FlashTimerWhenOvertime = options.FlashTimerWhenOvertime;
        BellOnOvertime = options.BellOnOvertime;
        ShowMousePointerInTimerDisplay = options.ShowMousePointerInTimerDisplay;
        AnalogueClockWidthPercent = options.AnalogueClockWidthPercent;
        FullScreenClockMode = options.FullScreenClockMode;
        BellVolumePercent = options.BellVolumePercent;
        SelectedClockHourFormat = ClockHourFormats.FirstOrDefault(f => f.Format == options.ClockHourFormat)
                                  ?? ClockHourFormats[3]; // Default to 24hr with leading zero
        IsDarkMode = options.IsDarkMode;
        MidWeekAdaptiveMode = options.MidWeekAdaptiveMode;
        WeekendAdaptiveMode = options.WeekendAdaptiveMode;
        TimerReminder = options.TimerReminder;
        ShowBackgroundOnTimer = options.ShowBackgroundOnTimer;
        IsCountdownWindowTransparent = options.IsCountdownWindowTransparent;
        PersistStudentTime = options.PersistStudentTime;
        ShrinkOnMinimise = options.ShrinkOnMinimise;
        IsApiEnabled = options.IsApiEnabled;
        ShowCircuitVisitToggle = options.ShowCircuitVisitToggle;
        AllowCountUpToggle = options.AllowCountUpToggle;
        CountUp = options.CountUp;
        WeekendIncludesFriday = options.WeekendIncludesFriday;
        OverrunNotifications = options.OverrunNotifications;
        GenerateTimingReports = options.GenerateTimingReports;
        ShowExportScheduleButton = options.ShowExportScheduleButton;
        HorizontalClockLayout = options.HorizontalClockLayout;
        HttpServerPort = options.HttpServerPort;
        MeetingStartTimesText = options.MeetingStartTimesText;
        CountdownDurationMins = options.CountdownDurationMins;
        CountdownElementsToShow = options.CountdownElementsToShow;

        // Load log level
        if (Enum.TryParse<LogEventLevel>(options.LogEventLevel, out var logLevel))
        {
            SelectedLogLevel = LogLevels.FirstOrDefault(l => l.Level == logLevel)
                               ?? LogLevels.FirstOrDefault(l => l.Level == LogEventLevel.Information);
        }
        else
        {
            SelectedLogLevel = LogLevels.FirstOrDefault(l => l.Level == LogEventLevel.Information);
        }

        // Auto-detect meeting type based on day of week
        MidWeekOrWeekend = AutoDetectMeetingType(options.MidWeekOrWeekend);

        ClassicMode = options.ClassicMode;
    }

    private MidWeekOrWeekend AutoDetectMeetingType(MidWeekOrWeekend savedValue)
    {
        var today = System.DateTime.Now.DayOfWeek;

        if (today == System.DayOfWeek.Saturday || today == System.DayOfWeek.Sunday)
        {
            return MidWeekOrWeekend.Weekend;
        }

        if (_optionsService.WeekendIncludesFriday && today == System.DayOfWeek.Friday)
        {
            return MidWeekOrWeekend.Weekend;
        }

        return MidWeekOrWeekend.MidWeek;
    }

    private void LoadLanguages()
    {
        if (_localizationService == null)
            return;

        AvailableLanguages.Clear();
        foreach (var language in _localizationService.GetSupportedLanguages())
        {
            AvailableLanguages.Add(language);
        }

        // Select current language
        var options = _optionsService.GetOptions();
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.CultureCode == options.Culture)
                          ?? AvailableLanguages.FirstOrDefault(l => l.CultureCode == "en-GB")
                          ?? AvailableLanguages.FirstOrDefault();
    }

    private void LoadMonitors()
    {
        var monitors = _monitorService.GetMonitors();
        AvailableMonitors.Clear();

        foreach (var monitor in monitors)
        {
            var monitorItem = new MonitorItem
            {
                MonitorId = monitor.MonitorId,
                MonitorName = monitor.MonitorName,
                FriendlyName = monitor.FriendlyName,
                IsPrimary = monitor.IsPrimary
            };
            AvailableMonitors.Add(monitorItem);
        }

        // Select saved monitor or primary
        var options = _optionsService.GetOptions();
        SelectedMonitor = AvailableMonitors.FirstOrDefault(m => m.MonitorId == options.MonitorId)
                          ?? AvailableMonitors.FirstOrDefault(m => m.IsPrimary)
                          ?? AvailableMonitors.FirstOrDefault();
    }

    private void GenerateQRCode()
    {
        var options = _optionsService.GetOptions();
        var port = options.HttpServerPort > 0 ? options.HttpServerPort : 8096;

        WebClockUrl = QRCodeGeneration.GetWebClockUrl(port);
        QrCodeImage = QRCodeGeneration.CreateQRCode(WebClockUrl);
    }

    private void LoadFirewallStatus()
    {
        IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        if (_firewallService == null)
        {
            FirewallStatusText = "Firewall service unavailable";
            FirewallPlatformName = "Unknown";
            IsFirewallConfigured = false;
            CanConfigureFirewall = false;
            return;
        }

        var options = _optionsService.GetOptions();
        var port = options.HttpServerPort > 0 ? options.HttpServerPort : 8096;

        var status = _firewallService.GetFirewallStatus(port);

        FirewallStatusText = status.StatusMessage;
        FirewallPlatformName = status.PlatformName;
        IsFirewallConfigured = status.IsConfigured;
        CanConfigureFirewall = status.CanConfigure;
    }

    /// <summary>
    /// Called when IsDarkMode property changes - applies theme immediately
    /// </summary>
    partial void OnIsDarkModeChanged(bool value)
    {
        if (global::Avalonia.Application.Current is App app)
        {
            app.SetTheme(value);
        }
    }
}

/// <summary>
/// Monitor item for display in UI
/// </summary>
public class MonitorItem
{
    public string MonitorId { get; set; } = string.Empty;
    public string MonitorName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    public string DisplayName => IsPrimary ? $"{FriendlyName} (Primary)" : FriendlyName;
}

/// <summary>
/// Log level item for display in settings
/// </summary>
public class LogEventLevelItem
{
    public string Name { get; }
    public LogEventLevel Level { get; }
    public string? Description { get; }

    public LogEventLevelItem(string name, LogEventLevel level, string? description = null)
    {
        Name = name;
        Level = level;
        Description = description;
    }

    public string DisplayName => string.IsNullOrEmpty(Description) ? Name : $"{Name} - {Description}";
}
