using System;
using Avalonia.Threading;
using OnlyT.Common.Services.DateTime;
using Serilog;

namespace OnlyT.Avalonia.Services.Reminders;

/// <summary>
/// Service that reminds operators to start the next talk if too much time has passed
/// </summary>
public class ReminderService : IReminderService
{
    // Time after which a reminder is triggered (60 seconds default)
    private const int DefaultReminderIntervalSeconds = 60;
    // Interval between repeated reminders
    private const int RepeatReminderIntervalSeconds = 30;

    private readonly IOptionsService _optionsService;
    private readonly IDateTimeService _dateTimeService;
    private readonly DispatcherTimer _reminderTimer;

    private DateTime? _lastTimerStopped;
    private DateTime? _lastReminderShown;
    private bool _reminderActive;
    private int _lastStoppedTalkId;

    public event EventHandler<ReminderEventArgs>? ReminderTriggered;

    public ReminderService(IOptionsService optionsService, IDateTimeService dateTimeService)
    {
        _optionsService = optionsService;
        _dateTimeService = dateTimeService;

        // Timer checks every 5 seconds if a reminder should be shown
        _reminderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _reminderTimer.Tick += OnReminderTimerTick;
    }

    public void OnTimerStarted(int talkId)
    {
        // Timer started - clear any pending reminders
        _reminderTimer.Stop();
        _lastTimerStopped = null;
        _lastReminderShown = null;

        if (_reminderActive)
        {
            _reminderActive = false;
            ReminderTriggered?.Invoke(this, new ReminderEventArgs
            {
                IsShowing = false,
                Message = string.Empty
            });
        }
    }

    public void OnTimerStopped(int talkId)
    {
        // Timer stopped - start watching for reminder trigger
        if (!_optionsService.GetOptions().TimerReminder)
        {
            return; // Reminders disabled
        }

        _lastStoppedTalkId = talkId;
        _lastTimerStopped = _dateTimeService.Now();
        _reminderTimer.Start();

        Log.Debug("Reminder timer started after talk {TalkId}", talkId);
    }

    public void DismissReminder()
    {
        _reminderActive = false;
        _lastReminderShown = _dateTimeService.Now();

        ReminderTriggered?.Invoke(this, new ReminderEventArgs
        {
            IsShowing = false,
            Message = string.Empty
        });
    }

    public void Shutdown()
    {
        _reminderTimer.Stop();
        _reminderActive = false;
    }

    private void OnReminderTimerTick(object? sender, EventArgs e)
    {
        if (ShouldShowReminder())
        {
            ShowReminder();
        }
    }

    private bool ShouldShowReminder()
    {
        if (!_lastTimerStopped.HasValue)
        {
            return false;
        }

        var now = _dateTimeService.Now();
        var timeSinceStopped = now - _lastTimerStopped.Value;

        // Check if enough time has passed since timer stopped
        if (timeSinceStopped.TotalSeconds < DefaultReminderIntervalSeconds)
        {
            return false;
        }

        // Check if we've shown a reminder too recently
        if (_lastReminderShown.HasValue)
        {
            var timeSinceLastReminder = now - _lastReminderShown.Value;
            if (timeSinceLastReminder.TotalSeconds < RepeatReminderIntervalSeconds)
            {
                return false;
            }
        }

        return true;
    }

    private void ShowReminder()
    {
        _reminderActive = true;
        _lastReminderShown = _dateTimeService.Now();

        Log.Information("Showing timer reminder - too long since last talk ended");

        ReminderTriggered?.Invoke(this, new ReminderEventArgs
        {
            IsShowing = true,
            Message = "Time to start the next talk!"
        });
    }
}
