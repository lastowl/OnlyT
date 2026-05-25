using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;

namespace OnlyT.Avalonia.Tests.Headless;

/// <summary>
/// Minimal Avalonia application used purely to spin up the headless platform
/// for UI/view-model tests (provides ICursorFactory, the dispatcher, etc.).
/// Deliberately does NOT use the real OnlyT App, so tests don't pull in its
/// DI container or open windows.
/// </summary>
public sealed class HeadlessTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>
/// Shared headless session. Test bodies that touch Avalonia controls or
/// view models (which marshal work onto the UI thread) run via <see cref="Run"/>
/// so they execute on the session's dispatcher thread.
/// </summary>
public static class HeadlessSession
{
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApp));

    public static void Run(Action body) =>
        Session.Dispatch(() =>
        {
            body();
            return Task.CompletedTask;
        }, CancellationToken.None).GetAwaiter().GetResult();
}
