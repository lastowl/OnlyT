using System;

namespace OnlyT.Avalonia.Services.Timer;

/// <summary>
/// Service for adaptive timer calculations that adjust talk durations
/// based on meeting progress to keep meetings on schedule
/// </summary>
public interface IAdaptiveTimerService
{
    /// <summary>
    /// Calculate the adapted duration for a specific talk based on meeting progress
    /// </summary>
    /// <param name="talkId">The ID of the talk to calculate</param>
    /// <returns>Adapted duration, or null if no adaptation is needed</returns>
    TimeSpan? CalculateAdaptedDuration(int talkId);

    /// <summary>
    /// Calculate the expected meeting overrun based on current progress
    /// </summary>
    /// <param name="talkId">The current talk ID</param>
    /// <returns>Expected overrun (positive) or underrun (negative), or null if not significant</returns>
    TimeSpan? CalculateMeetingOverrun(int talkId);

    /// <summary>
    /// Set the meeting start time (used for calculating overall meeting progress)
    /// </summary>
    /// <param name="startTime">The meeting start time</param>
    void SetMeetingStartTime(DateTime startTime);

    /// <summary>
    /// Get the inferred or set meeting start time
    /// </summary>
    DateTime? GetMeetingStartTime();
}
