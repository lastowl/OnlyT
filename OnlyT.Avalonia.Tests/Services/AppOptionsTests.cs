using OnlyT.Avalonia.Services;

namespace OnlyT.Avalonia.Tests.Services;

[TestClass]
public class AppOptionsTests
{
    [TestMethod]
    public void Sanitize_ClampsBellVolumeAboveRange()
    {
        var options = new AppOptions { BellVolumePercent = 250 };
        options.Sanitize();
        Assert.AreEqual(100, options.BellVolumePercent);
    }

    [TestMethod]
    public void Sanitize_ClampsBellVolumeBelowRange()
    {
        var options = new AppOptions { BellVolumePercent = -10 };
        options.Sanitize();
        Assert.AreEqual(0, options.BellVolumePercent);
    }

    [TestMethod]
    public void Sanitize_ClampsAnalogueClockWidth()
    {
        var options = new AppOptions { AnalogueClockWidthPercent = 500 };
        options.Sanitize();
        Assert.AreEqual(100, options.AnalogueClockWidthPercent);
    }

    [TestMethod]
    public void Sanitize_ClampsCountdownDurationMins()
    {
        var below = new AppOptions { CountdownDurationMins = 0 };
        below.Sanitize();
        Assert.AreEqual(1, below.CountdownDurationMins);

        var above = new AppOptions { CountdownDurationMins = 999 };
        above.Sanitize();
        Assert.AreEqual(60, above.CountdownDurationMins);
    }

    [TestMethod]
    public void Sanitize_ClampsHttpServerPort()
    {
        var above = new AppOptions { HttpServerPort = 200000 };
        above.Sanitize();
        Assert.AreEqual(65535, above.HttpServerPort);

        var below = new AppOptions { HttpServerPort = 0 };
        below.Sanitize();
        Assert.AreEqual(1, below.HttpServerPort);
    }

    [TestMethod]
    public void Sanitize_ClampsPersistDurationBelowRange()
    {
        var options = new AppOptions { PersistDurationSecs = 1 };
        options.Sanitize();
        Assert.AreEqual(5, options.PersistDurationSecs);
    }

    [TestMethod]
    public void Sanitize_ClampsPersistDurationAboveRange()
    {
        var options = new AppOptions { PersistDurationSecs = 5000 };
        options.Sanitize();
        Assert.AreEqual(600, options.PersistDurationSecs);
    }

    [TestMethod]
    public void Sanitize_LeavesValidPersistDurationUnchanged()
    {
        var options = new AppOptions { PersistDurationSecs = 90 };
        options.Sanitize();
        Assert.AreEqual(90, options.PersistDurationSecs);
    }

    [TestMethod]
    public void Sanitize_DefaultsEmptyCulture()
    {
        var options = new AppOptions { Culture = string.Empty };
        options.Sanitize();
        Assert.AreEqual("en-GB", options.Culture);
    }

    [TestMethod]
    public void Sanitize_DefaultsEmptyLogLevel()
    {
        var options = new AppOptions { LogEventLevel = string.Empty };
        options.Sanitize();
        Assert.AreEqual("Information", options.LogEventLevel);
    }

    [TestMethod]
    public void Defaults_PersistOptionsMatchExpectedValues()
    {
        var options = new AppOptions();
        Assert.IsTrue(options.PersistStudentTime, "PersistStudentTime should default on");
        Assert.IsTrue(options.ShowPersistCountdown, "ShowPersistCountdown should default on");
        Assert.AreEqual(90, options.PersistDurationSecs);
        Assert.IsTrue(options.ShowDigitalSeconds, "ShowDigitalSeconds should default on");
    }
}
