using System;
using System.Threading;
using Serilog;

namespace OnlyT.Avalonia.Services;

/// <summary>
/// Basic timer service for countdown functionality
/// </summary>
public class BasicTimerService : ITimerService
{
    private System.Threading.Timer? _timer;
    private TimeSpan _remainingTime;
    private TimeSpan _targetDuration;
    private TimerState _state = TimerState.Stopped;
    private DateTime _lastTickTime;

    public TimerState State => _state;
    public TimeSpan RemainingTime => _remainingTime;
    public TimeSpan TargetDuration => _targetDuration;
    public bool IsOvertime => _remainingTime < TimeSpan.Zero;

    public event EventHandler? TimerStarted;
    public event EventHandler? TimerStopped;
    public event EventHandler<TimeSpan>? TimeChanged;
    public event EventHandler? TimerEnded;
    public event EventHandler? TimerPaused;
    public event EventHandler? TimerResumed;

    public void Start(TimeSpan duration)
    {
        if (_state == TimerState.Running)
        {
            Stop();
        }

        _targetDuration = duration;
        _remainingTime = duration;
        _lastTickTime = DateTime.UtcNow;
        _state = TimerState.Running;

        _timer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

        Log.Information("Timer started: {Duration}", duration);
        TimerStarted?.Invoke(this, EventArgs.Empty);
        TimeChanged?.Invoke(this, _remainingTime);
    }

    public void Stop()
    {
        if (_state == TimerState.Stopped)
        {
            return;
        }

        _timer?.Dispose();
        _timer = null;
        _state = TimerState.Stopped;
        _remainingTime = TimeSpan.Zero;

        Log.Information("Timer stopped");
        TimerStopped?.Invoke(this, EventArgs.Empty);
        TimeChanged?.Invoke(this, _remainingTime);
    }

    public void Pause()
    {
        if (_state != TimerState.Running)
        {
            return;
        }

        _timer?.Dispose();
        _timer = null;
        _state = TimerState.Paused;

        Log.Information("Timer paused at {RemainingTime}", _remainingTime);
        TimerPaused?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        if (_state != TimerState.Paused)
        {
            return;
        }

        _lastTickTime = DateTime.UtcNow;
        _state = TimerState.Running;
        _timer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

        Log.Information("Timer resumed at {RemainingTime}", _remainingTime);
        TimerResumed?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        var wasRunning = _state == TimerState.Running;

        Stop();

        if (wasRunning)
        {
            Start(_targetDuration);
        }
        else
        {
            _remainingTime = _targetDuration;
            TimeChanged?.Invoke(this, _remainingTime);
        }
    }

    private void OnTimerTick(object? state)
    {
        if (_state != TimerState.Running)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var elapsed = now - _lastTickTime;
        _lastTickTime = now;

        var previousTime = _remainingTime;
        _remainingTime -= elapsed;

        // Check if we just crossed zero
        if (previousTime > TimeSpan.Zero && _remainingTime <= TimeSpan.Zero)
        {
            Log.Information("Timer ended");
            TimerEnded?.Invoke(this, EventArgs.Empty);
        }

        // Notify on second boundaries
        if ((int)previousTime.TotalSeconds != (int)_remainingTime.TotalSeconds)
        {
            TimeChanged?.Invoke(this, _remainingTime);
        }
    }
}
