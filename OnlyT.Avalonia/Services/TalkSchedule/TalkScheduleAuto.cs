namespace OnlyT.Avalonia.Services.TalkSchedule;

using System;
using System.Collections.Generic;
using System.Linq;
using OnlyT.Avalonia.MeetingTalkTimesFeed;
using OnlyT.Avalonia.Models;
using OnlyT.Avalonia.Resources;
using OnlyT.Common.Services.DateTime;

/// <summary>
/// The talk schedule when in "Automatic" operating mode
/// </summary>
internal static class TalkScheduleAuto
{
    private static readonly DateTime January2020Change = new(2020, 1, 6);

    // midweek meeting sections.
    private const string SectionTreasures = "Treasures";
    private const string SectionMinistry = "Ministry";
    private const string SectionLiving = "Living";

    // weekend sections.
    private const string SectionWeekend = "Weekend";

    public static bool SuccessGettingAutoFeedForMidWeekMtg { get; private set; }

    /// <summary>
    /// Gets the talk schedule.
    /// </summary>
    /// <param name="optionsService">Options service.</param>
    /// <returns>A collection of TalkScheduleItem.</returns>
    public static List<TalkScheduleItem> Read(IOptionsService optionsService)
    {
        var isCircuitVisit = optionsService.IsCircuitVisit;
        var autoBell = optionsService.IsBellEnabled && optionsService.AutoBell;
        var isJanuary2020OrLater = DateTime.Now.Date >= January2020Change;

        return optionsService.MidWeekOrWeekend == Options.MidWeekOrWeekend.Weekend
            ? GetWeekendMeetingSchedule(isCircuitVisit)
            : GetMidweekMeetingSchedule(isCircuitVisit, autoBell, isJanuary2020OrLater, null);
    }

    private static TalkScheduleItem CreateTreasuresItem(
        TalkTypesAutoMode talkType,
        string talkName,
        TimeSpan startOffset,
        TimeSpan duration,
        bool isStudentTalk,
        bool useBell,
        bool autoBell,
        bool persistFinalTimerValue)
    {
        return new TalkScheduleItem(talkType, talkName, SectionTreasures, Strings.SECTION_TREASURES)
        {
            StartOffsetIntoMeeting = startOffset,
            OriginalDuration = duration,
            BellApplicable = useBell,
            AutoBell = autoBell,
            IsStudentTalk = isStudentTalk,
            PersistFinalTimerValue = persistFinalTimerValue,
            Editable = true
        };
    }

    private static List<TalkScheduleItem> GetTreasuresSchedule(
        bool autoBell, bool isJanuary2020OrLater)
    {
        if (!isJanuary2020OrLater)
        {
            return GetTreasuresSchedulePreJan2020(autoBell);
        }

        return
        [
            CreateTreasuresItem(
                TalkTypesAutoMode.OpeningComments,
                Strings.TALK_OPENING_COMMENTS,
                new TimeSpan(0, 5, 0),
                TimeSpan.FromMinutes(1),
                false,
                false,
                autoBell,
                false),

            CreateTreasuresItem(
                TalkTypesAutoMode.TreasuresTalk,
                Strings.TALK_TREASURES,
                new TimeSpan(0, 6, 20),
                TimeSpan.FromMinutes(10),
                false,
                false,
                autoBell,
                false),

            CreateTreasuresItem(
                TalkTypesAutoMode.DiggingTalk,
                Strings.TALK_DIGGING,
                new TimeSpan(0, 16, 40),
                TimeSpan.FromMinutes(10),
                false,
                false,
                autoBell,
                false),

            CreateTreasuresItem(
                TalkTypesAutoMode.Reading,
                Strings.TALK_READING,
                new TimeSpan(0, 27, 0),
                TimeSpan.FromMinutes(4),
                true,
                true,
                autoBell,
                true)
        ];
    }

