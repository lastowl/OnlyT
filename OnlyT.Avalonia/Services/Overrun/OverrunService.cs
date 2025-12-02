using System;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Avalonia.Services.Snackbar;
using Serilog;

namespace OnlyT.Avalonia.Services.Overrun;

/// <summary>
/// Service for handling overrun/underrun notifications
/// Uses the snackbar service to display timing variance notifications
/// </summary>
public class OverrunService : IOverrunService
{
    private readonly IOptionsService _optionsService;
    private readonly ISnackbarService _snackbarService;

    public OverrunService(IOptionsService optionsService, ISnackbarService snackbarService)
    {
        _optionsService = optionsService;
        _snackbarService = snackbarService;
    }

    public void NotifyOfBadTiming(TimeSpan variance)
    {
        var options = _optionsService.GetOptions();
        if (!options.OverrunNotifications)
        {
            return;
        }

        var absMins = Math.Abs(Math.Round(variance.TotalMinutes));
        if (absMins < 1)
        {
            // Don't notify for less than 1 minute variance
            return;
        }

        var isOverrun = variance < TimeSpan.Zero;

        var msg = isOverrun
            ? $"Talk overran by {(int)absMins} minute{(absMins > 1 ? "s" : "")}"
            : $"Talk finished {(int)absMins} minute{(absMins > 1 ? "s" : "")} early";

        Log.Information("Timing notification: {Message} (variance: {Variance})", msg, variance);

        // Display via snackbar with longer duration for visibility
        _snackbarService.Enqueue(msg, TimeSpan.FromSeconds(8));
    }

    public void Shutdown()
    {
        // No special cleanup needed
    }
}
