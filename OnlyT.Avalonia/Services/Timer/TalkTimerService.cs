namespace OnlyT.Avalonia.Services.Timer
{
    using System;
    using System.Diagnostics;
    using global::Avalonia.Threading;
    using EventArgsTypes;
    using Models;

    /// <summary>
    /// Timer service
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    internal sealed class TalkTimerService : ITalkTimerService
    {
        private const int DefaultClosingSecs = 30; // TalkScheduleItem.DefaultClosingSecs
        private readonly Stopwatch _stopWatch = new();
        private readonly DispatcherTimer _timer = new();
        private readonly TimeSpan _timerInterval = TimeSpan.FromMilliseconds(100);
        private int _targetSecs = 600;
        private int _closingSecs = DefaultClosingSecs;
        private int? _talkId;
        private TimeSpan _currentTimeElapsed = TimeSpan.Zero;
        private int _currentSecondsElapsed;
        private bool _isCountingUp;
        private bool _isPaused;

        public TalkTimerService()
        {
            _timer.Interval = _timerInterval;
            _timer.Tick += TimerElapsedHandler;
        }

        public event EventHandler<TimerChangedEventArgs>? TimerChangedEvent;

        public event EventHandler<TimerStartedEventArgs>? TimerStartedEvent;

        public event EventHandler<TimerStartStopEventArgs>? TimerStartStopFromApiEvent;

        /// <summary>
        /// Gets a value indicating whether the timer is running
        /// </summary>
        public bool IsRunning => _stopWatch.IsRunning;

        /// <summary>
        /// Gets a value indicating whether the timer is paused
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Gets or sets the current number of seconds elapsed
        /// </summary>
        public int CurrentSecondsElapsed
        {
            get => _currentSecondsElapsed;
            set
            {
                if (_currentSecondsElapsed != value)
                {
                    _currentSecondsElapsed = value;
                    OnTimerChangedEvent(new TimerChangedEventArgs
                    {
                        TargetSecs = _targetSecs,
                        ElapsedSecs = _currentSecondsElapsed,
                        IsRunning = IsRunning,
                        ClosingSecs = _closingSecs,
                        IsCountingUp = _isCountingUp,
                    });
                }
            }
        }

        /// <summary>
        /// Gets or sets the current elapsed time
        /// </summary>
        private TimeSpan CurrentTimeElapsed
        {
            get => _currentTimeElapsed;
            set
            {
                if (_currentTimeElapsed != value)
                {
                    _currentTimeElapsed = value;
                    CurrentSecondsElapsed = (int)_currentTimeElapsed.TotalSeconds;
                }
            }
        }

        public void SetupTalk(int talkId, int targetSeconds, int closingSecs)
        {
            _talkId = talkId;
            _targetSecs = targetSeconds;
            _closingSecs = closingSecs;
        }

        public void AdjustTarget(int newTargetSecs)
        {
            _targetSecs = newTargetSecs;
        }

        public TimerStartStopEventArgs StartTalkTimerFromApi(int talkId)
        {
            var result = new TimerStartStopEventArgs
            {
                TalkId = talkId,
                Command = StartStopTimerCommands.Start
            };

            OnTimerStartStopFromApiEvent(result);
            return result;
        }

        public TimerStartStopEventArgs StopTalkTimerFromApi(int talkId)
        {
            var result = new TimerStartStopEventArgs
            {
                TalkId = talkId,
                Command = StartStopTimerCommands.Stop
            };

            OnTimerStartStopFromApiEvent(result);
            return result;
        }

        /// <summary>
        /// Starts the timer
        /// </summary>
        /// <param name="targetSecs">The target duration of the talk.</param>
        /// <param name="talkId">The Id of the talk that is being timed.</param>
        /// <param name="isCountingUp">Indicates if the timer is counting up rather than down.</param>
        public void Start(int targetSecs, int talkId, bool isCountingUp)
        {
            _targetSecs = targetSecs;
            _talkId = talkId;
            _isCountingUp = isCountingUp;
            _stopWatch.Start();
            UpdateTimerValue();
            _timer.Start();

            TimerStartedEvent?.Invoke(this, new TimerStartedEventArgs
            {
                TargetSecs = targetSecs,
                ClosingSecs = _closingSecs,
                IsCountingUp = _isCountingUp,
            });
        }

        /// <summary>
        /// Stops the timer
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
            _talkId = null;
            _isPaused = false;

            _stopWatch.Reset();
            UpdateTimerValue();
        }

        /// <summary>
        /// Pauses the timer
        /// </summary>
        public void Pause()
        {
            if (IsRunning && !_isPaused)
            {
                _stopWatch.Stop();
                _timer.Stop();
                _isPaused = true;
                UpdateTimerValue();
            }
        }

        /// <summary>
        /// Resumes the timer
        /// </summary>
        public void Resume()
        {
            if (_isPaused)
            {
                _stopWatch.Start();
                _timer.Start();
                _isPaused = false;
                UpdateTimerValue();
            }
        }

        public TimerStatus GetStatus()
        {
            return new TimerStatus
            {
                TalkId = _talkId,
                TargetSeconds = _targetSecs,
                IsRunning = IsRunning,
                TimeElapsed = CurrentTimeElapsed,
                ClosingSecs = _closingSecs
            };
        }
        
        public ClockRequestInfo GetClockRequestInfo()
        {
            return new ClockRequestInfo
            {
                TargetSeconds = _targetSecs,
                ElapsedTime = _currentTimeElapsed,
                IsRunning = IsRunning,
                IsCountingUp = _isCountingUp,
                ClosingSecs = _closingSecs,
            };
        }

        private void OnTimerChangedEvent(TimerChangedEventArgs e)
        {
            TimerChangedEvent?.Invoke(this, e);
        }

        private void TimerElapsedHandler(object? sender, EventArgs e)
        {
            _timer.Stop();
            UpdateTimerValue();
            _timer.Start();
        }

        private void UpdateTimerValue()
        {
            CurrentTimeElapsed = _stopWatch.Elapsed;
        }

        private void OnTimerStartStopFromApiEvent(TimerStartStopEventArgs e)
        {
            TimerStartStopFromApiEvent?.Invoke(this, e);
        }
    }
}
