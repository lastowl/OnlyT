using System;

namespace OnlyT.Avalonia.Services.Reminders;

/// <summary>
/// Service for managing timer reminders to operators
/// </summary>
public interface IReminderService
{
    /// <summary>
    /// Event raised when a reminder should be shown
    /// </summary>
    event EventHandler<ReminderEventArgs>? ReminderTriggered;

    /// <summary>
    /// Notify the service that a timer has started
    /// </summary>
    void OnTimerStarted(int talkId);

    /// <summary>
    /// Notify the service that a timer has stopped
    /// </summary>
    void OnTimerStopped(int talkId);

    /// <summary>
    /// Dismiss the current reminder
    /// </summary>
    void DismissReminder();

    /// <summary>
    /// Shutdown the reminder service
    /// </summary>
    void Shutdown();
}

/// <summary>
/// Event args for reminder events
/// </summary>
public class ReminderEventArgs : EventArgs
{
    /// <summary>
    /// The reminder message to display
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Whether the reminder is showing or being dismissed
    /// </summary>
    public bool IsShowing { get; set; }
}
