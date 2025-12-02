using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using OnlyT.Core.Abstractions;
using OnlyT.Utils;
using Serilog;

namespace OnlyT.Avalonia.Platform.MacOS;

public class MacOSWindowService : IWindowService
{
    private readonly Dictionary<string, Core.Abstractions.WindowPlacement> _savedPlacements = new();
    private readonly string _placementStoragePath;

    public MacOSWindowService()
    {
        // Use Application Support folder on macOS
        var appSupport = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appSupport, "OnlyT");
        Directory.CreateDirectory(appFolder);
        _placementStoragePath = Path.Combine(appFolder, "WindowPlacements.json");
        LoadPlacements();
    }

    public void SaveWindowPlacement(string windowId, Core.Abstractions.WindowPlacement placement)
    {
        _savedPlacements[windowId] = placement;
        PersistPlacements();
    }

    public Core.Abstractions.WindowPlacement? RestoreWindowPlacement(string windowId)
    {
        return _savedPlacements.TryGetValue(windowId, out var placement) ? placement : null;
    }

    public void SetAlwaysOnTop(object windowHandle, bool alwaysOnTop)
    {
        if (windowHandle is global::Avalonia.Controls.Window window)
        {
            window.Topmost = alwaysOnTop;
        }
    }

    public void HideCloseButton(object windowHandle)
    {
        // On macOS, we can't easily hide the close button without native code
        // We'll handle it by preventing the window from closing
        if (windowHandle is global::Avalonia.Controls.Window window)
        {
            window.Closing += (s, e) =>
            {
                e.Cancel = true;
            };
        }
    }

    private void LoadPlacements()
    {
        try
        {
            if (File.Exists(_placementStoragePath))
            {
                var json = File.ReadAllText(_placementStoragePath);
                var placements = JsonConvert.DeserializeObject<Dictionary<string, Core.Abstractions.WindowPlacement>>(json);
                if (placements != null)
                {
                    foreach (var kvp in placements)
                    {
                        _savedPlacements[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load window placements");
        }
    }

    private void PersistPlacements()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_savedPlacements, Formatting.Indented);
            File.WriteAllText(_placementStoragePath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save window placements");
        }
    }
}
