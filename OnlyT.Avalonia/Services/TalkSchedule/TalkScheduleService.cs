namespace OnlyT.Avalonia.Services.TalkSchedule;

using System;
using System.Collections.Generic;
using System.Linq;
using Models;
using Options;

/// <summary>
/// Service to handle the delivery of a talk schedule based on current "Operating mode"
/// </summary>
internal sealed class TalkScheduleService : ITalkScheduleService
{
    private readonly IOptionsService _optionsService;

    private Lazy<IEnumerable<TalkScheduleItem>> _fileBasedSchedule = null!;
    private Lazy<IEnumerable<TalkScheduleItem>> _autoSchedule = null!;
    private Lazy<IEnumerable<TalkScheduleItem>> _manualSchedule = null!;

    public TalkScheduleService(IOptionsService optionsService)
    {
        _optionsService = optionsService;
        Reset();
    }

    public void Reset()
    {
        _fileBasedSchedule = new Lazy<IEnumerable<TalkScheduleItem>>(() =>
            TalkScheduleFileBased.Read(_optionsService.AutoBell));
        _autoSchedule = new Lazy<IEnumerable<TalkScheduleItem>>(() =>
            TalkScheduleAuto.Read(_optionsService));
        _manualSchedule = new Lazy<IEnumerable<TalkScheduleItem>>(() =>
            TalkScheduleManual.Read(_optionsService));
    }

    public bool SuccessGettingAutoFeedForMidWeekMtg()
    {
        return TalkScheduleAuto.SuccessGettingAutoFeedForMidWeekMtg;
    }

    public void SetModifiedDuration(int talkId, TimeSpan? modifiedDuration)
    {
        var t = GetTalkScheduleItem(talkId);
        if (t != null)
        {
            t.ModifiedDuration = modifiedDuration;
        }
    }

    public IEnumerable<TalkScheduleItem> GetTalkScheduleItems()
    {
        return _optionsService.OperatingMode switch
        {
            OperatingMode.ScheduleFile => _fileBasedSchedule.Value,
            OperatingMode.Automatic => _autoSchedule.Value,
            OperatingMode.Manual => _manualSchedule.Value,
            _ => _manualSchedule.Value
        };
    }

    public TalkScheduleItem? GetTalkScheduleItem(int id)
    {
        return GetTalkScheduleItems().SingleOrDefault(n => n.Id == id);
    }

    public int GetNext(int currentTalkId)
    {
        var talks = GetTalkScheduleItems().ToArray();
        if (_optionsService.OperatingMode == OperatingMode.Manual)
        {
            return talks.First().Id;
        }

        var foundCurrent = false;
        for (var n = 0; n < talks.Length; ++n)
        {
            var thisTalk = talks[n];
            if (thisTalk.Id.Equals(currentTalkId))
            {
                foundCurrent = true;
            }

            if (n != talks.Length - 1 && foundCurrent && talks[n + 1].ActualDuration != TimeSpan.Zero)
            {
                return talks[n + 1].Id;
            }
        }

        return 0;
    }

    /// <summary>
    /// Records completed time on the talk when a timer stops.
    /// Called from OperatorPageViewModel when timer stops.
    /// </summary>
    public void RecordTalkCompleted(int talkId, int elapsedSecs)
    {
        var talk = GetTalkScheduleItem(talkId);
        if (talk != null)
        {
            talk.CompletedTimeSecs = elapsedSecs;
        }
    }
}
