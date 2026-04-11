using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.ViewModels;

namespace OnlyT.Avalonia.Views;

public partial class MainWindow : Window
{
    private const double MainWindowDefaultWidth = 395;
    private const double MainWindowDefaultHeight = 350;

    private const double MainWindowMinNormalWidth = 250;
    private const double MainWindowMinNormalHeight = 200;

    private const double MainWindowMinWidthAbsolute = 143;
    private const double MainWindowMinHeightAbsolute = 118;

    private const double MainWindowDefaultShrunkWidth = 143;
    private const double MainWindowDefaultShrunkHeight = 118;

    private readonly IOptionsService? _optionsService;
    private readonly OperatorPageViewModel? _operatorViewModel;
    private bool _isClosing;
    private bool _isShrunk;

    public MainWindow()
    {
        InitializeComponent();

        _optionsService = Ioc.Default.GetService<IOptionsService>();
        _operatorViewModel = Ioc.Default.GetService<OperatorPageViewModel>();

        // Set the OperatorPage's DataContext
        OperatorPageView.DataContext = _operatorViewModel;

        // Set minimum sizes
        MinWidth = MainWindowMinWidthAbsolute;
        MinHeight = MainWindowMinHeightAbsolute;

        // Restore window placement
        Opened += OnWindowOpened;
        Closing += OnWindowClosing;

        // Track size changes for shrink mode
        this.GetObservable(BoundsProperty).Subscribe(OnBoundsChanged);

        // Intercept minimize to honour the ShrinkOnMinimise option: instead
        // of actually minimising, snap the window to the compact shrunk
        // layout (which stays visible on screen). Matches WPF OnlyT.
        this.GetObservable(WindowStateProperty).Subscribe(OnWindowStateChanged);

        // Allow dragging when no title bar (shrunk mode)
        PointerPressed += OnPointerPressed;
    }

    private void OnWindowStateChanged(WindowState state)
    {
        if (state != WindowState.Minimized)
        {
            return;
        }

        if (_optionsService?.ShrinkOnMinimise != true)
        {
            return;
        }

        // Undo the minimize and shrink instead. Defer so we're not fighting
        // the window manager mid-transition.
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            WindowState = WindowState.Normal;
            if (!_isShrunk)
            {
                ShrinkToCompact();
            }
        });
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Allow drag when shrunk (no title bar)
        if (_isShrunk && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnBoundsChanged(Rect bounds)
    {
        var wasShrunk = _isShrunk;
        _isShrunk = bounds.Height < MainWindowMinNormalHeight || bounds.Width < MainWindowMinNormalWidth;

        if (wasShrunk != _isShrunk)
        {
            // Update window chrome for shrunk mode
            if (_isShrunk)
            {
                // Remove title bar in shrunk mode
                ExtendClientAreaToDecorationsHint = true;
                ExtendClientAreaTitleBarHeightHint = 0;
                ExtendClientAreaChromeHints = global::Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
            }
            else
            {
                // Restore title bar in normal mode
                ExtendClientAreaToDecorationsHint = false;
                ExtendClientAreaChromeHints = global::Avalonia.Platform.ExtendClientAreaChromeHints.Default;
            }

            // Notify ViewModel of shrink state change
            if (_operatorViewModel != null)
            {
                _operatorViewModel.InShrinkMode = _isShrunk;
            }
        }
    }

    private void OnWindowOpened(object? sender, System.EventArgs e)
    {
        var options = _optionsService?.GetOptions();

        if (options?.UseShrunkPlacementAtStart == true)
        {
            RestoreShrunkPlacement();
        }
        else
        {
            RestoreNormalPlacement();
        }

        // Apply native title bar theme
        if (Application.Current is App app)
        {
            app.ApplyTitleBarThemeToWindow(this);
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isClosing) return;

        // Prevent closing while timer is running
        if (_operatorViewModel?.IsRunning == true)
        {
            e.Cancel = true;
            return;
        }

        _isClosing = true;
        SaveWindowPlacement();
    }

    private void RestoreNormalPlacement()
    {
        var options = _optionsService?.GetOptions();
        var placement = options?.MainWindowPlacement;

        if (placement != null && placement.Width > 0 && placement.Height > 0)
        {
            if (IsPositionOnScreen(placement.Left, placement.Top))
            {
                Position = new PixelPoint(placement.Left, placement.Top);
            }

            Width = placement.Width;
            Height = placement.Height;

            if (placement.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }
        else
        {
            Width = MainWindowDefaultWidth;
            Height = MainWindowDefaultHeight;
        }
    }

    private void RestoreShrunkPlacement()
    {
        var options = _optionsService?.GetOptions();
        var placement = options?.MainWindowPlacementShrunk;

        if (placement != null && placement.Width > 0 && placement.Height > 0)
        {
            if (IsPositionOnScreen(placement.Left, placement.Top))
            {
                Position = new PixelPoint(placement.Left, placement.Top);
            }

            Width = placement.Width;
            Height = placement.Height;
        }
        else
        {
            Width = MainWindowDefaultShrunkWidth;
            Height = MainWindowDefaultShrunkHeight;
        }
    }

    private bool IsPositionOnScreen(int left, int top)
    {
        var screens = Screens?.All;
        if (screens == null || !screens.Any())
        {
            return left >= -32000 && top >= -32000 && left < 32000 && top < 32000;
        }

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

    private void SaveWindowPlacement()
    {
        if (_optionsService == null) return;

        var options = _optionsService.GetOptions();

        var placement = new WindowPlacement
        {
            Left = Position.X,
            Top = Position.Y,
            Width = (int)Width,
            Height = (int)Height,
            IsMaximized = WindowState == WindowState.Maximized
        };

        if (_isShrunk)
        {
            options.MainWindowPlacementShrunk = placement;
            options.UseShrunkPlacementAtStart = true;
        }
        else
        {
            options.MainWindowPlacement = placement;
            options.UseShrunkPlacementAtStart = false;
        }

        _optionsService.SaveOptions(options);
    }

    /// <summary>
    /// Expand from shrunk mode to normal mode
    /// </summary>
    public void ExpandFromShrink()
    {
        SaveWindowPlacement();
        RestoreNormalPlacement();
    }

    /// <summary>
    /// Shrink the window to compact mode
    /// </summary>
    public void ShrinkToCompact()
    {
        SaveWindowPlacement();
        RestoreShrunkPlacement();
    }
}
