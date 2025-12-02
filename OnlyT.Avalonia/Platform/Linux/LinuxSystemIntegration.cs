using System;
using System.IO;
using OnlyT.Core.Abstractions;
using Serilog;

namespace OnlyT.Avalonia.Platform.Linux;

public class LinuxSystemIntegration : ISystemIntegration
{
    private string? _lockFilePath;

    public void EnableDarkMode(object windowHandle, bool enable)
    {
        if (windowHandle is global::Avalonia.Controls.Window window)
        {
            // Set Avalonia theme variant for content theming
            // Note: Linux has no standardized API for title bar theming.
            // The title bar appearance depends on the window manager/desktop environment.
            // Content theming via RequestedThemeVariant is the best cross-platform approach.
            window.RequestedThemeVariant = enable
                ? global::Avalonia.Styling.ThemeVariant.Dark
                : global::Avalonia.Styling.ThemeVariant.Light;

            Log.Debug("Applied {Theme} theme to Linux window content", enable ? "dark" : "light");
        }
    }

    public object? GetSystemIcon(SystemIconType iconType)
    {
        // Linux icon handling depends on the desktop environment
        // Return null for now - Avalonia handles icons cross-platform
        return null;
    }

    public bool IsDarkModeEnabled()
    {
        try
        {
            // Check GTK theme for dark mode (common on many Linux desktops)
            var gtkTheme = Environment.GetEnvironmentVariable("GTK_THEME");
            if (!string.IsNullOrEmpty(gtkTheme) && gtkTheme.Contains("dark", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check GNOME settings using gsettings
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "gsettings",
                Arguments = "get org.gnome.desktop.interface gtk-theme",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Contains("dark", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to detect dark mode on Linux");
        }

        return false;
    }

    public bool EnsureSingleInstance(string mutexName)
    {
        try
        {
            // Use XDG_RUNTIME_DIR if available, otherwise use /tmp
            var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (string.IsNullOrEmpty(runtimeDir))
            {
                runtimeDir = "/tmp";
            }

            _lockFilePath = Path.Combine(runtimeDir, $"{mutexName}.lock");

            if (File.Exists(_lockFilePath))
            {
                // Check if the process is still running
                try
                {
                    var pidString = File.ReadAllText(_lockFilePath);
                    if (int.TryParse(pidString, out var pid))
                    {
                        // Check if process exists
                        var processExists = System.Diagnostics.Process.GetProcesses()
                            .Any(p => p.Id == pid);

                        if (processExists)
                        {
                            return false; // Another instance is running
                        }
                    }
                }
                catch
                {
                    // Process not found, remove stale lock file
                    File.Delete(_lockFilePath);
                }
            }

            // Create lock file with current process ID
            File.WriteAllText(_lockFilePath, System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create lock file on Linux");
            return true; // Allow the app to run if lock file creation fails
        }
    }
}
