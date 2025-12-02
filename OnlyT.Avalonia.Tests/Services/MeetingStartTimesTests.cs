using System.Globalization;
using OnlyT.Avalonia.Services.Options;

namespace OnlyT.Avalonia.Tests.Services;

[TestClass]
public class MeetingStartTimesTests
{
    [TestInitialize]
    public void Setup()
    {
        // Use English culture for consistent day name parsing in tests
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
    }

    [TestMethod]
    public void FromText_WithMultipleLines_ParsesAll()
    {
        // Arrange
        var times = new MeetingStartTimes();
        var text = "Mon 19:00\nSat 10:00";

        // Act
        times.FromText(text);

        // Assert
        Assert.AreEqual(2, times.Times.Count);
        Assert.AreEqual(DayOfWeek.Monday, times.Times[0].DayOfWeek);
        Assert.AreEqual(DayOfWeek.Saturday, times.Times[1].DayOfWeek);
    }

    [TestMethod]
    public void FromText_WithEmptyLines_IgnoresThem()
    {
        // Arrange
        var times = new MeetingStartTimes();
        var text = "Mon 19:00\n\n\nSat 10:00\n";

        // Act
        times.FromText(text);

        // Assert
        Assert.AreEqual(2, times.Times.Count);
    }

    [TestMethod]
    public void FromText_WithWindowsLineEndings_ParsesCorrectly()
    {
        // Arrange
        var times = new MeetingStartTimes();
        var text = "Mon 19:00\r\nSat 10:00";

        // Act
        times.FromText(text);

        // Assert
        Assert.AreEqual(2, times.Times.Count);
    }

    [TestMethod]
    public void FromText_ClearsPreviousTimes()
    {
        // Arrange
        var times = new MeetingStartTimes();
        times.FromText("Mon 19:00\nTue 19:00\nWed 19:00");
        Assert.AreEqual(3, times.Times.Count);

        // Act
        times.FromText("Sat 10:00");

        // Assert
        Assert.AreEqual(1, times.Times.Count);
        Assert.AreEqual(DayOfWeek.Saturday, times.Times[0].DayOfWeek);
    }

    [TestMethod]
    public void GetStartTimeForDay_WithMatchingDay_ReturnsTime()
    {
        // Arrange
        var times = new MeetingStartTimes();
        times.FromText("Mon 19:00\nSat 10:00");

        // Act
        var result = times.GetStartTimeForDay(DayOfWeek.Monday);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(new TimeSpan(19, 0, 0), result.Value);
    }

    [TestMethod]
    public void GetStartTimeForDay_WithNoMatchingDay_ReturnsNull()
    {
        // Arrange
        var times = new MeetingStartTimes();
        times.FromText("Mon 19:00\nSat 10:00");

        // Act
        var result = times.GetStartTimeForDay(DayOfWeek.Wednesday);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetStartTimeForDay_WithGenericTime_UsesItAsFallback()
    {
        // Arrange
        var times = new MeetingStartTimes();
        times.FromText("Mon 19:00\n10:00");  // Second is generic (no day)

        // Act
        var result = times.GetStartTimeForDay(DayOfWeek.Wednesday);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(new TimeSpan(10, 0, 0), result.Value);
    }

    [TestMethod]
    public void GetStartTimeForDay_PrefersSpecificDayOverGeneric()
    {
        // Arrange
        var times = new MeetingStartTimes();
        times.FromText("Mon 19:00\n10:00");  // Generic is 10:00

        // Act
        var result = times.GetStartTimeForDay(DayOfWeek.Monday);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(new TimeSpan(19, 0, 0), result.Value);  // Should use Monday-specific time
    }

    [TestMethod]
    public void AsText_FormatsAllTimes()
    {
        // Arrange
        var times = new MeetingStartTimes();
        times.FromText("Mon 19:00\nSat 10:00");

        // Act
        var result = times.AsText();

        // Assert
        Assert.IsTrue(result.Contains("19:00"));
        Assert.IsTrue(result.Contains("10:00"));
    }

    [TestMethod]
    public void Sanitize_SanitizesAllTimes()
    {
        // Arrange
        var times = new MeetingStartTimes();
        times.Times.Add(new MeetingStartTime { StartTime = new TimeSpan(19, 0, 0) });
        times.Times.Add(new MeetingStartTime { StartTime = new TimeSpan(30, 0, 0) }); // Invalid

        // Act
        times.Sanitize();

        // Assert
        Assert.AreEqual(new TimeSpan(19, 0, 0), times.Times[0].StartTime);
        Assert.AreEqual(TimeSpan.FromHours(24), times.Times[1].StartTime);
    }

    [TestMethod]
    public void FromText_WithEmptyString_ClearsTimesAndDoesNotThrow()
    {
        // Arrange
        var times = new MeetingStartTimes();
        times.FromText("Mon 19:00");

        // Act
        times.FromText("");

        // Assert
        Assert.AreEqual(0, times.Times.Count);
    }

    [TestMethod]
    public void FromText_WithInvalidLines_IgnoresThem()
    {
        // Arrange
        var times = new MeetingStartTimes();
        var text = "Mon 19:00\nInvalidLine\nSat 10:00";

        // Act
        times.FromText(text);

        // Assert
        Assert.AreEqual(2, times.Times.Count);
    }
}
