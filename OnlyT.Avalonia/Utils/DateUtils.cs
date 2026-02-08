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

    public static DateTime GetNearestQuarterOfAnHour(DateTime value)
    {
        var mins = value.Minute;
        int minsAdjust;

        var minsLong = mins % 15;
        if (minsLong <= 10)
        {
            minsAdjust = -minsLong;
        }
        else
        {
            minsAdjust = 15 - minsLong;
        }

        var newValue = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
        return newValue.AddMinutes(minsAdjust);
    }
}
