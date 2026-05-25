using System;
using OnlyT.Avalonia.Models;

namespace OnlyT.Avalonia.Tests.Models;

[TestClass]
public class TalkScheduleItemTests
{
    private static TalkScheduleItem MakeItem() =>
        new(1, "Talk", "Section", "Section")
        {
            OriginalDuration = TimeSpan.FromMinutes(10)
        };

    [TestMethod]
    public void ActualDuration_NoModifications_ReturnsOriginal()
    {
        var item = MakeItem();
        Assert.AreEqual(TimeSpan.FromMinutes(10), item.ActualDuration);
    }

    [TestMethod]
    public void ActualDuration_WithModified_ReturnsModified()
    {
        var item = MakeItem();
        item.ModifiedDuration = TimeSpan.FromMinutes(7);
        Assert.AreEqual(TimeSpan.FromMinutes(7), item.ActualDuration);
    }

    [TestMethod]
    public void ActualDuration_AdaptedTakesPriorityOverModified()
    {
        var item = MakeItem();
        item.ModifiedDuration = TimeSpan.FromMinutes(7);
        item.AdaptedDuration = TimeSpan.FromMinutes(4);
        Assert.AreEqual(TimeSpan.FromMinutes(4), item.ActualDuration);
    }

    [TestMethod]
    public void ModifiedDuration_EqualToOriginal_IsTreatedAsNull()
    {
        var item = MakeItem();
        item.ModifiedDuration = TimeSpan.FromMinutes(10); // same as original
        Assert.IsNull(item.ModifiedDuration);
        Assert.AreEqual(TimeSpan.FromMinutes(10), item.ActualDuration);
    }

    [TestMethod]
    public void PlannedDuration_IgnoresAdaptedDuration()
    {
        var item = MakeItem();
        item.ModifiedDuration = TimeSpan.FromMinutes(7);
        item.AdaptedDuration = TimeSpan.FromMinutes(4);

        // PlannedDuration is modified-or-original, not adapted.
        Assert.AreEqual(TimeSpan.FromMinutes(7), item.PlannedDuration);
    }

    [TestMethod]
    public void OvertimeString_NullWhenNotCompleted()
    {
        var item = MakeItem();
        Assert.IsNull(item.OvertimeString);
    }

    [TestMethod]
    public void OvertimeString_UnderTime_ShowsNegative()
    {
        var item = MakeItem(); // 10 min = 600s actual
        item.CompletedTimeSecs = 540; // finished 60s early

        // overtimeSecs = actual - completed = 60 (>= 0) => prefixed "-"
        Assert.AreEqual("-01:00", item.OvertimeString);
    }

    [TestMethod]
    public void OvertimeString_OverTime_ShowsPositive()
    {
        var item = MakeItem(); // 600s actual
        item.CompletedTimeSecs = 660; // ran 60s long

        // overtimeSecs = 600 - 660 = -60 (< 0) => prefixed "+"
        Assert.AreEqual("+01:00", item.OvertimeString);
    }

    [TestMethod]
    public void ClosingSecs_DefaultsTo30()
    {
        var item = MakeItem();
        Assert.AreEqual(TalkScheduleItem.DefaultClosingSecs, item.ClosingSecs);
        Assert.AreEqual(30, item.ClosingSecs);
    }
}
