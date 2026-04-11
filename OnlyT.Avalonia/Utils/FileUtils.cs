using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OnlyT.Avalonia.Utils;

/// <summary>
/// File utilities for cross-platform file paths
/// </summary>
public static class FileUtils
{
    private static string? _overrideDocumentFolder;

    public static void OverrideDocumentFolder(string? folder)
    {
        _overrideDocumentFolder = folder;
    }

    public static string GetUserAppDataFolder()
    {
        if (!string.IsNullOrEmpty(_overrideDocumentFolder))
        {
            return _overrideDocumentFolder;
        }

        string folder;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OnlyT");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "OnlyT");
        }
        else // Linux
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configHome))
            {
                configHome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config");
            }
            folder = Path.Combine(configHome, "OnlyT");
        }

        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string GetLogFolder()
    {
        var folder = Path.Combine(GetUserAppDataFolder(), "Logs");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// Path to the Avalonia fork's options file. Separate from the upstream
    /// WPF file (see <see cref="GetWpfOptionsFilePath"/>) so the two versions
    /// can evolve independently and fork-only fields never confuse WPF.
    /// </summary>
    public static string GetOptionsFilePath()
    {
        return Path.Combine(GetUserAppDataFolder(), "options.avalonia.json");
    }

    /// <summary>
    /// Path to the upstream WPF OnlyT options file. Used only to seed the
    /// fork's file on first launch — never written to.
    /// </summary>
    public static string GetWpfOptionsFilePath()
    {
        return Path.Combine(GetUserAppDataFolder(), "options.json");
    }

    public static string GetTimesFeedPath()
    {
        return Path.Combine(GetUserAppDataFolder(), "meeting-times-feed.json");
    }

    public static string GetTalkSchedulePath()
    {
        return Path.Combine(GetDocumentsFolder(), "talk_schedule.xml");
    }

    public static string GetTimingReportsFolder(string? identifier = null)
    {
        var folder = Path.Combine(GetDocumentsFolder(), "TimingReports", identifier ?? string.Empty);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string GetTimingReportsDatabaseFolder(string? identifier = null)
    {
        var folder = Path.Combine(GetUserAppDataFolder(), "TimingReportsDatabase", identifier ?? string.Empty);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string GetDocumentsFolder()
    {
        string folder;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OnlyT");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents",
                "OnlyT");
        }
        else // Linux
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents",
                "OnlyT");
        }

        Directory.CreateDirectory(folder);
        return folder;
    }
}
