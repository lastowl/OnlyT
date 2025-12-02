using Serilog.Core;
using Serilog.Events;

namespace OnlyT.Avalonia.Services.LogLevelSwitch;

/// <summary>
/// Service for switching log levels at runtime using Serilog's LoggingLevelSwitch
/// </summary>
public class LogLevelSwitchService : ILogLevelSwitchService
{
    /// <summary>
    /// Static level switch that can be used during logger configuration
    /// </summary>
    public static readonly LoggingLevelSwitch LevelSwitch = new()
    {
        MinimumLevel = LogEventLevel.Information
    };

    public void SetMinimumLevel(LogEventLevel level)
    {
        LevelSwitch.MinimumLevel = level;
    }

    public LogEventLevel GetMinimumLevel()
    {
        return LevelSwitch.MinimumLevel;
    }
}
