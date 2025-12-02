using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using OnlyT.Avalonia.Services.Snackbar;

namespace OnlyT.Avalonia.Controls;

public partial class SnackbarControl : UserControl
{
    private Border? _snackbarBorder;
    private TextBlock? _messageText;
    private Button? _actionButton;
    private CancellationTokenSource? _hideCts;
    private Action? _currentActionHandler;

    public SnackbarControl()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _snackbarBorder = this.FindControl<Border>("SnackbarBorder");
        _messageText = this.FindControl<TextBlock>("MessageText");
        _actionButton = this.FindControl<Button>("ActionButton");

        // Subscribe to snackbar service if available
        var snackbarService = Ioc.Default.GetService<ISnackbarService>();
        if (snackbarService != null)
        {
            snackbarService.MessageEnqueued += OnMessageEnqueued;
        }
    }

    private void OnMessageEnqueued(object? sender, SnackbarMessageEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ShowMessage(e.Message, e.ActionText, e.ActionHandler, e.Duration));
    }

    private async void ShowMessage(string message, string? actionText, Action? actionHandler, TimeSpan duration)
    {
        if (_snackbarBorder == null || _messageText == null || _actionButton == null)
            return;

        // Cancel any existing hide operation
        _hideCts?.Cancel();
        _hideCts = new CancellationTokenSource();
        var token = _hideCts.Token;

        // Store the action handler
        _currentActionHandler = actionHandler;

        // Update UI
        _messageText.Text = message;

        if (!string.IsNullOrEmpty(actionText))
        {
            _actionButton.Content = actionText;
            _actionButton.IsVisible = true;
        }
        else
        {
            _actionButton.IsVisible = false;
        }

        // Show the snackbar
        _snackbarBorder.Opacity = 0;
        _snackbarBorder.IsVisible = true;
        _snackbarBorder.Opacity = 1;

        try
        {
            // Wait for the duration
            await Task.Delay(duration, token);

            // Hide the snackbar
            if (!token.IsCancellationRequested)
            {
                await HideSnackbar();
            }
        }
        catch (TaskCanceledException)
        {
            // Cancelled, new message will be shown
        }
    }

    private async Task HideSnackbar()
    {
        if (_snackbarBorder == null)
            return;

        _snackbarBorder.Opacity = 0;
        await Task.Delay(300); // Wait for fade animation
        _snackbarBorder.IsVisible = false;
        _currentActionHandler = null;
    }

    private async void OnActionClick(object? sender, RoutedEventArgs e)
    {
        _currentActionHandler?.Invoke();
        _hideCts?.Cancel();
        await HideSnackbar();
    }
}
