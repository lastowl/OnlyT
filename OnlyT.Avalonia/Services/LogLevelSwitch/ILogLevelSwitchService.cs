using Serilog.Events;

namespace OnlyT.Avalonia.Services.LogLevelSwitch;

/// <summary>
/// Service for changing the log level at runtime
/// </summary>
public interface ILogLevelSwitchService
{
    /// <summary>
    /// Set the minimum log level
    /// </summary>
    void SetMinimumLevel(LogEventLevel level);

    /// <summary>
    /// Get the current minimum log level
    /// </summary>
    LogEventLevel GetMinimumLevel();
}
