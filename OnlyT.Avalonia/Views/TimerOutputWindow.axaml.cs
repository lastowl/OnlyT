using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.NDI;
using Serilog;

namespace OnlyT.Avalonia.Views;

public partial class TimerOutputWindow : Window
{
    private readonly IOptionsService? _optionsService;
    private readonly INdiService? _ndiService;
    private bool _isShuttingDown = false;

    public TimerOutputWindow()
    {
        InitializeComponent();

        _optionsService = Ioc.Default.GetService<IOptionsService>();
        _ndiService = Ioc.Default.GetService<INdiService>();

        // Exit application when this window is closed
        Closing += OnWindowClosing;
        Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        var cmdArgs = Program.CommandLineArgs;

        // Handle NDI mode - adjust window size for NDI output
        if (cmdArgs.IsTimerNdi)
        {
            Width = cmdArgs.NdiWidth;
            Height = cmdArgs.NdiHeight;

            // Start NDI capture if enabled
            if (_ndiService != null && _ndiService.IsEnabled && Content is Control contentControl)
            {
                try
                {
                    _ndiService.StartCapture(contentControl, cmdArgs.NdiWidth, cmdArgs.NdiHeight, cmdArgs.NdiFrameRate);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "NDI capture failed to start");
                }
            }
        }
        else
        {
            // Only restore placement if not in full screen mode
            var options = _optionsService?.GetOptions();
            if (options != null && !options.FullScreenMode)
            {
                RestoreWindowPlacement();
            }
        }

        // Hide cursor if setting is disabled (default: cursor hidden for clean presentation)
        ApplyCursorVisibility();

        // Apply native title bar theme
        if (Application.Current is App app)
        {
            app.ApplyTitleBarThemeToWindow(this);
        }
    }

    /// <summary>
    /// Apply the cursor visibility setting
    /// </summary>
    public void ApplyCursorVisibility()
    {
        var options = _optionsService?.GetOptions();
        if (options != null && !options.ShowMousePointerInTimerDisplay)
        {
            // Hide the cursor on the timer display window
            Cursor = new Cursor(StandardCursorType.None);
        }
        else
        {
            // Show the default cursor
            Cursor = Cursor.Default;
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Prevent infinite recursion
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;

        // Stop NDI capture if running
        _ndiService?.StopCapture();

        // Save window placement before closing (if not fullscreen and not NDI mode)
        var cmdArgs = Program.CommandLineArgs;
        var options = _optionsService?.GetOptions();
        if (options != null && !options.FullScreenMode && !cmdArgs.IsTimerNdi)
        {
            SaveWindowPlacement();
        }

        // Unsubscribe to prevent being called again during shutdown
        Closing -= OnWindowClosing;

        // Shutdown the entire application when timer output window is closed
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    private void RestoreWindowPlacement()
    {
        var options = _optionsService?.GetOptions();
        var placement = options?.TimerOutputWindowPlacement;

        if (placement != null)
        {
            // Validate the position is on a visible screen
            if (IsPositionOnScreen(placement.Left, placement.Top))
            {
                Position = new PixelPoint(placement.Left, placement.Top);
            }

            if (placement.Width > 0 && placement.Height > 0)
            {
                Width = placement.Width;
                Height = placement.Height;
            }

            if (placement.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }
    }

    private bool IsPositionOnScreen(int left, int top)
    {
        // Check if position is within any available screen bounds
        var screens = Screens?.All;
        if (screens == null || !screens.Any())
        {
            // Can't determine screens, allow reasonable positions
            return left >= -32000 && top >= -32000 && left < 32000 && top < 32000;
        }

        // Check if the top-left corner is on any screen (with some margin)
        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            // Allow position if within extended bounds (100px margin for partially visible windows)
            if (left >= bounds.X - 100 && left < bounds.X + bounds.Width &&
                top >= bounds.Y - 100 && top < bounds.Y + bounds.Height)
            {
                return true;
            }
        }

        return false;
    }

    private void SaveWindowPlacement()
    {
        if (_optionsService == null) return;

        var options = _optionsService.GetOptions();
        options.TimerOutputWindowPlacement = new WindowPlacement
        {
            Left = Position.X,
            Top = Position.Y,
            Width = (int)Width,
            Height = (int)Height,
            IsMaximized = WindowState == WindowState.Maximized
        };

        _optionsService.SaveOptions(options);
    }
}
