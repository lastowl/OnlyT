using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OnlyT.Avalonia.Models;
using OnlyT.Avalonia.Services.TalkSchedule;
using OnlyT.Avalonia.Utils;

namespace OnlyT.Avalonia.Tests.Services;

/// <summary>
/// Verifies that a schedule exported to the file-based XML format reads back
/// with its key attributes intact — the contract that file-based mode and the
/// persist-after-stop feature both rely on.
/// </summary>
[TestClass]
public class ScheduleRoundTripTests
{
    private string _tempFile = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"onlyt_schedule_{Guid.NewGuid():N}.xml");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    private static TalkScheduleItem Talk(string name, int minutes, bool bell, bool persist, bool? countUp = null) =>
        new(1, name, "Treasures", "Treasures")
        {
            OriginalDuration = TimeSpan.FromMinutes(minutes),
            BellApplicable = bell,
            PersistFinalTimerValue = persist,
            Editable = true,
            CountUp = countUp,
        };

    [TestMethod]
    public void RoundTrip_PreservesNameAndDuration()
    {
        var talks = new List<TalkScheduleItem> { Talk("Opening", 3, bell: false, persist: false) };
        ScheduleExporter.Export(talks, _tempFile);

        var read = TalkScheduleFileBased.ParseFile(_tempFile, autoBell: false);

        Assert.AreEqual(1, read.Count);
        Assert.AreEqual("Opening", read[0].Name);
        Assert.AreEqual(TimeSpan.FromMinutes(3), read[0].OriginalDuration);
    }

    [TestMethod]
    public void RoundTrip_PreservesPersistFlag()
    {
        var talks = new List<TalkScheduleItem>
        {
            Talk("Student talk", 4, bell: true, persist: true),
            Talk("Non-persist", 5, bell: false, persist: false),
        };
        ScheduleExporter.Export(talks, _tempFile);

        var read = TalkScheduleFileBased.ParseFile(_tempFile, autoBell: false);

        Assert.IsTrue(read[0].PersistFinalTimerValue, "Persist flag should survive round-trip");
        Assert.IsFalse(read[1].PersistFinalTimerValue, "Non-persist talk should stay non-persist");
    }

    [TestMethod]
    public void RoundTrip_PreservesBellAndCountUp()
    {
        var talks = new List<TalkScheduleItem> { Talk("Counting up", 6, bell: true, persist: false, countUp: true) };
        ScheduleExporter.Export(talks, _tempFile);

        var read = TalkScheduleFileBased.ParseFile(_tempFile, autoBell: false);

        Assert.IsTrue(read[0].BellApplicable);
        Assert.AreEqual(true, read[0].CountUp);
    }

    [TestMethod]
    public void RoundTrip_AppliesAutoBellArgument()
    {
        ScheduleExporter.Export([Talk("T", 1, bell: true, persist: false)], _tempFile);

        Assert.IsTrue(TalkScheduleFileBased.ParseFile(_tempFile, autoBell: true)[0].AutoBell);
        Assert.IsFalse(TalkScheduleFileBased.ParseFile(_tempFile, autoBell: false)[0].AutoBell);
    }

    [TestMethod]
    public void RoundTrip_AssignsSequentialIds()
    {
        var talks = new List<TalkScheduleItem>
        {
            Talk("A", 1, false, false),
            Talk("B", 2, false, false),
        };
        ScheduleExporter.Export(talks, _tempFile);

        var read = TalkScheduleFileBased.ParseFile(_tempFile, autoBell: false);

        Assert.AreEqual(read[0].Id + 1, read[1].Id, "Ids should be sequential");
    }

    [TestMethod]
    public void ParseFile_MissingFile_ReturnsEmptyList()
    {
        var read = TalkScheduleFileBased.ParseFile(
            Path.Combine(Path.GetTempPath(), "does_not_exist_onlyt.xml"), autoBell: false);

        Assert.AreEqual(0, read.Count);
    }

    [TestMethod]
    public void ParseFile_MalformedXml_ReturnsEmptyList()
    {
        File.WriteAllText(_tempFile, "<meeting><items><item name=");
        var read = TalkScheduleFileBased.ParseFile(_tempFile, autoBell: false);
        Assert.AreEqual(0, read.Count);
    }
}
