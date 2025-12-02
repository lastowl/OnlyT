using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OnlyT.Utils;

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

    public static string GetOptionsFilePath()
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

    public static string GetTimingReportsFolder()
    {
        var folder = Path.Combine(GetDocumentsFolder(), "TimingReports");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string GetTimingReportsDatabaseFolder()
    {
        var folder = Path.Combine(GetUserAppDataFolder(), "TimingReportsDatabase");
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
