using System;
using OnlyT.Avalonia.MeetingTalkTimesFeed;
using OnlyT.Common.Services.DateTime;
using Serilog;

namespace OnlyT.Avalonia.Services;

/// <summary>
/// Service for managing meeting times and schedules
/// </summary>
public class MeetingTimesService : IMeetingTimesService
{
    private readonly IDateTimeService _dateTimeService;
    private TimesFeed? _timesFeed;
    private string _feedUri = string.Empty;

    public event EventHandler? MeetingDataChanged;

    public MeetingTimesService(IDateTimeService dateTimeService)
    {
        _dateTimeService = dateTimeService;
    }

    public string FeedUri
    {
        get => _feedUri;
        set
        {
            if (_feedUri != value)
            {
                _feedUri = value;
                if (!string.IsNullOrEmpty(_feedUri))
                {
                    _timesFeed = new TimesFeed(_feedUri);
                    MeetingDataChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    public Meeting? GetTodaysMeeting()
    {
        if (_timesFeed == null || string.IsNullOrEmpty(_feedUri))
        {
            Log.Information("Meeting times feed not configured");
            return null;
        }

        try
        {
            return _timesFeed.GetMeetingDataForToday(_dateTimeService);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting today's meeting data");
            return null;
        }
    }
}
