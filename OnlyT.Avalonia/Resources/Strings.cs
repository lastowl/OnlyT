namespace OnlyT.Avalonia.Resources;

using System.Resources;
using System.Reflection;
using System.Globalization;

/// <summary>
/// String resources for talk and section names - using localization
/// </summary>
public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("OnlyT.Avalonia.Properties.Resources", Assembly.GetExecutingAssembly());

    private static string GetString(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";

    // Talk Names
    public static string TALK_OPENING_COMMENTS => GetString("TALK_OPENING_COMMENTS");
    public static string TALK_TREASURES => GetString("TALK_TREASURES");
    public static string TALK_DIGGING => GetString("TALK_DIGGING");
    public static string TALK_READING => GetString("TALK_READING");
    public static string TALK_PRESENTATIONS => GetString("TALK_PRESENTATIONS");
    public static string TALK_LIVING1 => GetString("TALK_LIVING1");
    public static string TALK_LIVING2 => GetString("TALK_LIVING2");
    public static string TALK_CONG_STUDY => GetString("TALK_CONG_STUDY");
    public static string TALK_CONCLUDING_COMMENTS => GetString("TALK_CONCLUDING_COMMENTS");
    public static string TALK_SERVICE => GetString("TALK_SERVICE");
    public static string TALK_PUBLIC => GetString("TALK_PUBLIC");
    public static string TALK_WT => GetString("TALK_WT");
    public static string TALK_CONCLUDING => GetString("TALK_CONCLUDING");

    // Ministry Item Names
    public static string MINISTRY1 => GetString("MINISTRY1");
    public static string MINISTRY2 => GetString("MINISTRY2");
    public static string MINISTRY3 => GetString("MINISTRY3");
    public static string MINISTRY4 => GetString("MINISTRY4");

    // Section Names
    public static string SECTION_TREASURES => GetString("SECTION_TREASURES");
    public static string SECTION_MINISTRY => GetString("SECTION_MINISTRY");
    public static string SECTION_LIVING => GetString("SECTION_LIVING");
    public static string SECTION_WEEKEND => GetString("SECTION_WEEKEND");

    // Meeting Types
    public static string MIDWEEK => GetString("MIDWEEK");
    public static string WEEKEND => GetString("WEEKEND");

    // Operating Modes
    public static string OP_MODE_MANUAL => GetString("OP_MODE_MANUAL");
    public static string OP_MODE_AUTO => GetString("OP_MODE_AUTO");
    public static string OP_MODE_FILE => GetString("OP_MODE_FILE");

    // Adaptive Modes
    public static string ADAPTIVE_MODE_NONE => GetString("ADAPTIVE_MODE_NONE");
    public static string ADAPTIVE_MODE_ONE_WAY => GetString("ADAPTIVE_MODE_ONE_WAY");
    public static string ADAPTIVE_MODE_TWO_WAY => GetString("ADAPTIVE_MODE_TWO_WAY");

    // Clock Display Modes
    public static string FULL_SCREEN_DIGITAL => GetString("FULL_SCREEN_DIGITAL");
    public static string FULL_SCREEN_ANALOGUE => GetString("FULL_SCREEN_ANALOGUE");
    public static string FULL_SCREEN_BOTH => GetString("FULL_SCREEN_BOTH");

    // Clock Formats
    public static string CLOCK_FORMAT_12 => GetString("CLOCK_FORMAT_12");
    public static string CLOCK_FORMAT_12Z => GetString("CLOCK_FORMAT_12Z");
    public static string CLOCK_FORMAT_24 => GetString("CLOCK_FORMAT_24");
    public static string CLOCK_FORMAT_24Z => GetString("CLOCK_FORMAT_24Z");
    public static string CLOCK_FORMAT_12AMPM => GetString("CLOCK_FORMAT_12AMPM");
    public static string CLOCK_FORMAT_12ZAMPM => GetString("CLOCK_FORMAT_12ZAMPM");

    // Reminders
    public static string TIMER_REMINDER_MSG => GetString("TIMER_REMINDER_MSG");

    // Log Levels
    public static string LOG_LEVEL_VERBOSE => GetString("LOG_LEVEL_VERBOSE");
    public static string LOG_LEVEL_DEBUG => GetString("LOG_LEVEL_DEBUG");
    public static string LOG_LEVEL_INFORMATION => GetString("LOG_LEVEL_INFORMATION");
    public static string LOG_LEVEL_WARNING => GetString("LOG_LEVEL_WARNING");
    public static string LOG_LEVEL_ERROR => GetString("LOG_LEVEL_ERROR");
    public static string LOG_LEVEL_FATAL => GetString("LOG_LEVEL_FATAL");
}
