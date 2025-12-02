namespace OnlyT.Avalonia.Services.Report;

using System;
using OnlyT.Report.Models;

/// <summary>
/// Service for storing meeting timing data used to generate PDF reports
/// </summary>
public interface ILocalTimingDataStoreService
{
    DateTime LastTimerStop { get; }

    void InsertPlannedMeetingEnd(DateTime plannedEnd);

    void InsertSongSegment(DateTime startTime, string description, TimeSpan plannedDuration);

    void InsertConcludingSongSegment(DateTime startTime, DateTime endTime, string description, TimeSpan plannedDuration);

    void InsertMeetingStart(DateTime value);

    void InsertActualMeetingEnd(DateTime end);

    void InsertTimerStart(
        string description,
        bool isSongSegment,
        bool isStudentTalk,
        TimeSpan plannedDuration,
        TimeSpan adaptedDuration);

    void InsertTimerStop();

    MeetingTimes? GetCurrentMeetingTimes();

    bool ValidCurrentMeetingTimes();

    void PurgeCurrentMeetingTimes();

    void Save();

    void DeleteAllData();

    HistoricalMeetingTimes? GetHistoricalMeetingTimes();
}
