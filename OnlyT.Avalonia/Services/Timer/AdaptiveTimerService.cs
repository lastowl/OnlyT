using System;
using System.Linq;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Avalonia.Services.TalkSchedule;
using OnlyT.Common.Services.DateTime;
using Serilog;

namespace OnlyT.Avalonia.Services.Timer;

/// <summary>
/// Service that calculates adaptive talk durations to keep meetings on schedule.
/// Can proportionally adjust remaining talks based on current progress.
/// </summary>
public class AdaptiveTimerService : IAdaptiveTimerService
{
    // Only adapt if the deviation is within these bounds
    private const int LargestDeviationMinutes = 15;
    private const int SmallestDeviationSecs = 15;

    private readonly ITalkScheduleService _scheduleService;
    private readonly IOptionsService _optionsService;
    private readonly IDateTimeService _dateTimeService;

    private DateTime? _meetingStartTime;

    public AdaptiveTimerService(
        ITalkScheduleService scheduleService,
        IOptionsService optionsService,
        IDateTimeService dateTimeService)
    {
        _scheduleService = scheduleService;
        _optionsService = optionsService;
        _dateTimeService = dateTimeService;
    }

    public void SetMeetingStartTime(DateTime startTime)
    {
        _meetingStartTime = startTime;
        Log.Information("Meeting start time set to {StartTime}", startTime);
    }

    public DateTime? GetMeetingStartTime()
    {
        return _meetingStartTime;
    }

    public TimeSpan? CalculateAdaptedDuration(int talkId)
    {
        try
        {
            var talks = _scheduleService.GetTalkScheduleItems().ToList();
            var talk = talks.FirstOrDefault(t => t.Id == talkId);

            if (talk == null)
            {
                return null;
            }

            // Check if adaptive mode is enabled
            var adaptiveMode = GetAdaptiveMode();
            if (adaptiveMode == AdaptiveMode.None)
            {
                return null;
            }

            // Check if this talk allows adaptive timing
            if (!talk.AllowAdaptive)
            {
                return null;
            }

            // Ensure we have a meeting start time (infer if not set)
            EnsureMeetingStartTime(talks);

            if (!_meetingStartTime.HasValue)
            {
                return null;
            }

            // Calculate expected meeting end time based on planned durations
            var plannedMeetingEnd = CalculatePlannedMeetingEnd(talks);

            // Calculate time remaining until planned end
            var now = _dateTimeService.Now();
            var totalTimeRemaining = plannedMeetingEnd - now;

            // Calculate total time required for remaining program
            var remainingTalks = talks.SkipWhile(t => t.Id != talkId).ToList();
            var remainingProgramTime = remainingTalks
                .Sum(t => t.ActualDuration.TotalSeconds);

            // Add changeover times (about 30 seconds per talk transition)
            remainingProgramTime += (remainingTalks.Count - 1) * 30;

            // Calculate deviation (positive = running ahead, negative = running behind)
            var deviation = totalTimeRemaining.TotalSeconds - remainingProgramTime;

            // Only adapt if deviation is significant
            if (Math.Abs(deviation) < SmallestDeviationSecs)
            {
                return null; // Deviation too small to matter
            }

            if (Math.Abs(deviation) > LargestDeviationMinutes * 60)
            {
                return null; // Deviation too large - something else is wrong
            }

            // In OneWay mode, only shorten talks (deviation must be negative)
            if (adaptiveMode == AdaptiveMode.OneWay && deviation > 0)
            {
                return null; // Don't extend talks in OneWay mode
            }

            // Calculate this talk's share of the adjustment
            var totalRemainingAdaptiveTime = remainingTalks
                .Where(t => t.AllowAdaptive)
                .Sum(t => t.ActualDuration.TotalSeconds);

            if (totalRemainingAdaptiveTime <= 0)
            {
                return null;
            }

            // Proportional share based on this talk's duration
            var fraction = talk.ActualDuration.TotalSeconds / totalRemainingAdaptiveTime;
            var adjustment = deviation * fraction;

            // Calculate new duration
            var newDurationSecs = talk.ActualDuration.TotalSeconds + adjustment;

            // Ensure minimum duration of 1 minute
            newDurationSecs = Math.Max(60, newDurationSecs);

            var newDuration = TimeSpan.FromSeconds(Math.Round(newDurationSecs));

            Log.Debug("Adaptive timer: Talk {TalkId} adjusted from {Original} to {Adapted} (deviation: {Deviation}s)",
                talkId, talk.ActualDuration, newDuration, deviation);

            return newDuration;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error calculating adaptive duration for talk {TalkId}", talkId);
            return null;
        }
    }

