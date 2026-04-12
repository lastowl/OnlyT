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

        void Start(int targetSecs, int talkId, bool isCountingUp);

        void Stop();

        void Pause();

        void Resume();

        ClockRequestInfo GetClockRequestInfo();

        TimerStatus GetStatus();

        void SetupTalk(int talkId, int targetSeconds, int closingSecs);

        void AdjustTarget(int newTargetSecs);

        TimerStartStopEventArgs StartTalkTimerFromApi(int talkId);

        TimerStartStopEventArgs StopTalkTimerFromApi(int talkId);
    }
}
