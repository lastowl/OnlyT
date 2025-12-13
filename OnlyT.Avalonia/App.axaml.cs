using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Material.Styles.Themes;
using Microsoft.Extensions.DependencyInjection;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.LogLevelSwitch;
using OnlyT.Avalonia.Services.NDI;
using OnlyT.Avalonia.Services.Report;
using OnlyT.Avalonia.Services.Localization;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Avalonia.Services.Reminders;
using OnlyT.Avalonia.Services.Overrun;
using OnlyT.Avalonia.Services.Snackbar;
using OnlyT.Avalonia.Services.TalkSchedule;
using OnlyT.Avalonia.Services.Timer;
using OnlyT.Avalonia.ViewModels;
using OnlyT.Avalonia.Views;
using OnlyT.Avalonia.WebServer;
using OnlyT.Common.Services.DateTime;
using OnlyT.Core.Abstractions;
using OnlyT.EventTracking;
using OnlyT.Utils;
using Sentry;
using Serilog;
using Serilog.Events;

namespace OnlyT.Avalonia;

public class App : Application
{
    private readonly string _appString = "OnlyTMeetingTimer";
    private IPlatformServices? _platformServices;

    public App()
    {
        //yesInitSentry();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                _platformServices = Program.GetPlatformServices();
                ConfigureServices();

                // Check if we should allow multiple instances
                var cmdArgs = Program.CommandLineArgs;
                if (!cmdArgs.NoMutex && AnotherInstanceRunning())
                {
                    desktop.Shutdown();
                    return;
                }

                ConfigureLogger();

                // Log command line args if any were provided
                if (cmdArgs.Port.HasValue || cmdArgs.AutoStart || cmdArgs.Silent ||
                    cmdArgs.NoMutex || cmdArgs.ForceDarkMode.HasValue || cmdArgs.IsTimerNdi)
                {
                    Log.Information("Command line args: Port={Port}, AutoStart={AutoStart}, Silent={Silent}, NoMutex={NoMutex}, ForceDarkMode={ForceDarkMode}, NDI={Ndi}",
                        cmdArgs.Port, cmdArgs.AutoStart, cmdArgs.Silent, cmdArgs.NoMutex, cmdArgs.ForceDarkMode, cmdArgs.IsTimerNdi);
                }

                // Initialize NDI if enabled
                if (cmdArgs.IsTimerNdi)
                {
                    InitializeNdi();
                }

                ApplyTheme();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = Ioc.Default.GetService<MainViewModel>()
                };

                // Close entire app when main window closes
                desktop.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnMainWindowClose;

