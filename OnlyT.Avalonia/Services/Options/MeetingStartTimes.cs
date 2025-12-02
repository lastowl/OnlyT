using System;
using System.Collections.Generic;

namespace OnlyT.Avalonia.Services.Options;

/// <summary>
/// Collection of meeting start times
/// </summary>
public class MeetingStartTimes
{
    /// <summary>
    /// List of configured start times
    /// </summary>
    public List<MeetingStartTime> Times { get; } = [];

    /// <summary>
    /// Sanitize all start times
    /// </summary>
    public void Sanitize()
    {
        foreach (var startTime in Times)
        {
            startTime.Sanitize();
        }
    }

    /// <summary>
    /// Parse start times from multi-line text
    /// </summary>
    public void FromText(string value)
    {
        Times.Clear();

        if (!string.IsNullOrWhiteSpace(value))
        {
            var lines = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var startTime = MeetingStartTime.FromText(line.Trim());
                if (startTime != null)
                {
                    Times.Add(startTime);
                }
            }
        }
    }

    /// <summary>
    /// Convert to multi-line text
    /// </summary>
    public string AsText()
    {
        var result = new List<string>();

        foreach (var startTime in Times)
        {
            var text = startTime.AsText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add(text);
            }
        }

        return string.Join(Environment.NewLine, result);
    }

    /// <summary>
    /// Get the meeting start time for a specific day
    /// </summary>
    public TimeSpan? GetStartTimeForDay(DayOfWeek dayOfWeek)
    {
        // First look for a specific day match
        foreach (var startTime in Times)
        {
            if (startTime.DayOfWeek == dayOfWeek)
            {
                return startTime.StartTime;
            }
        }

        // Then look for a generic (any day) entry
        foreach (var startTime in Times)
        {
            if (startTime.DayOfWeek == null)
            {
                return startTime.StartTime;
            }
        }

        return null;
    }
}
