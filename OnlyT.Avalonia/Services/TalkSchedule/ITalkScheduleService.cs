namespace OnlyT.Avalonia.Services.TalkSchedule;

using System;
using System.Collections.Generic;
using Models;

/// <summary>
/// Service for managing the talk schedule and timer items for meetings.
/// </summary>
/// <remarks>
/// This service provides access to the list of scheduled talks, allows modification
/// of talk durations, and tracks which talks have been completed.
/// </remarks>
public interface ITalkScheduleService
{
    /// <summary>
    /// Gets all scheduled talk items for the current meeting.
    /// </summary>
    /// <returns>An enumerable collection of talk schedule items.</returns>
    IEnumerable<TalkScheduleItem> GetTalkScheduleItems();

    /// <summary>
    /// Gets a specific talk schedule item by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the talk.</param>
    /// <returns>The talk schedule item, or null if not found.</returns>
    TalkScheduleItem? GetTalkScheduleItem(int id);

    /// <summary>
    /// Gets the ID of the next talk after the specified talk.
    /// </summary>
    /// <param name="currentTalkId">The ID of the current talk.</param>
    /// <returns>The ID of the next talk, or -1 if there is no next talk.</returns>
    int GetNext(int currentTalkId);

    /// <summary>
    /// Resets all talk schedule items to their initial state.
    /// </summary>
    /// <remarks>
    /// This clears any completed times and modified durations,
    /// returning all talks to their original scheduled state.
    /// </remarks>
    void Reset();

    /// <summary>
    /// Indicates whether the automatic feed was successfully retrieved for midweek meeting.
    /// </summary>
    /// <returns>True if the automatic feed was successfully loaded; otherwise, false.</returns>
    bool SuccessGettingAutoFeedForMidWeekMtg();

    /// <summary>
    /// Sets a modified duration for a specific talk.
    /// </summary>
    /// <param name="talkId">The ID of the talk to modify.</param>
    /// <param name="modifiedDuration">The new duration, or null to reset to original duration.</param>
    /// <remarks>
    /// This allows operators to adjust talk durations on-the-fly during a meeting,
    /// for example when the chairman needs to extend or shorten a talk.
    /// </remarks>
    void SetModifiedDuration(int talkId, TimeSpan? modifiedDuration);
}
