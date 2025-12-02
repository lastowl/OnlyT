using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OnlyT.Avalonia.Converters;

/// <summary>
/// Converts a percentage (0-100) to a pixel size for clock display
/// At 100%, returns Double.PositiveInfinity (no max constraint)
/// At lower percentages, returns proportionally smaller values
/// </summary>
public class PercentToSizeConverter : IValueConverter
{
    public static readonly PercentToSizeConverter Instance = new();

    // Base size at 100% - this gives a reasonable maximum
    private const double BaseSize = 2000;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int percent)
        {
            if (percent >= 100)
            {
                return double.PositiveInfinity;
            }

            // Scale from 30-100% to actual size
            // At 100%: no constraint (infinity)
            // At 30%: 30% of BaseSize = 600px
            return BaseSize * (percent / 100.0);
        }

        return double.PositiveInfinity;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
