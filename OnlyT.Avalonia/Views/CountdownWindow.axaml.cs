using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using OnlyT.Avalonia.Controls;
using OnlyT.Avalonia.Services;

namespace OnlyT.Avalonia.Views;

public partial class CountdownWindow : Window
{
    private readonly IOptionsService? _optionsService;

    public event EventHandler? TimeUpEvent;

    public CountdownWindow()
    {
        InitializeComponent();

        _optionsService = Ioc.Default.GetService<IOptionsService>();

        if (_optionsService?.GetOptions().IsCountdownWindowTransparent == true)
        {
            // Match the WPF AllowsTransparency behaviour: make the window
            // chrome and root background see-through so only the inner
            // bordered countdown panel is visible on screen.
            TransparencyLevelHint = new List<WindowTransparencyLevel> { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            SystemDecorations = SystemDecorations.None;
        }

        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
    }

    public void Start(int totalSeconds)
    {
        var ctrl = this.FindControl<CountdownControl>("CountdownCtrl");
        if (ctrl != null)
        {
            ctrl.TimeUpEvent += OnCountdownTimeUp;
            ctrl.Start(totalSeconds);
        }
    }

    private void OnCountdownTimeUp(object? sender, EventArgs e)
    {
        var ctrl = this.FindControl<CountdownControl>("CountdownCtrl");
        if (ctrl != null)
        {
            ctrl.Stop();
            ctrl.TimeUpEvent -= OnCountdownTimeUp;
        }

        // Delay then notify
        DispatcherTimer.RunOnce(() =>
        {
            TimeUpEvent?.Invoke(this, EventArgs.Empty);
        }, TimeSpan.FromSeconds(1));
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ApplyTitleBarThemeToWindow(this);
        }

        RestoreWindowPlacement();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowPlacement();
    }

    private void RestoreWindowPlacement()
    {
        var placement = _optionsService?.GetOptions().CountdownWindowPlacement;
        if (placement == null)
            return;

        if (IsPositionOnScreen(placement.Left, placement.Top))
        {
            Position = new PixelPoint(placement.Left, placement.Top);
        }

        if (placement.Width > 0 && placement.Height > 0)
        {
            Width = placement.Width;
            Height = placement.Height;
        }
    }

    private void SaveWindowPlacement()
    {
        if (_optionsService == null) return;

        var options = _optionsService.GetOptions();
        options.CountdownWindowPlacement = new WindowPlacement
        {
            Left = Position.X,
            Top = Position.Y,
            Width = (int)Width,
            Height = (int)Height
        };

        _optionsService.SaveOptions(options);
    }

    private bool IsPositionOnScreen(int left, int top)
    {
        var screens = Screens?.All;
        if (screens == null || !screens.Any())
            return left >= -32000 && top >= -32000 && left < 32000 && top < 32000;

        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            if (left >= bounds.X - 100 && left < bounds.X + bounds.Width &&
                top >= bounds.Y - 100 && top < bounds.Y + bounds.Height)
            {
                return true;
            }
        }

        return false;
    }
}
