using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using OnlyT.Avalonia.Models;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.Options;
using OnlyT.Avalonia.Services.TalkSchedule;
using OnlyT.Avalonia.Services.Timer;
using OnlyT.Avalonia.WebServer;

namespace OnlyT.Avalonia.Tests.WebServer;

/// <summary>
/// Exercises the web/API data-shaping logic with mocked services, so the JSON
/// the web clock and remote-control API depend on can be verified without
/// binding a socket or running the listener.
/// </summary>
[TestClass]
public class HttpServerDataTests
{
    private Mock<ITalkTimerService> _timer = null!;
    private Mock<ITalkScheduleService> _schedule = null!;
    private Mock<IOptionsService> _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _timer = new Mock<ITalkTimerService>();
        _schedule = new Mock<ITalkScheduleService>();
        _options = new Mock<IOptionsService>();

        // Sensible defaults; individual tests override as needed.
        _options.SetupGet(o => o.CountUp).Returns(false);
        _options.SetupGet(o => o.CountdownDurationMins).Returns(5);
        _options.SetupGet(o => o.MeetingStartTimes).Returns(new MeetingStartTimes());
        _schedule.Setup(s => s.GetTalkScheduleItems()).Returns([]);
    }

    private HttpServer CreateServer() =>
        new(_timer.Object, _schedule.Object, _options.Object);

    private static TalkScheduleItem Talk(int id, string name, int durationMins, int closing = 45) =>
        new(id, name, "Section", "Section")
        {
            OriginalDuration = TimeSpan.FromMinutes(durationMins),
            ClosingSecs = closing,
            BellApplicable = true,
        };

    [TestMethod]
    public void GetTimersData_MapsTalksFromSchedule()
    {
        _timer.Setup(t => t.GetStatus()).Returns(new TimerStatus { IsRunning = false });
        _schedule.Setup(s => s.GetTalkScheduleItems())
            .Returns([Talk(1, "Opening", 3), Talk(2, "Bible reading", 4)]);

        var result = CreateServer().GetTimersData();

        Assert.AreEqual(2, result.TimerInfo.Count);
        Assert.AreEqual("Opening", result.TimerInfo[0].TalkTitle);
        Assert.AreEqual(180, result.TimerInfo[0].OriginalDurationSecs);
        Assert.AreEqual(240, result.TimerInfo[1].OriginalDurationSecs);
    }

    [TestMethod]
    public void GetTimersData_UsesCurrentTalkClosingSecsInStatus()
    {
        _timer.Setup(t => t.GetStatus())
            .Returns(new TimerStatus { IsRunning = true, TalkId = 2, TargetSeconds = 240 });
        _schedule.Setup(s => s.GetTalkScheduleItem(2)).Returns(Talk(2, "Talk", 4, closing: 60));

        var result = CreateServer().GetTimersData();

        Assert.AreEqual(60, result.Status.ClosingSecs);
        Assert.IsTrue(result.Status.IsRunning);
    }

    [TestMethod]
    public void GetTimersData_FallsBackToDefaultClosingWhenNoCurrentTalk()
    {
        _timer.Setup(t => t.GetStatus()).Returns(new TimerStatus { IsRunning = false, TalkId = null });

        var result = CreateServer().GetTimersData();

        Assert.AreEqual(30, result.Status.ClosingSecs);
    }

    [TestMethod]
    public void GetTimersData_AppliesCountUpDefaultWhenTalkUnset()
    {
        _options.SetupGet(o => o.CountUp).Returns(true);
        _timer.Setup(t => t.GetStatus()).Returns(new TimerStatus { IsRunning = false });
        _schedule.Setup(s => s.GetTalkScheduleItems()).Returns([Talk(1, "T", 5)]); // CountUp null

        var result = CreateServer().GetTimersData();

        Assert.IsTrue(result.TimerInfo[0].CountUp, "Talk with no CountUp should inherit the option default");
    }

    [TestMethod]
    public void BuildClockData_RunningTimer_ReportsRemainingSeconds()
    {
        _timer.Setup(t => t.GetStatus()).Returns(new TimerStatus
        {
            IsRunning = true,
            TargetSeconds = 300,
            TimeElapsed = TimeSpan.FromSeconds(120),
        });

        var data = CreateServer().BuildClockData(DateTime.Now);

        Assert.IsTrue(data.isRunning);
        Assert.AreEqual(180, data.remainingSecs);
        Assert.IsFalse(data.showCountdown, "No meeting countdown while the timer runs");
    }

    [TestMethod]
    public void BuildClockData_ShowsCountdownWithinWindow()
    {
        _timer.Setup(t => t.GetStatus()).Returns(new TimerStatus { IsRunning = false });
        _options.SetupGet(o => o.CountdownDurationMins).Returns(5);

        var now = new DateTime(2026, 1, 1, 18, 57, 0); // 3 min before a 19:00 start
        var starts = new MeetingStartTimes();
        starts.Times.Add(new MeetingStartTime { DayOfWeek = now.DayOfWeek, StartTime = new TimeSpan(19, 0, 0) });
        _options.SetupGet(o => o.MeetingStartTimes).Returns(starts);

        var data = CreateServer().BuildClockData(now);

        Assert.IsTrue(data.showCountdown);
        Assert.AreEqual(180, data.countdownSecs);
    }

    [TestMethod]
    public void BuildClockData_NoCountdownOutsideWindow()
    {
        _timer.Setup(t => t.GetStatus()).Returns(new TimerStatus { IsRunning = false });
        _options.SetupGet(o => o.CountdownDurationMins).Returns(5);

        var now = new DateTime(2026, 1, 1, 18, 40, 0); // 20 min before start, window is 5
        var starts = new MeetingStartTimes();
        starts.Times.Add(new MeetingStartTime { DayOfWeek = now.DayOfWeek, StartTime = new TimeSpan(19, 0, 0) });
        _options.SetupGet(o => o.MeetingStartTimes).Returns(starts);

        var data = CreateServer().BuildClockData(now);

        Assert.IsFalse(data.showCountdown);
        Assert.IsNull(data.countdownSecs);
    }
}
