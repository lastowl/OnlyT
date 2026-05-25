using OnlyT.Avalonia.EventArgsTypes;
using OnlyT.Avalonia.Models;
using OnlyT.Avalonia.Services.Timer;

namespace OnlyT.Avalonia.Tests.Services;

[TestClass]
public class TalkTimerServiceTests
{
    [TestMethod]
    public void InitialState_NotRunningNotPaused()
    {
        var svc = new TalkTimerService();
        Assert.IsFalse(svc.IsRunning);
        Assert.IsFalse(svc.IsPaused);
    }

    [TestMethod]
    public void Start_RaisesTimerStartedWithTarget()
    {
        var svc = new TalkTimerService();
        TimerStartedEventArgs? started = null;
        svc.TimerStartedEvent += (_, e) => started = e;

        svc.Start(targetSecs: 300, talkId: 1, isCountingUp: false);

        Assert.IsTrue(svc.IsRunning);
        Assert.IsNotNull(started);
        Assert.AreEqual(300, started!.TargetSecs);
        Assert.IsFalse(started.IsCountingUp);

        svc.Stop();
    }

    [TestMethod]
    public void Start_WithPersist_PropagatesFlagOntoChangedEvent()
    {
        var svc = new TalkTimerService();
        TimerChangedEventArgs? changed = null;
        svc.TimerChangedEvent += (_, e) => changed = e;

        svc.Start(targetSecs: 300, talkId: 1, isCountingUp: false, persistFinalTimerValue: true);
        // TimerChangedEvent only fires when the whole-second value changes, so
        // simulate a tick of elapsed time to force one while running.
        svc.CurrentSecondsElapsed = 5;

        Assert.IsNotNull(changed);
        Assert.IsTrue(changed!.IsRunning);
        Assert.IsTrue(changed.PersistFinalTimerValue);

        svc.Stop();
    }

    [TestMethod]
    public void Stop_AfterPersistStart_StopEventStillCarriesPersistFlag()
    {
        var svc = new TalkTimerService();
        svc.Start(targetSecs: 300, talkId: 1, isCountingUp: false, persistFinalTimerValue: true);
        svc.CurrentSecondsElapsed = 5; // simulate the talk having run a while

        TimerChangedEventArgs? stopEvent = null;
        svc.TimerChangedEvent += (_, e) =>
        {
            if (!e.IsRunning)
            {
                stopEvent = e;
            }
        };

        svc.Stop(); // resets elapsed 5 -> 0, which raises a stop event

        Assert.IsFalse(svc.IsRunning);
        Assert.IsNotNull(stopEvent, "A stop event (IsRunning=false) should be raised");
        Assert.IsTrue(stopEvent!.PersistFinalTimerValue,
            "The stop event must carry the persist flag so the output window can persist");
    }

    [TestMethod]
    public void Start_WithoutPersist_StopEventHasNoPersistFlag()
    {
        var svc = new TalkTimerService();
        svc.Start(targetSecs: 120, talkId: 2, isCountingUp: false, persistFinalTimerValue: false);
        svc.CurrentSecondsElapsed = 5;

        TimerChangedEventArgs? stopEvent = null;
        svc.TimerChangedEvent += (_, e) =>
        {
            if (!e.IsRunning)
            {
                stopEvent = e;
            }
        };

        svc.Stop();

        Assert.IsNotNull(stopEvent);
        Assert.IsFalse(stopEvent!.PersistFinalTimerValue);
    }

    [TestMethod]
    public void Pause_SetsPausedAndStopsRunning()
    {
        var svc = new TalkTimerService();
        svc.Start(targetSecs: 300, talkId: 1, isCountingUp: false);

        svc.Pause();

        Assert.IsTrue(svc.IsPaused);
        Assert.IsFalse(svc.IsRunning);

        svc.Stop();
    }

    [TestMethod]
    public void Resume_ClearsPausedAndResumesRunning()
    {
        var svc = new TalkTimerService();
        svc.Start(targetSecs: 300, talkId: 1, isCountingUp: false);
        svc.Pause();

        svc.Resume();

        Assert.IsFalse(svc.IsPaused);
        Assert.IsTrue(svc.IsRunning);

        svc.Stop();
    }

    [TestMethod]
    public void GetStatus_ReflectsRunningTalk()
    {
        var svc = new TalkTimerService();
        svc.Start(targetSecs: 450, talkId: 7, isCountingUp: false);

        var status = svc.GetStatus();

        Assert.IsTrue(status.IsRunning);
        Assert.AreEqual(7, status.TalkId);
        Assert.AreEqual(450, status.TargetSeconds);

        svc.Stop();
    }

    [TestMethod]
    public void StartTalkTimerFromApi_ReturnsStartCommand()
    {
        var svc = new TalkTimerService();
        var result = svc.StartTalkTimerFromApi(3);
        Assert.AreEqual(StartStopTimerCommands.Start, result.Command);
    }

    [TestMethod]
    public void StopTalkTimerFromApi_ReturnsStopCommand()
    {
        var svc = new TalkTimerService();
        var result = svc.StopTalkTimerFromApi(3);
        Assert.AreEqual(StartStopTimerCommands.Stop, result.Command);
    }
}
