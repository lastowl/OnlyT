using System;
using OnlyT.Avalonia.MeetingTalkTimesFeed;

namespace OnlyT.Avalonia.Services;

/// <summary>
/// Service for managing meeting times and schedules
/// </summary>
public interface IMeetingTimesService
{
    /// <summary>
    /// Gets the meeting data for today
    /// </summary>
    Meeting? GetTodaysMeeting();

    /// <summary>
    /// Gets the feed URI
    /// </summary>
    string FeedUri { get; set; }

    /// <summary>
    /// Event raised when meeting data changes
    /// </summary>
    event EventHandler? MeetingDataChanged;
}
