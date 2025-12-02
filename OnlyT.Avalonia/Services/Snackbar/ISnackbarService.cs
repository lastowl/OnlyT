using System;

namespace OnlyT.Avalonia.Services.Snackbar;

/// <summary>
/// Service interface for displaying snackbar/toast notifications
/// </summary>
public interface ISnackbarService
{
    /// <summary>
    /// Event raised when a new snackbar message is enqueued
    /// </summary>
    event EventHandler<SnackbarMessageEventArgs>? MessageEnqueued;

    /// <summary>
    /// Display a simple message
    /// </summary>
    void Enqueue(string message);

    /// <summary>
    /// Display a message with an action button
    /// </summary>
    void Enqueue(string message, string actionText, Action actionHandler);

    /// <summary>
    /// Display a message with custom duration
    /// </summary>
    void Enqueue(string message, TimeSpan duration);

    /// <summary>
    /// Display a message with an action button and custom duration
    /// </summary>
    void Enqueue(string message, string actionText, Action actionHandler, TimeSpan duration);

    /// <summary>
    /// Display a message with an "OK" action button
    /// </summary>
    void EnqueueWithOk(string message);
}

/// <summary>
/// Event args for snackbar messages
/// </summary>
public class SnackbarMessageEventArgs : EventArgs
{
    public string Message { get; }
    public string? ActionText { get; }
    public Action? ActionHandler { get; }
    public TimeSpan Duration { get; }

    public SnackbarMessageEventArgs(string message, string? actionText, Action? actionHandler, TimeSpan duration)
    {
        Message = message;
        ActionText = actionText;
        ActionHandler = actionHandler;
        Duration = duration;
    }
}
