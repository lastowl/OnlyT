using System;
using System.Collections.Generic;
using System.Linq;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Common.Services.DateTime;

namespace OnlyT.Avalonia.Services.CountdownTimer;

public sealed class CountdownTimerTriggerService
{
    private readonly object _locker = new();
    private readonly IOptionsService _optionsService;
    private readonly IDateTimeService _dateTimeService;
    private List<CountdownTriggerPeriod>? _triggerPeriods;

    public CountdownTimerTriggerService(
        IOptionsService optionsService,
        IDateTimeService dateTimeService)
    {
        _optionsService = optionsService;
        _dateTimeService = dateTimeService;

        UpdateTriggerPeriods();
    }

    public void UpdateTriggerPeriods()
    {
        var times = _optionsService.MeetingStartTimes.Times;

        lock (_locker)
        {
            _triggerPeriods = null;
            CalculateTriggerPeriods(times);
        }
    }

    public bool IsInCountdownPeriod(out int secondsRemaining)
    {
        lock (_locker)
        {
            if (_triggerPeriods != null)
            {
                var now = _dateTimeService.Now();

                var trigger = _triggerPeriods.FirstOrDefault(x => x.Start <= now && x.End > now);
                if (trigger != null)
                {
                    secondsRemaining = (int)(trigger.End - now).TotalSeconds;
                    return secondsRemaining >= 10;
                }
            }
        }

        secondsRemaining = 0;
        return false;
    }

    /// <summary>
    /// Gets seconds until the next meeting start time, looking up to 7 days ahead
    /// </summary>
    public int? GetSecondsUntilNextMeeting()
    {
        var times = _optionsService.MeetingStartTimes.Times;
        if (times.Count == 0)
            return null;

        var now = _dateTimeService.Now();
        DateTime? closest = null;

        // Check today and the next 7 days
        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var date = now.Date.AddDays(dayOffset);

            foreach (var time in times)
            {
                if (time.DayOfWeek != null && time.DayOfWeek.Value != date.DayOfWeek)
                    continue;

                var meetingDateTime = date.Add(time.StartTime);

                if (meetingDateTime > now)
                {
                    if (closest == null || meetingDateTime < closest)
                    {
                        closest = meetingDateTime;
                    }
                }
            }

            // If we found one today or on this day, no need to search further
            if (closest != null && dayOffset > 0)
                break;
        }

        if (closest != null)
        {
            return (int)(closest.Value - now).TotalSeconds;
        }

        return null;
    }

    private void CalculateTriggerPeriods(List<MeetingStartTime> meetingStartTimes)
    {
        var triggerPeriods = new List<CountdownTriggerPeriod>();

        var today = _dateTimeService.Now().Date;
        var countdownDurationMins = _optionsService.CountdownDurationMins;

        foreach (var time in meetingStartTimes)
        {
            if (time.DayOfWeek == null || time.DayOfWeek.Value == today.DayOfWeek)
            {
                triggerPeriods.Add(new CountdownTriggerPeriod
                {
                    Start = today.Add(
                        TimeSpan.FromMinutes(
                            (time.StartTime.Hours * 60) + time.StartTime.Minutes - countdownDurationMins)),
                    End = today.Add(new TimeSpan(time.StartTime.Hours, time.StartTime.Minutes, 0))
                });
            }
        }

        _triggerPeriods = triggerPeriods;
    }
}
