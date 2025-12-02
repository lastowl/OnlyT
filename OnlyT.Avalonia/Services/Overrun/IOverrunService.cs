using System;

namespace OnlyT.Avalonia.Services.Overrun;

/// <summary>
/// Service interface for handling overrun/underrun notifications
/// </summary>
public interface IOverrunService
{
    /// <summary>
    /// Notify the user of bad timing (overrun or underrun)
    /// </summary>
    void NotifyOfBadTiming(TimeSpan variance);

    /// <summary>
    /// Shutdown and cleanup
    /// </summary>
    void Shutdown();
}
