using System;

namespace OnlyT.Avalonia.Services;

/// <summary>
/// Service for managing timer functionality
/// </summary>
public interface ITimerService
{
    /// <summary>
    /// Current timer state
    /// </summary>
    TimerState State { get; }

    /// <summary>
    /// Remaining time
    /// </summary>
    TimeSpan RemainingTime { get; }

    /// <summary>
    /// Target duration
    /// </summary>
    TimeSpan TargetDuration { get; }

    /// <summary>
    /// Whether timer is in overtime (negative time)
    /// </summary>
    bool IsOvertime { get; }

    /// <summary>
    /// Event raised when timer starts
    /// </summary>
    event EventHandler? TimerStarted;

    /// <summary>
    /// Event raised when timer stops
    /// </summary>
    event EventHandler? TimerStopped;

    /// <summary>
    /// Event raised when time changes (every second)
    /// </summary>
    event EventHandler<TimeSpan>? TimeChanged;

    /// <summary>
    /// Event raised when timer reaches zero
    /// </summary>
    event EventHandler? TimerEnded;

    /// <summary>
    /// Event raised when timer is paused
    /// </summary>
    event EventHandler? TimerPaused;

    /// <summary>
    /// Event raised when timer is resumed
    /// </summary>
    event EventHandler? TimerResumed;

    /// <summary>
    /// Start the timer with specified duration
    /// </summary>
    void Start(TimeSpan duration);

    /// <summary>
    /// Stop the timer
    /// </summary>
    void Stop();

    /// <summary>
    /// Pause the timer
    /// </summary>
    void Pause();

    /// <summary>
    /// Resume the timer
    /// </summary>
    void Resume();

    /// <summary>
    /// Reset the timer to target duration
    /// </summary>
    void Reset();
}

/// <summary>
/// Timer states
/// </summary>
public enum TimerState
{
    Stopped,
    Running,
    Paused
}