    public TimeSpan? CalculateMeetingOverrun(int talkId)
    {
        try
        {
            var talks = _scheduleService.GetTalkScheduleItems().ToList();

            EnsureMeetingStartTime(talks);
            if (!_meetingStartTime.HasValue)
            {
                return null;
            }

            var plannedMeetingEnd = CalculatePlannedMeetingEnd(talks);
            var now = _dateTimeService.Now();

            // Calculate remaining program time
            var remainingTalks = talks.SkipWhile(t => t.Id != talkId).ToList();
            var remainingProgramTime = TimeSpan.FromSeconds(
                remainingTalks.Sum(t => t.ActualDuration.TotalSeconds) +
                (remainingTalks.Count - 1) * 30); // Changeover time

            var expectedEnd = now + remainingProgramTime;
            var overrun = expectedEnd - plannedMeetingEnd;

            // Only report significant overruns (2-20 minutes)
            if (Math.Abs(overrun.TotalMinutes) < 2 || Math.Abs(overrun.TotalMinutes) > 20)
            {
                return null;
            }

            return overrun;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error calculating meeting overrun");
            return null;
        }
    }

    private AdaptiveMode GetAdaptiveMode()
    {
        var options = _optionsService.GetOptions();
        return _optionsService.MidWeekOrWeekend == MidWeekOrWeekend.MidWeek
            ? options.MidWeekAdaptiveMode
            : options.WeekendAdaptiveMode;
    }

    private void EnsureMeetingStartTime(System.Collections.Generic.List<Models.TalkScheduleItem> talks)
    {
        if (_meetingStartTime.HasValue)
        {
            return;
        }

        // Try to infer from schedule
        var firstTalk = talks.FirstOrDefault();
        if (firstTalk == null)
        {
            return;
        }

        // Use the configured meeting start time from options, defaulting to reasonable times
        var now = _dateTimeService.Now();
        var options = _optionsService.GetOptions();

        // Default meeting start times
        TimeSpan defaultStart = _optionsService.MidWeekOrWeekend == MidWeekOrWeekend.MidWeek
            ? new TimeSpan(19, 0, 0)  // 7:00 PM for midweek
            : new TimeSpan(10, 0, 0); // 10:00 AM for weekend

        // Use today's date with the default start time
        _meetingStartTime = now.Date + defaultStart;

        // If we're past the default start time, assume we started earlier today
        if (now > _meetingStartTime)
        {
            // Estimate start time based on current progress
            var elapsedTalks = talks.TakeWhile(t => t.CompletedTimeSecs.HasValue).ToList();
            var elapsedTime = TimeSpan.FromSeconds(elapsedTalks.Sum(t => t.CompletedTimeSecs ?? 0));
            _meetingStartTime = now - elapsedTime;
        }

        Log.Debug("Inferred meeting start time: {StartTime}", _meetingStartTime);
    }

    private DateTime CalculatePlannedMeetingEnd(System.Collections.Generic.List<Models.TalkScheduleItem> talks)
    {
        if (!_meetingStartTime.HasValue)
        {
            return _dateTimeService.Now();
        }

        var totalDuration = TimeSpan.FromSeconds(
            talks.Sum(t => t.OriginalDuration.TotalSeconds) +
            (talks.Count - 1) * 30); // Changeover time

        // Add interval time for midweek meetings (song between parts)
        if (_optionsService.MidWeekOrWeekend == MidWeekOrWeekend.MidWeek)
        {
            totalDuration += TimeSpan.FromMinutes(3.5); // Song time
        }

        return _meetingStartTime.Value + totalDuration;
    }
}
