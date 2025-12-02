namespace OnlyT.Avalonia.Services.Options;

/// <summary>
/// Clock hour display format options
/// </summary>
public enum ClockHourFormat
{
    /// <summary>
    /// 12hr clock format, e.g. "3:00"
    /// </summary>
    Format12,

    /// <summary>
    /// 12hr clock format with leading hr zero, e.g. "03:00"
    /// </summary>
    Format12LeadingZero,

    /// <summary>
    /// 24hr clock format, e.g. "15:00"
    /// </summary>
    Format24,

    /// <summary>
    /// 24hr clock format with leading hr zero, e.g. "06:00"
    /// </summary>
    Format24LeadingZero,

    /// <summary>
    /// 12hr clock format with AM/PM, e.g. "3:00 PM"
    /// </summary>
    Format12AMPM,

    /// <summary>
    /// 12hr clock format with leading zero and AM/PM, e.g. "03:00 PM"
    /// </summary>
    Format12LeadingZeroAMPM
}

/// <summary>
/// Display item for clock hour format selection
/// </summary>
public class ClockHourFormatItem
{
    public ClockHourFormatItem(string name, ClockHourFormat format, string example)
    {
        Name = name;
        Format = format;
        Example = example;
    }

    public string Name { get; }
    public ClockHourFormat Format { get; }
    public string Example { get; }

    public string DisplayName => $"{Name} ({Example})";
}
