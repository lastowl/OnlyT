using Avalonia.Threading;
using Moq;
using OnlyT.Avalonia.EventArgsTypes;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.Timer;
using OnlyT.Avalonia.Tests.Headless;
using OnlyT.Avalonia.ViewModels;

namespace OnlyT.Avalonia.Tests.ViewModels;

/// <summary>
/// Headless tests for the count-up display on the timer output view model:
/// the target duration shown under the timer, a duration changed mid-talk,
/// and a pause that lands on a new second.
/// </summary>
[TestClass]
public class TimerOutputViewModelCountUpTests
{
    private static Mock<IOptionsService> OptionsMock(bool showTarget = true)
    {
        var options = new Mock<IOptionsService>();
        options.SetupGet(o => o.ShowCountUpTarget).Returns(showTarget);
        options.SetupGet(o => o.PersistStudentTime).Returns(true);
        options.SetupGet(o => o.PersistDurationSecs).Returns(90);
        options.Setup(o => o.GetOptions()).Returns(new AppOptions());
        return options;
    }

    private static void Start(Mock<ITalkTimerService> timer, int targetSecs, bool countUp) =>
        timer.Raise(t => t.TimerStartedEvent += null, timer.Object,
            new TimerStartedEventArgs { TargetSecs = targetSecs, IsCountingUp = countUp });

    private static void Tick(Mock<ITalkTimerService> timer, int targetSecs, int elapsedSecs,
        bool countUp, bool isRunning = true, bool persist = false) =>
        timer.Raise(t => t.TimerChangedEvent += null, timer.Object,
            new TimerChangedEventArgs
            {
                TargetSecs = targetSecs,
                ElapsedSecs = elapsedSecs,
                IsRunning = isRunning,
                IsCountingUp = countUp,
                PersistFinalTimerValue = persist,
            });

    [TestMethod]
    public void CountingUp_ShowsTargetUnderTimer() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock().Object);

        Start(timer, 600, countUp: true);
        Tick(timer, 600, 65, countUp: true);
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual("01:05", vm.TimeDisplay);
        Assert.AreEqual("10:00", vm.CountUpTargetDisplay);
        Assert.IsTrue(vm.IsCountUpTargetVisible);
    });

    [TestMethod]
    public void CountingDown_NoTarget() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock().Object);

        Start(timer, 600, countUp: false);
        Tick(timer, 600, 65, countUp: false);
        Dispatcher.UIThread.RunJobs();

        Assert.IsNull(vm.CountUpTargetDisplay);
        Assert.IsFalse(vm.IsCountUpTargetVisible);
    });

    [TestMethod]
    public void OptionOff_NoTarget() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock(showTarget: false).Object);

        Start(timer, 600, countUp: true);
        Tick(timer, 600, 65, countUp: true);
        Dispatcher.UIThread.RunJobs();

        Assert.IsFalse(vm.IsCountUpTargetVisible);
    });

    [TestMethod]
    public void TargetChangedMidTalk_KeepsElapsedAndUpdatesTarget() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock().Object);

        Start(timer, 600, countUp: true);
        Tick(timer, 600, 120, countUp: true);
        // operator adds 5 minutes while the talk runs
        Tick(timer, 900, 121, countUp: true);
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual("02:01", vm.TimeDisplay, "Elapsed time must not jump when the target changes");
        Assert.AreEqual("15:00", vm.CountUpTargetDisplay);
    });

    [TestMethod]
    public void PauseOnNewSecond_KeepsTimerShown() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock().Object);
        Start(timer, 600, countUp: true);
        Tick(timer, 600, 120, countUp: true, persist: true);
        Dispatcher.UIThread.RunJobs();

        // the pause's final update crosses a second boundary: IsRunning is false
        timer.SetupGet(t => t.IsPaused).Returns(true);
        Tick(timer, 600, 121, countUp: true, isRunning: false, persist: true);
        Dispatcher.UIThread.RunJobs();

        Assert.IsFalse(vm.IsShowingClock, "A pause is not a stop");
        Assert.IsFalse(vm.ShowPersistBar, "A pause must not start the persist countdown");
        Assert.AreEqual("02:01", vm.TimeDisplay);
        Assert.IsTrue(vm.IsCountUpTargetVisible);
    });

    [TestMethod]
    public void Stop_HidesTarget() => HeadlessSession.Run(() =>
    {
        var timer = new Mock<ITalkTimerService>();
        var vm = new TimerOutputViewModel(timer.Object, OptionsMock().Object);
        Start(timer, 600, countUp: true);
        Tick(timer, 600, 65, countUp: true);

        Tick(timer, 600, 0, countUp: true, isRunning: false);
        Dispatcher.UIThread.RunJobs();

        Assert.IsTrue(vm.IsShowingClock);
        Assert.IsFalse(vm.IsCountUpTargetVisible);
    });
}
