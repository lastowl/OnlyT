using System.Globalization;
using OnlyT.Avalonia.Services.Options;

namespace OnlyT.Avalonia.Tests.Services;

[TestClass]
public class MeetingStartTimeTests
{
    [TestInitialize]
    public void Setup()
    {
        // Use English culture for consistent day name parsing in tests
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
    }

    [TestMethod]
    public void FromText_WithValidTimeOnly_ParsesCorrectly()
    {
        // Arrange
        var text = "19:00";

        // Act
        var result = MeetingStartTime.FromText(text);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNull(result.DayOfWeek);
        Assert.AreEqual(new TimeSpan(19, 0, 0), result.StartTime);
    }

    [TestMethod]
    public void FromText_WithDayAndTime_ParsesCorrectly()
    {
        // Arrange
        var text = "Mon 19:00";

        // Act
        var result = MeetingStartTime.FromText(text);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(DayOfWeek.Monday, result.DayOfWeek);
        Assert.AreEqual(new TimeSpan(19, 0, 0), result.StartTime);
    }

    [TestMethod]
    public void FromText_With12HourPmTime_ParsesCorrectly()
    {
        // Arrange
        var text = "7pm";

        // Act
        var result = MeetingStartTime.FromText(text);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNull(result.DayOfWeek);
        Assert.AreEqual(new TimeSpan(19, 0, 0), result.StartTime);
    }

    [TestMethod]
    public void FromText_With12HourAmTime_ParsesCorrectly()
    {
        // Arrange
        var text = "10:30";

        // Act
        var result = MeetingStartTime.FromText(text);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(new TimeSpan(10, 30, 0), result.StartTime);
    }

    [TestMethod]
    public void FromText_WithSaturdayMorning_ParsesCorrectly()
    {
        // Arrange
        var text = "Sat 10:00";

        // Act
        var result = MeetingStartTime.FromText(text);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(DayOfWeek.Saturday, result.DayOfWeek);
        Assert.AreEqual(new TimeSpan(10, 0, 0), result.StartTime);
    }

    [TestMethod]
    public void FromText_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = MeetingStartTime.FromText("");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void FromText_WithNullString_ReturnsNull()
    {
        // Act
        var result = MeetingStartTime.FromText(null!);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void FromText_WithWhitespace_ReturnsNull()
    {
        // Act
        var result = MeetingStartTime.FromText("   ");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void AsText_WithDayAndTime_FormatsCorrectly()
    {
        // Arrange
        var startTime = new MeetingStartTime
        {
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeSpan(19, 30, 0)
        };

        // Act
        var result = startTime.AsText();

        // Assert
        Assert.IsTrue(result.Contains("19:30"));
    }

    [TestMethod]
    public void AsText_WithTimeOnly_FormatsCorrectly()
    {
        // Arrange
        var startTime = new MeetingStartTime
        {
            DayOfWeek = null,
            StartTime = new TimeSpan(10, 0, 0)
        };

        // Act
        var result = startTime.AsText();

        // Assert
        Assert.AreEqual("10:00", result);
    }

    [TestMethod]
    public void Sanitize_WithValidTime_DoesNotChange()
    {
        // Arrange
        var startTime = new MeetingStartTime
        {
            StartTime = new TimeSpan(19, 0, 0)
        };

        // Act
        startTime.Sanitize();

        // Assert
        Assert.AreEqual(new TimeSpan(19, 0, 0), startTime.StartTime);
    }

    [TestMethod]
    public void Sanitize_WithTimeOver24Hours_CapsAt24()
    {
        // Arrange
        var startTime = new MeetingStartTime
        {
            StartTime = new TimeSpan(25, 0, 0)
        };

        // Act
        startTime.Sanitize();

        // Assert
        Assert.AreEqual(TimeSpan.FromHours(24), startTime.StartTime);
    }

    [TestMethod]
    public void FromText_WithSingleDigitHour_ParsesCorrectly()
    {
        // Arrange
        var text = "7";

        // Act
        var result = MeetingStartTime.FromText(text);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(new TimeSpan(7, 0, 0), result.StartTime);
    }

    [TestMethod]
    public void FromText_WithThreeDigitTime_ParsesCorrectly()
    {
        // Arrange - 7:30 as "730"
        var text = "730";

        // Act
        var result = MeetingStartTime.FromText(text);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(new TimeSpan(7, 30, 0), result.StartTime);
    }

    [TestMethod]
    public void FromText_WithFourDigitTime_ParsesCorrectly()
    {
        // Arrange - 19:30 as "1930"
        var text = "1930";

        // Act
        var result = MeetingStartTime.FromText(text);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(new TimeSpan(19, 30, 0), result.StartTime);
    }
}
