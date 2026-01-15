using System;

namespace OnlyT.Avalonia.Utils;

/// <summary>
/// Date utilities
/// </summary>
public static class DateUtils
{
    public static DateTime GetMondayOfThisWeek()
    {
        var today = DateTime.Now.Date;
        var daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return today.AddDays(-daysSinceMonday);
    }
}
