using System;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnlyT.Avalonia.Services;

namespace OnlyT.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the countdown window that displays time until meeting starts
/// </summary>
public partial class CountdownViewModel : ObservableObject
{
    private readonly IOptionsService? _optionsService;
    private readonly DispatcherTimer _timer;
    private DateTime _targetTime;
    private Action? _closeAction;
    private Action? _timeUpAction;

    [ObservableProperty]
    private string _countdownDisplay = "00:00";

    [ObservableProperty]
    private SolidColorBrush _countdownColor = new(Colors.LimeGreen);

    [ObservableProperty]
    private IBrush _windowBackground = new SolidColorBrush(Color.Parse("#1a1a1a"));

    [ObservableProperty]
    private IBrush _borderBackground = new SolidColorBrush(Color.Parse("#2a2a2a"));

    [ObservableProperty]
    private bool _isTransparent;

    public CountdownViewModel(IOptionsService optionsService)
    {
        _optionsService = optionsService;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += OnTimerTick;

        // Apply transparency setting
        ApplyTransparencySetting(_optionsService?.IsCountdownWindowTransparent ?? false);
    }

    // Parameterless constructor for design-time
    public CountdownViewModel()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += OnTimerTick;
        CountdownDisplay = "05:00";
    }

    /// <summary>
    /// Start the countdown for the specified duration
    /// </summary>
    public void Start(int durationMins, Action? closeAction = null, Action? timeUpAction = null)
    {
        _closeAction = closeAction;
        _timeUpAction = timeUpAction;
        _targetTime = DateTime.Now.AddMinutes(durationMins);
        _timer.Start();
        UpdateDisplay();
    }

    /// <summary>
    /// Start countdown using configured duration
    /// </summary>
    public void Start(Action? closeAction = null, Action? timeUpAction = null)
    {
        var duration = _optionsService?.CountdownDurationMins ?? 5;
        Start(duration, closeAction, timeUpAction);
    }

    /// <summary>
    /// Stop the countdown
    /// </summary>
    public void Stop()
    {
        _timer.Stop();
    }

    [RelayCommand]
    private void Close()
    {
        Stop();
        _closeAction?.Invoke();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateDisplay();
    }

    private void ApplyTransparencySetting(bool isTransparent)
    {
        IsTransparent = isTransparent;
        if (isTransparent)
        {
            WindowBackground = new SolidColorBrush(Color.FromArgb(180, 26, 26, 26));
            BorderBackground = new SolidColorBrush(Color.FromArgb(200, 42, 42, 42));
        }
        else
        {
            WindowBackground = new SolidColorBrush(Color.Parse("#1a1a1a"));
            BorderBackground = new SolidColorBrush(Color.Parse("#2a2a2a"));
        }
    }

    private void UpdateDisplay()
    {
        var remaining = _targetTime - DateTime.Now;

        if (remaining.TotalSeconds <= 0)
        {
            // Time's up
            CountdownDisplay = "00:00";
            CountdownColor = new SolidColorBrush(Colors.Red);
            _timer.Stop();
            _timeUpAction?.Invoke();
            return;
        }

        // Format display
        if (remaining.TotalHours >= 1)
        {
            CountdownDisplay = $"{(int)remaining.TotalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }
        else
        {
            CountdownDisplay = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        // Color based on time remaining
        if (remaining.TotalSeconds <= 60)
        {
            CountdownColor = new SolidColorBrush(Colors.Orange);
        }
        else if (remaining.TotalSeconds <= 30)
        {
            CountdownColor = new SolidColorBrush(Colors.Red);
        }
        else
        {
            CountdownColor = new SolidColorBrush(Colors.LimeGreen);
        }
    }
}
