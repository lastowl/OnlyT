using System;
using Avalonia.Threading;

namespace OnlyT.Avalonia.Services.Snackbar;

/// <summary>
/// Service for displaying snackbar/toast notifications
/// </summary>
public sealed class SnackbarService : ISnackbarService
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);

    public event EventHandler<SnackbarMessageEventArgs>? MessageEnqueued;

    public void Enqueue(string message)
    {
        Enqueue(message, null, null, DefaultDuration);
    }

    public void Enqueue(string message, string actionText, Action actionHandler)
    {
        Enqueue(message, actionText, actionHandler, DefaultDuration);
    }

    public void Enqueue(string message, TimeSpan duration)
    {
        Enqueue(message, null, null, duration);
    }

    public void Enqueue(string message, string? actionText, Action? actionHandler, TimeSpan duration)
    {
        Dispatcher.UIThread.Post(() =>
        {
            MessageEnqueued?.Invoke(this, new SnackbarMessageEventArgs(message, actionText, actionHandler, duration));
        });
    }

    public void EnqueueWithOk(string message)
    {
        Enqueue(message, "OK", () => { }, DefaultDuration);
    }
}
