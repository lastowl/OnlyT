namespace OnlyT.Avalonia.Services.Report;

using System;
using System.IO;
using OnlyT.Common.Services.DateTime;
using OnlyT.Report.Database;
using OnlyT.Report.Models;
using OnlyT.Utils;
using Serilog;

/// <summary>
/// Service for storing meeting timing data locally using LiteDB.
/// Used to generate PDF timing reports.
/// </summary>
internal sealed class LocalTimingDataStoreService : ILocalTimingDataStoreService
{
    private const int MeetingMinsOutOfRange = 20;

    private readonly IDateTimeService _dateTimeService;
    private readonly string? _optionsIdentifier;

    private LocalData? _localData;
    private string? _currentPartDescription;
    private bool _currentPartIsStudentTalk;
    private MeetingTimes? _mtgTimes;
    private bool _initialised;

    public LocalTimingDataStoreService(IDateTimeService dateTimeService, string? optionsIdentifier = null)
    {
        _dateTimeService = dateTimeService;
        _optionsIdentifier = optionsIdentifier;
    }

    public MeetingTimes? MeetingTimes => _mtgTimes;

    public DateTime LastTimerStop
    {
        get
        {
            EnsureInitialised();
            return _mtgTimes?.LastTimerStop ?? DateTime.MinValue;
        }
    }

    public void DeleteAllData()
    {
        try
        {
            EnsureInitialised();
            _localData?.DeleteAllMeetingTimesData();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not delete timing data");
        }
    }

    public void InsertSongSegment(DateTime startTime, string description, TimeSpan plannedDuration)
    {
        try
        {
            EnsureInitialised();
            _mtgTimes?.InsertSongSegment(startTime, description, plannedDuration);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not insert song segment");
        }
    }

    public void InsertConcludingSongSegment(DateTime startTime, DateTime endTime, string description, TimeSpan plannedDuration)
    {
        try
        {
            EnsureInitialised();
            _mtgTimes?.InsertConcludingSongSegment(startTime, endTime, description, plannedDuration);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not insert concluding song segment");
        }
    }

    public void InsertPlannedMeetingEnd(DateTime plannedEnd)
    {
        try
        {
            EnsureInitialised();
            _mtgTimes?.InsertPlannedMeetingEnd(plannedEnd);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not insert meeting planned end");
        }
    }

    public void InsertMeetingStart(DateTime value)
    {
        try
        {
            EnsureInitialised();
            _mtgTimes?.InsertMeetingStart(value);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not insert meeting start");
        }
    }

    public void InsertActualMeetingEnd(DateTime end)
    {
        try
        {
            EnsureInitialised();
            _mtgTimes?.InsertActualMeetingEnd(end);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not insert meeting end");
        }
    }

    public void InsertTimerStart(
        string description,
        bool isSongSegment,
        bool isStudentTalk,
        TimeSpan plannedDuration,
        TimeSpan adaptedDuration)
    {
        try
        {
            EnsureInitialised();
            _currentPartDescription = description;
            _currentPartIsStudentTalk = isStudentTalk;

            _mtgTimes?.InsertTimerStart(
                description, isSongSegment, isStudentTalk, plannedDuration, adaptedDuration);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not insert timer start");
        }
    }

    public void InsertTimerStop()
    {
        try
        {
            EnsureInitialised();
            _mtgTimes?.InsertTimerStop(_currentPartDescription, _currentPartIsStudentTalk);
            _currentPartDescription = null;
            _currentPartIsStudentTalk = false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not insert timer stop");
        }
    }

    public MeetingTimes? GetCurrentMeetingTimes()
    {
        try
        {
            EnsureInitialised();

            return _mtgTimes == null
                ? null
                : _localData?.GetMeetingTimes(_mtgTimes.Session);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not retrieve meeting times data");
        }

        return null;
    }

    public bool ValidCurrentMeetingTimes()
    {
        EnsureInitialised();

        if (_mtgTimes == null)
        {
            return false;
        }

        var valid =
            _mtgTimes.MeetingStart != default &&
            _mtgTimes.MeetingActualEnd != default &&
            Math.Abs(_mtgTimes.GetMeetingOvertime().TotalMinutes) < MeetingMinsOutOfRange;

        if (!valid)
        {
            if (_mtgTimes.MeetingStart == default)
            {
                Log.Debug("Meeting Start not set");
            }

            if (_mtgTimes.MeetingActualEnd == default)
            {
                Log.Debug("Meeting End not set");
            }

            var mins = Math.Abs(_mtgTimes.GetMeetingOvertime().TotalMinutes);

            if (mins >= MeetingMinsOutOfRange)
            {
                var minsOvertime = _mtgTimes.GetMeetingOvertime().TotalMinutes;
                var overOrUnderStr = minsOvertime > 0 ? "overtime" : "undertime";
                Log.Debug("Meeting duration is out of range ({Duration} {OverUnder})", TimeSpan.FromMinutes(mins), overOrUnderStr);
            }
        }

        return valid;
    }

    public void PurgeCurrentMeetingTimes()
    {
        EnsureInitialised();
        _mtgTimes?.Purge();
    }

    public void Save()
    {
        try
        {
            EnsureInitialised();
            _localData?.Save(_mtgTimes);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not save timing data");
        }
    }

    public HistoricalMeetingTimes? GetHistoricalMeetingTimes()
    {
        try
        {
            EnsureInitialised();

            var now = _dateTimeService.Now().Date;

            return _localData?.GetHistoricalTimingData(now);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not retrieve historical meeting data");
        }

        return null;
    }

    private void EnsureInitialised()
    {
        if (!_initialised)
        {
            Init();
            _initialised = true;
        }
    }

    private void Init()
    {
        try
        {
            var folder = FileUtils.GetTimingReportsDatabaseFolder(_optionsIdentifier);
            var dbFilePath = Path.Combine(folder, "TimingDataV2.db");

            _localData = new LocalData(dbFilePath);
            _mtgTimes = new MeetingTimes(_dateTimeService);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not initialise timing data store");
        }
    }
}
