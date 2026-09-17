namespace OnlyT.Avalonia.Services.Timer
{
    using System;
    using EventArgsTypes;
    using Models;

    public interface ITalkTimerService
    {
        event EventHandler<TimerChangedEventArgs> TimerChangedEvent;

        event EventHandler<TimerStartedEventArgs> TimerStartedEvent;

        event EventHandler<TimerStartStopEventArgs> TimerStartStopFromApiEvent;

        int CurrentSecondsElapsed { get; set; }

        bool IsRunning { get; }

        bool IsPaused { get; }

        void Start(int targetSecs, int talkId, bool isCountingUp, bool persistFinalTimerValue = false);

        void Stop();

        void Pause();

        void Resume();

        ClockRequestInfo GetClockRequestInfo();

        TimerStatus GetStatus();

        void SetupTalk(int talkId, int targetSeconds, int closingSecs);

        /// <summary>
        /// Records that <see cref="Start"/> will follow shortly (the operator page waits for the next
        /// second boundary), so the status reports the timer as running in the meantime.
        /// </summary>
        void BeginStarting();

        void AdjustTarget(int newTargetSecs);

        TimerStartStopEventArgs StartTalkTimerFromApi(int talkId);

        TimerStartStopEventArgs StopTalkTimerFromApi(int talkId);
    }
}
