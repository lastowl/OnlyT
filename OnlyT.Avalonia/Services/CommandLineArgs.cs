using System;
using System.Collections.Generic;

namespace OnlyT.Avalonia.Services;

/// <summary>
/// Command line arguments for OnlyT
/// </summary>
public class CommandLineArgs
{
    /// <summary>
    /// Override HTTP server port (0 = use settings)
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Allow multiple instances to run
    /// </summary>
    public bool NoMutex { get; set; }

    /// <summary>
    /// Start timer automatically on launch
    /// </summary>
    public bool AutoStart { get; set; }

    /// <summary>
    /// Start minimized / in system tray
    /// </summary>
    public bool Silent { get; set; }

    /// <summary>
    /// Don't persist settings changes
    /// </summary>
    public bool NoSettingsPersistence { get; set; }

    /// <summary>
    /// Override dark mode setting
    /// </summary>
    public bool? ForceDarkMode { get; set; }

    /// <summary>
    /// Show help message
    /// </summary>
    public bool ShowHelp { get; set; }

    /// <summary>
    /// Enable NDI output for timer display
    /// </summary>
    public bool IsTimerNdi { get; set; }

    /// <summary>
    /// NDI output width (default 1920)
    /// </summary>
    public int NdiWidth { get; set; } = 1920;

    /// <summary>
    /// NDI output height (default 1080)
    /// </summary>
    public int NdiHeight { get; set; } = 1080;

    /// <summary>
    /// NDI frame rate (default 30)
    /// </summary>
    public int NdiFrameRate { get; set; } = 30;

    /// <summary>
    /// Parse command line arguments
    /// </summary>
    public static CommandLineArgs Parse(string[] args)
    {
        var result = new CommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "--port":
                case "-p":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var port))
                    {
                        result.Port = port;
                        i++; // Skip the next argument
                    }
                    break;

                case "--nomutex":
                case "--no-mutex":
                    result.NoMutex = true;
                    break;

                case "--autostart":
                case "--auto-start":
                case "-a":
                    result.AutoStart = true;
                    break;

                case "--silent":
                case "-s":
                    result.Silent = true;
                    break;

                case "--nosettings":
                case "--no-settings":
                    result.NoSettingsPersistence = true;
                    break;

                case "--dark":
                    result.ForceDarkMode = true;
                    break;

                case "--light":
                    result.ForceDarkMode = false;
                    break;

                case "--help":
                case "-h":
                case "-?":
                    result.ShowHelp = true;
                    break;

                case "--ndi":
                case "--timer-ndi":
                    result.IsTimerNdi = true;
                    break;

                case "--ndi-width":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var ndiWidth))
                    {
                        result.NdiWidth = ndiWidth;
                        i++;
                    }
                    break;

                case "--ndi-height":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var ndiHeight))
                    {
                        result.NdiHeight = ndiHeight;
                        i++;
                    }
                    break;

                case "--ndi-fps":
                case "--ndi-framerate":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var ndiFps))
                    {
                        result.NdiFrameRate = ndiFps;
                        i++;
                    }
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Get help text for command line arguments
    /// </summary>
    public static string GetHelpText()
    {
        return @"OnlyT Meeting Timer - Command Line Arguments

Usage: OnlyT [options]

Options:
  --port, -p <num>     Set HTTP server port (default: 8096)
  --nomutex            Allow multiple instances to run
  --autostart, -a      Auto-start timer on launch
  --silent, -s         Start minimized
  --nosettings         Don't persist settings changes
  --dark               Force dark mode
  --light              Force light mode
  --ndi                Enable NDI output for timer display
  --ndi-width <num>    NDI output width (default: 1920)
  --ndi-height <num>   NDI output height (default: 1080)
  --ndi-fps <num>      NDI frame rate (default: 30)
  --help, -h           Show this help message

Examples:
  OnlyT --port 9000
  OnlyT --dark --autostart
  OnlyT --nomutex --port 8097
  OnlyT --ndi --ndi-width 1280 --ndi-height 720";
    }
}

