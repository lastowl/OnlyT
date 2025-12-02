using System;
using System.Runtime.InteropServices;
using System.Threading;
using OnlyT.Core.Abstractions;
using Serilog;

namespace OnlyT.Avalonia.Platform.Windows;

public class WindowsSystemIntegration : ISystemIntegration
{
    private Mutex? _appMutex;

    // Windows DWM API for dark mode title bar
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public void EnableDarkMode(object windowHandle, bool enable)
    {
        if (windowHandle is global::Avalonia.Controls.Window window)
        {
            // 1. Set Avalonia theme variant for content
            window.RequestedThemeVariant = enable
                ? global::Avalonia.Styling.ThemeVariant.Dark
                : global::Avalonia.Styling.ThemeVariant.Light;

            // 2. Apply native Windows title bar dark mode
            ApplyTitleBarDarkMode(window, enable);
        }
    }

    private void ApplyTitleBarDarkMode(global::Avalonia.Controls.Window window, bool enable)
    {
        try
        {
            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null)
            {
                Log.Debug("Window platform handle not available yet");
                return;
            }

            var hwnd = platformHandle.Handle;
            if (hwnd == IntPtr.Zero)
            {
                Log.Debug("Window handle is zero");
                return;
            }

            var darkModeValue = enable ? 1 : 0;
            var result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(int));

            if (result == 0)
            {
                Log.Debug("Applied {Theme} title bar theme to window", enable ? "dark" : "light");
            }
            else
            {
                Log.Debug("DwmSetWindowAttribute returned {Result}", result);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply Windows title bar dark mode");
        }
    }

    public object? GetSystemIcon(SystemIconType iconType)
    {
        // Return null for now - Avalonia handles icons differently
        // Could implement using System.Drawing.SystemIcons if needed
        return null;
    }

    public bool IsDarkModeEnabled()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Check Windows registry for dark mode setting
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                if (key != null)
                {
                    var value = key.GetValue("AppsUseLightTheme");
                    if (value is int intValue)
                    {
                        return intValue == 0; // 0 = dark mode, 1 = light mode
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to detect dark mode setting");
            }
        }

        return false;
    }

    public bool EnsureSingleInstance(string mutexName)
    {
        try
        {
            _appMutex = new Mutex(true, mutexName, out var createdNew);
            return createdNew;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create application mutex");
            return true; // Allow the app to run if mutex creation fails
        }
    }
}
