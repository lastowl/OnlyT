using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Views;
using OnlyT.Core.Abstractions;

namespace OnlyT.Avalonia.ViewModels;

/// <summary>
/// Main window view model - controls the timer
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ITimerService _timerService;
    private readonly IOptionsService _optionsService;
    private readonly IMonitorService _monitorService;
    private readonly IBellService _bellService;

    [ObservableProperty]
    private string _timeDisplay = "00:00";

    [ObservableProperty]
    private string _statusText = "Stopped";

    [ObservableProperty]
    private int _selectedDuration = 5;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    public ObservableCollection<int> AvailableDurations { get; } = new()
    {
        1, 2, 5, 10, 15, 30, 45, 60
    };

    public MainViewModel(
        ITimerService timerService,
        IOptionsService optionsService,
        IMonitorService monitorService,
        IBellService bellService)
    {
        _timerService = timerService;
        _optionsService = optionsService;
        _monitorService = monitorService;
        _bellService = bellService;

        _timerService.TimeChanged += OnTimeChanged;
        _timerService.TimerEnded += OnTimerEnded;
        _timerService.TimerStarted += OnTimerStarted;
        _timerService.TimerStopped += OnTimerStopped;
        _timerService.TimerPaused += OnTimerPaused;
        _timerService.TimerResumed += OnTimerResumed;

        var options = _optionsService.GetOptions();
        _selectedDuration = options.DefaultDurationMinutes;
    }

    [RelayCommand]
    private void Start()
    {
        _timerService.Start(TimeSpan.FromMinutes(SelectedDuration));
        ShowTimerOutputWindow();
    }

    [RelayCommand]
    private void Stop()
    {
        _timerService.Stop();
    }

    [RelayCommand]
    private void Pause()
    {
        if (IsPaused)
        {
            _timerService.Resume();
        }
        else
        {
            _timerService.Pause();
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _timerService.Reset();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = Ioc.Default.GetService<SettingsViewModel>()
        };
        settingsWindow.Show();
    }

    private void OnTimeChanged(object? sender, TimeSpan time)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (time < TimeSpan.Zero)
            {
                // Overtime - show negative time
                var overtime = time.Negate();
                TimeDisplay = $"-{(int)overtime.TotalMinutes:D2}:{overtime.Seconds:D2}";
            }
            else
            {
                TimeDisplay = $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
            }
        });
    }

    private void OnTimerEnded(object? sender, EventArgs e)
    {
        var options = _optionsService.GetOptions();
        if (options.IsBellEnabled)
        {
            _bellService.Play(options.BellVolumePercent);
        }
    }

    private void OnTimerStarted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsRunning = true;
            IsPaused = false;
            StatusText = "Running";
        });
    }

    private void OnTimerStopped(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsRunning = false;
            IsPaused = false;
            StatusText = "Stopped";
            TimeDisplay = "00:00";
        });
    }

    private void OnTimerPaused(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPaused = true;
            StatusText = "Paused";
        });
    }

    private void OnTimerResumed(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPaused = false;
            StatusText = "Running";
        });
    }

    private void ShowTimerOutputWindow()
    {
        // Check if timer output window already exists
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var existingWindow = desktop.Windows.OfType<TimerOutputWindow>().FirstOrDefault();
            if (existingWindow != null)
            {
                existingWindow.Activate();
                return;
            }

            var timerOutputWindow = new TimerOutputWindow
            {
                DataContext = Ioc.Default.GetService<TimerOutputViewModel>()
            };
            timerOutputWindow.Show();
        }
    }
}
