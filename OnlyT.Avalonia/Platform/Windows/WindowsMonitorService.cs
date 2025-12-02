using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform;
using OnlyT.Core.Abstractions;

namespace OnlyT.Avalonia.Platform.Windows;

public class WindowsMonitorService : IMonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var screens = GetScreens();
        var monitors = new List<MonitorInfo>();

        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            monitors.Add(new MonitorInfo
            {
                MonitorId = screen.DisplayName ?? $"Display_{monitors.Count}",
                MonitorName = screen.DisplayName ?? $"Display {monitors.Count + 1}",
                FriendlyName = screen.DisplayName ?? $"Display {monitors.Count + 1}",
                IsPrimary = screen.IsPrimary,
                Left = bounds.X,
                Top = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height
            });
        }

        return monitors;
    }

    public MonitorInfo GetPrimaryMonitor()
    {
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m => m.IsPrimary)
            ?? monitors.FirstOrDefault()
            ?? new MonitorInfo { MonitorId = "primary", MonitorName = "Primary", FriendlyName = "Primary Display", IsPrimary = true };
    }

    private IReadOnlyList<global::Avalonia.Platform.Screen> GetScreens()
    {
        // Avalonia's screen detection
        var screens = new List<global::Avalonia.Platform.Screen>();

        // Get all available screens from the platform
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow != null)
            {
                var platformScreen = mainWindow.Screens;
                if (platformScreen != null)
                {
                    screens.AddRange(platformScreen.All);
                }
            }
        }

        // If no screens detected, return empty list
        // The application should handle this case gracefully
        return screens;
    }
}
