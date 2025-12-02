using System;
using System.Runtime.InteropServices;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using OnlyT.Avalonia.Services;
using OnlyT.Core.Abstractions;

namespace OnlyT.Avalonia;

internal class Program
{
    /// <summary>
    /// Parsed command line arguments (available to App)
    /// </summary>
    public static CommandLineArgs CommandLineArgs { get; private set; } = new();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Parse command line arguments
            CommandLineArgs = CommandLineArgs.Parse(args);

            // Handle help request
            if (CommandLineArgs.ShowHelp)
            {
                Console.WriteLine(CommandLineArgs.GetHelpText());
                return;
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex}");
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    /// <summary>
    /// Get the platform services based on the current OS
    /// </summary>
    public static IPlatformServices GetPlatformServices()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new Platform.Windows.WindowsPlatformServices();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new Platform.MacOS.MacOSPlatformServices();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new Platform.Linux.LinuxPlatformServices();
        }
        else
        {
            throw new PlatformNotSupportedException("Current platform is not supported");
        }
    }
}
