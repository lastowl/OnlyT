using System;
using System.Globalization;
using System.Linq;

namespace OnlyT.Avalonia.Services.Options;

/// <summary>
/// Represents a meeting start time for a specific day of the week
/// </summary>
public class MeetingStartTime
{
    /// <summary>
    /// Day of the week (null means any day)
    /// </summary>
    public DayOfWeek? DayOfWeek { get; set; }

    /// <summary>
    /// The start time
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// Parse a meeting start time from text like "Mon 19:00" or "7pm"
    /// </summary>
    public static MeetingStartTime? FromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var dayOfWeek = FindDayOfWeek(text);
        var cleanText = text;

        if (dayOfWeek.HasValue)
        {
            // Remove the day name from the text
            cleanText = RemoveDayName(text, dayOfWeek.Value);
        }

        var time = GetTimeOfDay(cleanText);
        if (time != null)
        {
            return new MeetingStartTime
            {
                DayOfWeek = dayOfWeek,
                StartTime = time.Value
            };
        }

        return null;
    }

    /// <summary>
    /// Sanitize the start time (ensure it's valid)
    /// </summary>
    public void Sanitize()
    {
        if (StartTime.TotalHours > 24)
        {
            StartTime = TimeSpan.FromHours(24);
        }
    }

    /// <summary>
    /// Convert to text representation
    /// </summary>
    public string AsText()
    {
        var timeString = StartTime.ToString(@"hh\:mm");

        if (DayOfWeek != null)
        {
            var dayName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(DayOfWeek.Value);
            return $"{dayName} {timeString}";
        }

        return timeString;
    }

    private static DayOfWeek? FindDayOfWeek(string text)
    {
        foreach (DayOfWeek dow in Enum.GetValues(typeof(DayOfWeek)))
        {
            // Check full name
            var fullName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(dow);
            if (text.IndexOf(fullName, StringComparison.OrdinalIgnoreCase) >= 0)
                return dow;

            // Check abbreviated name
            var abbrevName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(dow);
            if (text.IndexOf(abbrevName, StringComparison.OrdinalIgnoreCase) >= 0)
                return dow;

            // Check shortest name
            var shortName = CultureInfo.CurrentCulture.DateTimeFormat.GetShortestDayName(dow);
            if (text.IndexOf(shortName, StringComparison.OrdinalIgnoreCase) >= 0)
                return dow;
        }

        return null;
    }

    private static string RemoveDayName(string text, DayOfWeek dow)
    {
        var fullName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(dow);
        var abbrevName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(dow);
        var shortName = CultureInfo.CurrentCulture.DateTimeFormat.GetShortestDayName(dow);

        text = text.Replace(fullName, "", StringComparison.OrdinalIgnoreCase);
        text = text.Replace(abbrevName, "", StringComparison.OrdinalIgnoreCase);
        text = text.Replace(shortName, "", StringComparison.OrdinalIgnoreCase);

        return text.Trim();
    }

    private static TimeSpan? GetTimeOfDay(string text)
    {
        var hasPm = HasPm(text);

        var hour = -1;
        var mins = -1;

        var digits = text.Where(char.IsDigit).ToArray();
        if (digits.Length > 0 && digits.Length < 5)
        {
            switch (digits.Length)
            {
                case 1:
                    hour = int.Parse(digits[0].ToString());
                    mins = 0;
                    break;

                case 2:
                    hour = int.Parse($"{digits[0]}{digits[1]}");
                    mins = 0;
                    break;

                case 3:
                    hour = int.Parse(digits[0].ToString());
                    mins = int.Parse($"{digits[1]}{digits[2]}");
                    break;

                case 4:
                    hour = int.Parse($"{digits[0]}{digits[1]}");
                    mins = int.Parse($"{digits[2]}{digits[3]}");
                    break;
            }
        }

        if (hour < 12 && hasPm)
        {
            hour += 12;
        }

        if (hour > -1 && mins > -1 && mins < 60 && hour <= 24)
        {
            return new TimeSpan(hour, mins, 0);
        }

        return null;
    }

    private static bool HasPm(string text)
    {
        var trimmedText = text.Trim().ToLowerInvariant();
        return trimmedText.EndsWith("pm") || trimmedText.EndsWith("p.m.") || trimmedText.EndsWith("p");
    }
}