                desktop.Exit += OnExit;
            }
            catch (Exception ex)
            {
                Log.Logger.Fatal(ex, "Fatal error during initialization");
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            // Stop NDI service
            var ndiService = Ioc.Default.GetService<INdiService>();
            ndiService?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error stopping NDI service");
        }

        try
        {
            // Stop HTTP server
            var httpServer = Ioc.Default.GetService<IHttpServer>();
            httpServer?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error stopping HTTP server");
        }

        if (Log.IsEnabled(LogEventLevel.Information))
        {
            Log.Logger.Information("==== Exit ====");
        }
    }

    private static void InitializeNdi()
    {
        try
        {
            var ndiService = Ioc.Default.GetService<INdiService>();
            if (ndiService != null)
            {
                ndiService.Initialize("OnlyT Timer");
                Log.Information("NDI initialized with source name 'OnlyT Timer'");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize NDI");
        }
    }

    private void ConfigureServices()
    {
        var serviceCollection = new ServiceCollection();

        // Platform services
        if (_platformServices != null)
        {
            serviceCollection.AddSingleton(_platformServices);
            serviceCollection.AddSingleton(_platformServices.MonitorService);
            serviceCollection.AddSingleton(_platformServices.WindowService);
            serviceCollection.AddSingleton(_platformServices.SystemIntegration);
        }

        // Core MVP services
        serviceCollection.AddSingleton<IOptionsService, SimpleOptionsService>();
        serviceCollection.AddSingleton<ITimerService, BasicTimerService>();
        serviceCollection.AddSingleton<ITalkTimerService, TalkTimerService>();
        serviceCollection.AddSingleton<IBellService, SimpleBellService>();
        serviceCollection.AddSingleton<IDateTimeService>(_ => new DateTimeService(null));
        serviceCollection.AddSingleton<IMeetingTimesService, MeetingTimesService>();
        serviceCollection.AddSingleton<ITalkScheduleService, TalkScheduleService>();
        serviceCollection.AddSingleton<IAdaptiveTimerService, AdaptiveTimerService>();
        serviceCollection.AddSingleton<IReminderService, ReminderService>();
        serviceCollection.AddSingleton<ILocalizationService, LocalizationService>();
        serviceCollection.AddSingleton<IQueryWeekendService, QueryWeekendService>();
        serviceCollection.AddSingleton<ILocalTimingDataStoreService, LocalTimingDataStoreService>();
        serviceCollection.AddSingleton<INdiService, NdiService>();

        // Web API
        serviceCollection.AddSingleton<IHttpServer, HttpServer>();
        serviceCollection.AddSingleton<IFirewallService, FirewallService>();

        // Notifications
        serviceCollection.AddSingleton<ISnackbarService, SnackbarService>();
        serviceCollection.AddSingleton<IOverrunService, OverrunService>();

        // Logging
        serviceCollection.AddSingleton<ILogLevelSwitchService, LogLevelSwitchService>();

        // ViewModels
        serviceCollection.AddSingleton<MainViewModel>();
        serviceCollection.AddSingleton<TimerOutputViewModel>();
        serviceCollection.AddSingleton<SettingsViewModel>();
        serviceCollection.AddSingleton<OperatorPageViewModel>();

        var serviceProvider = serviceCollection.BuildServiceProvider();
        Ioc.Default.ConfigureServices(serviceProvider);

        // Start HTTP server if enabled
        StartHttpServer(serviceProvider);

        // Apply culture setting
        ApplyCulture(serviceProvider);
    }

    private static void ApplyCulture(IServiceProvider serviceProvider)
    {
        try
        {
            var optionsService = serviceProvider.GetRequiredService<IOptionsService>();
            var localizationService = serviceProvider.GetRequiredService<ILocalizationService>();

            var culture = optionsService.Culture;
            if (!string.IsNullOrWhiteSpace(culture))
            {
                localizationService.SetCulture(culture);
                Log.Information("Applied culture: {Culture}", culture);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply culture setting");
        }
    }

    private static void StartHttpServer(IServiceProvider serviceProvider)
    {
        try
        {
            var optionsService = serviceProvider.GetRequiredService<IOptionsService>();
            if (optionsService.IsApiEnabled)
            {
                // Command line port overrides settings
                var port = Program.CommandLineArgs.Port ?? optionsService.HttpServerPort;

                // Configure firewall if needed
                ConfigureFirewall(serviceProvider, port);

                var httpServer = serviceProvider.GetRequiredService<IHttpServer>();
                httpServer.Start(port);
                Log.Information("HTTP server started on port {Port}", port);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to start HTTP server");
        }
    }

    private static void ConfigureFirewall(IServiceProvider serviceProvider, int port)
    {
        try
        {
            var firewallService = serviceProvider.GetRequiredService<IFirewallService>();

            if (!firewallService.IsFirewallConfigurationNeeded())
            {
                Log.Information("Firewall configuration not needed on this platform");
                return;
            }

            var result = firewallService.ConfigureFirewall(port);

            if (result.Success)
            {
                if (result.AlreadyConfigured)
                {
                    Log.Information("Firewall already configured for port {Port}", port);
                }
                else
                {
                    Log.Information("Firewall configured successfully for port {Port}: {Message}", port, result.Message);
                }
            }
            else if (result.RequiresElevation)
            {
                Log.Warning("Firewall configuration requires elevated privileges. Manual configuration may be needed.");
                Log.Information("Manual instructions:\n{Instructions}", firewallService.GetManualInstructions(port));
            }
            else
            {
                Log.Warning("Failed to configure firewall: {Message}", result.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during firewall configuration");
        }
    }

    private static void ConfigureLogger()
    {
        var logsDirectory = FileUtils.GetLogFolder();

        try
        {
            // Apply saved log level from options
            var optionsService = Ioc.Default.GetService<IOptionsService>();
            if (optionsService != null)
            {
                var savedLevel = optionsService.LogEventLevel;
                if (Enum.TryParse<LogEventLevel>(savedLevel, out var level))
                {
                    LogLevelSwitchService.LevelSwitch.MinimumLevel = level;
                }
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LogLevelSwitchService.LevelSwitch)
                .WriteTo.File(Path.Combine(logsDirectory, "log-.txt"), retainedFileCountLimit: 28, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            if (Log.IsEnabled(LogEventLevel.Information))
            {
                Log.Logger.Information("==== Launched ====");
                Log.Logger.Information("Version {Version}", VersionDetection.GetCurrentVersion());
                Log.Logger.Information("Log level: {Level}", LogLevelSwitchService.LevelSwitch.MinimumLevel);
            }
        }
        catch (Exception)
        {
            // logging won't work but silently fails
            // "no-op" logger
            Log.Logger = new LoggerConfiguration().CreateLogger();
        }
    }

    private bool AnotherInstanceRunning()
    {
        if (_platformServices == null)
        {
            return false;
        }

        return !_platformServices.SystemIntegration.EnsureSingleInstance(_appString);
    }

    private void ApplyTheme()
    {
        try
        {
            var optionsService = Ioc.Default.GetService<IOptionsService>();
            if (optionsService == null) return;

            // Command line can override dark mode setting
            var cmdArgs = Program.CommandLineArgs;
            var isDarkMode = cmdArgs.ForceDarkMode ?? optionsService.IsDarkMode;
            SetTheme(isDarkMode);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply theme");
        }
    }

    /// <summary>
    /// Public method to allow theme changes at runtime
    /// </summary>
    public void SetTheme(bool isDarkMode)
    {
        try
        {
            // 1. Set Material.Avalonia base theme
#pragma warning disable CS0618 // PaletteHelper is obsolete but still functional
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(isDarkMode ? Theme.Dark : Theme.Light);
            paletteHelper.SetTheme(theme);
#pragma warning restore CS0618

            // 2. Swap custom color resource dictionary
            SwapCustomColorDictionary(isDarkMode);

            // 3. Apply native title bar theming to all windows
            ApplyNativeTitleBarTheme(isDarkMode);

            Log.Information("Applied {Theme} theme", isDarkMode ? "dark" : "light");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to set theme");
        }
    }

    private void SwapCustomColorDictionary(bool isDarkMode)
    {
        try
        {
            var mergedDictionaries = Resources?.MergedDictionaries;
            if (mergedDictionaries == null) return;

            // Find and remove existing custom colors dictionary
            global::Avalonia.Controls.IResourceProvider? existingCustomColors = null;
            foreach (var dict in mergedDictionaries)
            {
                if (dict is global::Avalonia.Controls.ResourceDictionary rd &&
                    rd.MergedDictionaries.Count > 0)
                {
                    // Check for our custom colors in the merged dictionaries
                    foreach (var innerDict in rd.MergedDictionaries)
                    {
                        existingCustomColors = innerDict;
                        break;
                    }

                    if (existingCustomColors != null)
                    {
                        rd.MergedDictionaries.Clear();

                        // Add the appropriate custom colors
                        var customColorsUri = isDarkMode
                            ? new Uri("avares://OnlyT/Themes/CustomColoursDark.axaml")
                            : new Uri("avares://OnlyT/Themes/CustomColours.axaml");

                        var newColors = new global::Avalonia.Markup.Xaml.Styling.ResourceInclude(customColorsUri)
                        {
                            Source = customColorsUri
                        };
                        rd.MergedDictionaries.Add(newColors);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to swap custom color dictionary");
        }
    }

    private void ApplyNativeTitleBarTheme(bool isDarkMode)
    {
        try
        {
            if (_platformServices == null) return;

            // Apply to all open windows
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    _platformServices.SystemIntegration.EnableDarkMode(window, isDarkMode);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply native title bar theme");
        }
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private async void OnAboutClicked(object? sender, EventArgs e)
    {
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var version = VersionDetection.GetCurrentVersion();
                var aboutWindow = new Window
                {
                    Title = "About OnlyT",
                    Width = 400,
                    Height = 250,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    Content = new StackPanel
                    {
                        Margin = new global::Avalonia.Thickness(30),
                        Spacing = 15,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "OnlyT",
                                FontSize = 28,
                                FontWeight = global::Avalonia.Media.FontWeight.Bold,
                                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = "Meeting Timer Application",
                                FontSize = 14,
                                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = $"Version {version}",
                                FontSize = 12,
                                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                                Foreground = global::Avalonia.Media.Brushes.Gray
                            },
                            new TextBlock
                            {
                                Text = "© 2024 OnlyT",
                                FontSize = 12,
                                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                                Foreground = global::Avalonia.Media.Brushes.Gray
                            },
                            new TextBlock
                            {
                                Text = "https://github.com/lastowl/OnlyT",
                                FontSize = 11,
                                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                                Foreground = global::Avalonia.Media.Brushes.DodgerBlue
                            }
                        }
                    }
                };

                await aboutWindow.ShowDialog(desktop.MainWindow);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show About dialog");
        }
    }

    /// <summary>
    /// Apply native title bar theme to a specific window (call when window is shown)
    /// </summary>
    public void ApplyTitleBarThemeToWindow(global::Avalonia.Controls.Window window)
    {
        try
        {
            if (_platformServices == null) return;

            var optionsService = Ioc.Default.GetService<IOptionsService>();
            if (optionsService == null) return;

            var cmdArgs = Program.CommandLineArgs;
            var isDarkMode = cmdArgs.ForceDarkMode ?? optionsService.IsDarkMode;

            _platformServices.SystemIntegration.EnableDarkMode(window, isDarkMode);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply title bar theme to window");
        }
    }

    private static void InitSentry()
    {
        SentrySdk.Init(o =>
        {
            o.Dsn = "https://a507dcd971e89dc9b69a630090030ba7@o4509644339281920.ingest.de.sentry.io/4509753015926864";

#if DEBUG
            o.Debug = true;
#endif
            o.IsGlobalModeEnabled = true;
        });
    }
}