    private static List<TalkScheduleItem> GetTreasuresSchedulePreJan2020(bool autoBell)
    {
        return
        [
            CreateTreasuresItem(
                TalkTypesAutoMode.OpeningComments,
                Strings.TALK_OPENING_COMMENTS,
                new TimeSpan(0, 5, 0),
                TimeSpan.FromMinutes(3),
                false,
                false,
                autoBell,
                false),

            CreateTreasuresItem(
                TalkTypesAutoMode.TreasuresTalk,
                Strings.TALK_TREASURES,
                new TimeSpan(0, 8, 20),
                TimeSpan.FromMinutes(10),
                false,
                false,
                autoBell,
                false),

            CreateTreasuresItem(
                TalkTypesAutoMode.DiggingTalk,
                Strings.TALK_DIGGING,
                new TimeSpan(0, 18, 40),
                TimeSpan.FromMinutes(8),
                false,
                false,
                autoBell,
                false),

            CreateTreasuresItem(
                TalkTypesAutoMode.Reading,
                Strings.TALK_READING,
                new TimeSpan(0, 27, 0),
                TimeSpan.FromMinutes(4),
                true,
                true,
                autoBell,
                true)
        ];
    }

    private static TalkScheduleItem CreateMinistryItem(
        TalkTypesAutoMode talkType,
        string talkName,
        TimeSpan startOffset,
        TimeSpan duration,
        bool isStudentTalk,
        bool useBell,
        bool autoBell,
        bool persistFinalTimerValue,
        bool editableTime)
    {
        return new TalkScheduleItem(talkType, talkName, SectionMinistry, Strings.SECTION_MINISTRY)
        {
            StartOffsetIntoMeeting = startOffset,
            OriginalDuration = duration,
            BellApplicable = useBell,
            AutoBell = autoBell,
            PersistFinalTimerValue = persistFinalTimerValue,
            Editable = editableTime,
            IsStudentTalk = isStudentTalk
        };
    }

    private static List<TalkScheduleItem> GetMinistrySchedule(Meeting? meetingData, bool autoBell)
    {
        var result = new List<TalkScheduleItem>();

        var timers = new List<TalkTimer>();

        const int maxItems = 4;

        for (int n = 0; n < maxItems; ++n)
        {
            var talkType = TalkTypesUtils.GetMinistryTalkType(n);

            TalkTimer? item;

            if (meetingData == null)
            {
                // failed to download auto schedule!
                item = new TalkTimer
                {
                    IsStudentTalk = true,
                    Minutes = 3, // as good a guess as any
                    TalkType = talkType
                };
            }
            else
            {
                item = meetingData.Talks.FirstOrDefault(x => x.TalkType.Equals(talkType));
            }

            if (item != null)
            {
                timers.Add(item);
            }
        }

        var startOffset = new TimeSpan(0, 32, 20);

        for (var n = 0; n < timers.Count; ++n)
        {
            var talkType = TalkTypesUtils.GetAutoModeMinistryTalkType(n);
            var timer = timers[n];

            result.Add(CreateMinistryItem(
                talkType,
                GetMinistryItemTitle(n + 1),
                startOffset,
                TimeSpan.FromMinutes(timer.Minutes),
                timer.IsStudentTalk,
                timer.IsStudentTalk,
                autoBell,
                timer.IsStudentTalk,
                true));

            startOffset = startOffset.Add(TimeSpan.FromMinutes(timer.Minutes));
            if (timer.IsStudentTalk)
            {
                // counsel...
                startOffset = startOffset.Add(TimeSpan.FromMinutes(1));
            }

            startOffset = startOffset.Add(TimeSpan.FromSeconds(20));
        }

        return result;
    }

    private static string GetMinistryItemTitle(int item)
    {
        return item switch
        {
            1 => Strings.MINISTRY1,
            2 => Strings.MINISTRY2,
            3 => Strings.MINISTRY3,
            4 => Strings.MINISTRY4,
            _ => throw new ArgumentException("Unknown item", nameof(item))
        };
    }

    private static TalkScheduleItem CreateLivingItem(
        TalkTypesAutoMode talkType,
        string talkName,
        TimeSpan startOffset,
        TimeSpan duration)
    {
        return new TalkScheduleItem(talkType, talkName, SectionLiving, Strings.SECTION_LIVING)
        {
            StartOffsetIntoMeeting = startOffset,
            OriginalDuration = duration,
            Editable = true,
            AllowAdaptive = true
        };
    }

