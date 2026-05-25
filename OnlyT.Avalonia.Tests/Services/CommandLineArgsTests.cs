using OnlyT.Avalonia.Services;

namespace OnlyT.Avalonia.Tests.Services;

[TestClass]
public class CommandLineArgsTests
{
    [TestMethod]
    public void Parse_NoArgs_ReturnsDefaults()
    {
        var result = CommandLineArgs.Parse([]);

        Assert.IsNull(result.Port);
        Assert.IsFalse(result.NoMutex);
        Assert.IsFalse(result.AutoStart);
        Assert.IsFalse(result.ShowHelp);
        Assert.IsNull(result.ForceDarkMode);
        Assert.AreEqual(1920, result.NdiWidth);
        Assert.AreEqual(1080, result.NdiHeight);
        Assert.AreEqual(30, result.NdiFrameRate);
    }

    [TestMethod]
    public void Parse_PortFlag_SetsPort()
    {
        Assert.AreEqual(9090, CommandLineArgs.Parse(["--port", "9090"]).Port);
        Assert.AreEqual(9090, CommandLineArgs.Parse(["-p", "9090"]).Port);
    }

    [TestMethod]
    public void Parse_PortFlagWithoutValue_LeavesPortNull()
    {
        Assert.IsNull(CommandLineArgs.Parse(["--port"]).Port);
    }

    [TestMethod]
    public void Parse_PortFlagWithNonNumeric_LeavesPortNull()
    {
        Assert.IsNull(CommandLineArgs.Parse(["--port", "abc"]).Port);
    }

    [TestMethod]
    public void Parse_IsCaseInsensitive()
    {
        Assert.IsTrue(CommandLineArgs.Parse(["--AUTOSTART"]).AutoStart);
    }

    [TestMethod]
    public void Parse_NoSettings_SetsPersistenceFlag()
    {
        Assert.IsTrue(CommandLineArgs.Parse(["--nosettings"]).NoSettingsPersistence);
        Assert.IsTrue(CommandLineArgs.Parse(["--no-settings"]).NoSettingsPersistence);
    }

    [TestMethod]
    public void Parse_DarkAndLight_SetForceDarkMode()
    {
        Assert.AreEqual(true, CommandLineArgs.Parse(["--dark"]).ForceDarkMode);
        Assert.AreEqual(false, CommandLineArgs.Parse(["--light"]).ForceDarkMode);
    }

    [TestMethod]
    public void Parse_HelpAliases_SetShowHelp()
    {
        Assert.IsTrue(CommandLineArgs.Parse(["--help"]).ShowHelp);
        Assert.IsTrue(CommandLineArgs.Parse(["-h"]).ShowHelp);
        Assert.IsTrue(CommandLineArgs.Parse(["-?"]).ShowHelp);
    }

    [TestMethod]
    public void Parse_Id_SetsOptionsIdentifier()
    {
        Assert.AreEqual("station2", CommandLineArgs.Parse(["--id", "station2"]).OptionsIdentifier);
    }

    [TestMethod]
    public void Parse_NdiFlagsAndDimensions()
    {
        var result = CommandLineArgs.Parse(["--ndi", "--ndi-width", "1280", "--ndi-height", "720", "--ndi-fps", "25"]);

        Assert.IsTrue(result.IsTimerNdi);
        Assert.AreEqual(1280, result.NdiWidth);
        Assert.AreEqual(720, result.NdiHeight);
        Assert.AreEqual(25, result.NdiFrameRate);
    }

    [TestMethod]
    public void Parse_CombinedFlags_AllApplied()
    {
        var result = CommandLineArgs.Parse(["-p", "8080", "--silent", "--autostart", "--nomutex"]);

        Assert.AreEqual(8080, result.Port);
        Assert.IsTrue(result.Silent);
        Assert.IsTrue(result.AutoStart);
        Assert.IsTrue(result.NoMutex);
    }

    [TestMethod]
    public void GetHelpText_IsNotEmpty()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(CommandLineArgs.GetHelpText()));
    }
}
