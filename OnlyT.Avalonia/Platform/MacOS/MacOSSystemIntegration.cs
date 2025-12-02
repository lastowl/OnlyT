using System;
using System.IO;
using System.Runtime.InteropServices;
using OnlyT.Core.Abstractions;
using Serilog;

namespace OnlyT.Avalonia.Platform.MacOS;

public class MacOSSystemIntegration : ISystemIntegration
{
    private string? _lockFilePath;

    // Objective-C runtime bindings for macOS title bar theming
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string className);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void(IntPtr receiver, IntPtr selector, IntPtr arg);

    public void EnableDarkMode(object windowHandle, bool enable)
    {
        if (windowHandle is global::Avalonia.Controls.Window window)
        {
            // 1. Set Avalonia theme variant for content
            window.RequestedThemeVariant = enable
                ? global::Avalonia.Styling.ThemeVariant.Dark
                : global::Avalonia.Styling.ThemeVariant.Light;

            // 2. Apply native macOS title bar appearance
            ApplyTitleBarAppearance(window, enable);
        }
    }

    private void ApplyTitleBarAppearance(global::Avalonia.Controls.Window window, bool isDarkMode)
    {
        try
        {
            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null)
            {
                Log.Debug("Window platform handle not available yet for macOS theming");
                return;
            }

            var nsWindow = platformHandle.Handle;
            if (nsWindow == IntPtr.Zero)
            {
                Log.Debug("NSWindow handle is zero");
                return;
            }

            // Get NSAppearance class
            var nsAppearanceClass = objc_getClass("NSAppearance");
            if (nsAppearanceClass == IntPtr.Zero)
            {
                Log.Debug("Failed to get NSAppearance class");
                return;
            }

            // Create appearance name string
            var appearanceName = isDarkMode ? "NSAppearanceNameDarkAqua" : "NSAppearanceNameAqua";

            // Get NSString class and create the appearance name string
            var nsStringClass = objc_getClass("NSString");
            var stringWithUTF8StringSel = sel_registerName("stringWithUTF8String:");
            var appearanceNamePtr = Marshal.StringToCoTaskMemAnsi(appearanceName);

            try
            {
                var nsAppearanceName = objc_msgSend(nsStringClass, stringWithUTF8StringSel, appearanceNamePtr);

                // Get appearance object using appearanceNamed:
                var appearanceNamedSel = sel_registerName("appearanceNamed:");
                var appearance = objc_msgSend(nsAppearanceClass, appearanceNamedSel, nsAppearanceName);

                if (appearance != IntPtr.Zero)
                {
                    // Set the window's appearance
                    var setAppearanceSel = sel_registerName("setAppearance:");
                    objc_msgSend_void(nsWindow, setAppearanceSel, appearance);
                    Log.Debug("Applied {Theme} appearance to macOS window", isDarkMode ? "dark" : "light");
                }
                else
                {
                    Log.Debug("Failed to create NSAppearance for {Theme}", appearanceName);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(appearanceNamePtr);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply macOS title bar appearance");
        }
    }

    public object? GetSystemIcon(SystemIconType iconType)
    {
        // macOS uses SF Symbols or system icons
        // Return null for now - Avalonia handles icons cross-platform
        return null;
    }

    public bool IsDarkModeEnabled()
    {
        try
        {
            // On macOS, we could check the system appearance using:
            // defaults read -g AppleInterfaceStyle
            // For now, return false as default
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "defaults",
                Arguments = "read -g AppleInterfaceStyle",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Trim().Equals("Dark", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to detect dark mode on macOS");
        }

        return false;
    }

    public bool EnsureSingleInstance(string mutexName)
    {
        try
        {
            // On macOS, use a lock file instead of mutex
            var tempPath = Path.GetTempPath();
            _lockFilePath = Path.Combine(tempPath, $"{mutexName}.lock");

            if (File.Exists(_lockFilePath))
            {
                // Check if the process is still running
                try
                {
                    var pidString = File.ReadAllText(_lockFilePath);
                    if (int.TryParse(pidString, out var pid))
                    {
                        var process = System.Diagnostics.Process.GetProcessById(pid);
                        if (process != null && !process.HasExited)
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
            Log.Warning(ex, "Failed to create lock file on macOS");
            return true; // Allow the app to run if lock file creation fails
        }
    }
}
