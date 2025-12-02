using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using OnlyT.Core.Abstractions;

namespace OnlyT.Avalonia.Platform.MacOS;

public class MacOSMonitorService : IMonitorService
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
            ?? new MonitorInfo { MonitorId = "primary", MonitorName = "Built-in Display", FriendlyName = "Built-in Display", IsPrimary = true };
    }

    private IReadOnlyList<global::Avalonia.Platform.Screen> GetScreens()
    {
        var screens = new List<global::Avalonia.Platform.Screen>();

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

        return screens;
    }
}
