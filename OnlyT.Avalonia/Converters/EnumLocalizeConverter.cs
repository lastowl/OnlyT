using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.DependencyInjection;
using OnlyT.Avalonia.Services.Localization;

namespace OnlyT.Avalonia.Converters;

/// <summary>
/// Converts an enum value shown in a ComboBox to a localized, human-friendly
/// string. Without this the combos bound directly to enum arrays render raw
/// PascalCase names ("AnalogueAndDigital", "ScheduleFile", ...) that never
/// translate. Maps each value to an existing resource key; falls back to a
/// spaced-out version of the enum name if no key is found.
/// </summary>
public class EnumLocalizeConverter : IValueConverter
{
    public static readonly EnumLocalizeConverter Instance = new();

    // Keyed by "<EnumTypeName>.<ValueName>" -> resource key.
    private static readonly Dictionary<string, string> KeyMap = new()
    {
        ["OperatingMode.Manual"] = "OP_MODE_MANUAL",
        ["OperatingMode.Automatic"] = "OP_MODE_AUTO",
        ["OperatingMode.ScheduleFile"] = "OP_MODE_FILE",

        ["MidWeekOrWeekend.MidWeek"] = "MIDWEEK",
        ["MidWeekOrWeekend.Weekend"] = "WEEKEND",

        ["FullScreenClockMode.Analogue"] = "FULL_SCREEN_ANALOGUE",
        ["FullScreenClockMode.Digital"] = "FULL_SCREEN_DIGITAL",
        ["FullScreenClockMode.AnalogueAndDigital"] = "FULL_SCREEN_BOTH",

        ["AdaptiveMode.None"] = "ADAPTIVE_MODE_NONE",
        ["AdaptiveMode.OneWay"] = "ADAPTIVE_MODE_ONE_WAY",
        ["AdaptiveMode.TwoWay"] = "ADAPTIVE_MODE_TWO_WAY",

        ["ElementsToShow.DialAndDigital"] = "DIAL_AND_DIGITAL",
        ["ElementsToShow.Dial"] = "DIAL",
        ["ElementsToShow.Digital"] = "DIGITAL",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Enum)
        {
            return value?.ToString() ?? string.Empty;
        }

        var lookup = $"{value.GetType().Name}.{value}";
        if (KeyMap.TryGetValue(lookup, out var resourceKey))
        {
            var localized = Ioc.Default.GetService<ILocalizationService>()?.GetString(resourceKey);
            if (!string.IsNullOrEmpty(localized) && localized != resourceKey)
            {
                return localized;
            }
        }

        return Humanize(value.ToString() ?? string.Empty);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    // "AnalogueAndDigital" -> "Analogue And Digital" (last-resort fallback only).
    private static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