    private static List<TalkScheduleItem> GetLivingSchedule(bool isCircuitVisit, Meeting? meetingData)
    {
        var result = new List<TalkScheduleItem>();

        var timerPart1 = meetingData?.Talks.FirstOrDefault(x => x.TalkType.Equals(TalkTypes.Living1)) ??
                         new TalkTimer { Minutes = 15, TalkType = TalkTypes.Living1 };

        var timerPart2 = meetingData?.Talks.FirstOrDefault(x => x.TalkType.Equals(TalkTypes.Living2));

        result.Add(CreateLivingItem(
            TalkTypesAutoMode.LivingPart1,
            Strings.TALK_LIVING1,
            new TimeSpan(0, 51, 40),
            TimeSpan.FromMinutes(timerPart1.Minutes)));

        result.Add(CreateLivingItem(
            TalkTypesAutoMode.LivingPart2,
            Strings.TALK_LIVING2,
            new TimeSpan(0, 51, 40).Add(TimeSpan.FromMinutes(timerPart1.Minutes)),
            TimeSpan.FromMinutes(timerPart2?.Minutes ?? 0)));

        if (isCircuitVisit)
        {
            result.Add(CreateLivingItem(
                TalkTypesAutoMode.ConcludingComments,
                Strings.TALK_CONCLUDING_COMMENTS,
                new TimeSpan(1, 7, 0),
                TimeSpan.FromMinutes(3)));

            result.Add(CreateLivingItem(
                TalkTypesAutoMode.CircuitServiceTalk,
                Strings.TALK_SERVICE,
                new TimeSpan(1, 10, 0),
                TimeSpan.FromMinutes(30)));
        }
        else
        {
            result.Add(CreateLivingItem(
                TalkTypesAutoMode.CongBibleStudy,
                Strings.TALK_CONG_STUDY,
                new TimeSpan(1, 7, 0),
                TimeSpan.FromMinutes(30)));

            result.Add(CreateLivingItem(
                TalkTypesAutoMode.ConcludingComments,
                Strings.TALK_CONCLUDING_COMMENTS,
                new TimeSpan(1, 37, 0),
                TimeSpan.FromMinutes(3)));
        }

        return result;
    }

    private static List<TalkScheduleItem> GetMidweekMeetingSchedule(
        bool isCircuitVisit,
        bool autoBell,
        bool isJanuary2020OrLater,
        Meeting? meetingData)
    {
        var result = new List<TalkScheduleItem>();

        // Treasures...
        result.AddRange(GetTreasuresSchedule(autoBell, isJanuary2020OrLater));

        // Ministry...
        result.AddRange(GetMinistrySchedule(meetingData, autoBell));

        // Living...
        result.AddRange(GetLivingSchedule(isCircuitVisit, meetingData));

        return result;
    }

    private static TalkScheduleItem CreateWeekendItem(
        TalkTypesAutoMode talkType,
        string talkName,
        TimeSpan startOffset,
        TimeSpan duration,
        bool allowAdaptive)
    {
        return new TalkScheduleItem(talkType, talkName, SectionWeekend, Strings.SECTION_WEEKEND)
        {
            StartOffsetIntoMeeting = startOffset,
            OriginalDuration = duration,
            Editable = true,
            AllowAdaptive = allowAdaptive
        };
    }

    private static List<TalkScheduleItem> GetWeekendMeetingSchedule(bool isCircuitVisit)
    {
        var result = new List<TalkScheduleItem>();

        if (isCircuitVisit)
        {
            result.Add(CreateWeekendItem(
                TalkTypesAutoMode.PublicTalk,
                Strings.TALK_PUBLIC,
                new TimeSpan(0, 5, 0),
                TimeSpan.FromMinutes(30),
                false));

            // song here
            result.Add(CreateWeekendItem(
                TalkTypesAutoMode.Watchtower,
                Strings.TALK_WT,
                new TimeSpan(0, 40, 0),
                TimeSpan.FromMinutes(30),
                true));

            result.Add(CreateWeekendItem(
                TalkTypesAutoMode.CircuitServiceTalk,
                Strings.TALK_CONCLUDING,
                new TimeSpan(0, 70, 0),
                TimeSpan.FromMinutes(30),
                true));
        }
        else
        {
            result.Add(CreateWeekendItem(
                TalkTypesAutoMode.PublicTalk,
                Strings.TALK_PUBLIC,
                new TimeSpan(0, 5, 0),
                TimeSpan.FromMinutes(30),
                false));

            // song
            result.Add(CreateWeekendItem(
                TalkTypesAutoMode.Watchtower,
                Strings.TALK_WT,
                new TimeSpan(0, 40, 0),
                TimeSpan.FromMinutes(60),
                true));
        }

        return result;
    }
}
