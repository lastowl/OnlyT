using Avalonia.Threading;
using Moq;
using OnlyT.Avalonia.EventArgsTypes;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.Timer;
using OnlyT.Avalonia.Tests.Headless;
using OnlyT.Avalonia.ViewModels;

namespace OnlyT.Avalonia.Tests.ViewModels;

/// <summary>
/// Behavioural (headless) tests for the persist-after-stop transition on the
/// timer output view model. They drive the timer service's events and pump the
/// Avalonia dispatcher; the view model marshals its handlers onto the UI thread.
///
/// Note: these cover the enter-persist transition and its gating. The eventual
/// revert to the clock is driven by a DispatcherTimer over wall-clock time and
/// is left to manual/e2e verification.
/// </summary>
[TestClass]
public class TimerOutputViewModelPersistTests
{
    private static Mock<IOptionsService> OptionsMock(bool persist = true, bool showBar = true)
    {
        var options = new Mock<IOptionsService>();
        options.SetupGet(o => o.PersistStudentTime).Returns(persist);
        options.SetupGet(o => o.ShowPersistCountdown).Returns(showBar);
        options.SetupGet(o => o.PersistDurationSecs).Returns(90);
        // RefreshSettings/ApplyClockMode reach through to the backing AppOptions.
        options.Setup(o => o.GetOptions()).Returns(new AppOptions());
        return options;
    }

    private static void RaiseStop(Mock<ITalkTimerService> timer, bool persist) =>
        timer.Raise(t => t.TimerChangedEvent += null, timer.Object,
            new TimerChangedEventArgs { IsRunning = false, PersistFinalTimerValue = persist });

    [TestMethod]
    public void Stop_PersistTalk_ShowsBarAndHoldsValue() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock().Object);
        Dispatcher.UIThread.RunJobs();

        RaiseStop(timer, persist: true);
        Dispatcher.UIThread.RunJobs();

        Assert.IsTrue(vm.ShowPersistBar, "Persist bar should appear after a student talk stops");
        Assert.IsFalse(vm.IsShowingClock, "Display should hold the final value, not revert to the clock");
    });

    [TestMethod]
    public void Stop_NonPersistTalk_RevertsToClockNoBar() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock().Object);
        Dispatcher.UIThread.RunJobs();

        RaiseStop(timer, persist: false);
        Dispatcher.UIThread.RunJobs();

        Assert.IsFalse(vm.ShowPersistBar);
        Assert.IsTrue(vm.IsShowingClock, "Non-persist talk should revert to the clock immediately");
    });

    [TestMethod]
    public void Stop_PersistTalk_PersistDisabled_DoesNotPersist() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock(persist: false).Object);
        Dispatcher.UIThread.RunJobs();

        RaiseStop(timer, persist: true);
        Dispatcher.UIThread.RunJobs();

        Assert.IsFalse(vm.ShowPersistBar);
        Assert.IsTrue(vm.IsShowingClock, "With the option off, even a persist talk reverts immediately");
    });

    [TestMethod]
    public void Stop_PersistButBarDisabled_HoldsValueWithoutBar() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock(showBar: false).Object);
        Dispatcher.UIThread.RunJobs();

        RaiseStop(timer, persist: true);
        Dispatcher.UIThread.RunJobs();

        Assert.IsFalse(vm.ShowPersistBar, "Bar is suppressed when ShowPersistCountdown is off");
        Assert.IsFalse(vm.IsShowingClock, "But the final value is still held on screen");
    });

    [TestMethod]
    public void NewTimerStart_DuringPersist_CancelsPersistBar() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock().Object);
        Dispatcher.UIThread.RunJobs();
        RaiseStop(timer, persist: true);
        Dispatcher.UIThread.RunJobs();
        Assert.IsTrue(vm.ShowPersistBar);

        timer.Raise(t => t.TimerStartedEvent += null, timer.Object,
            new TimerStartedEventArgs { TargetSecs = 300 });
        Dispatcher.UIThread.RunJobs();

        Assert.IsFalse(vm.ShowPersistBar, "Starting a new talk should clear the persist bar");
    });
}
