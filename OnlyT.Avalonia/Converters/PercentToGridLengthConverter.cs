using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace OnlyT.Avalonia.Converters;

public class PercentToGridLengthConverter : IValueConverter
{
    public static readonly PercentToGridLengthConverter Instance = new();
    public static readonly PercentToGridLengthConverter ComplementInstance = new() { IsComplement = true };

    public bool IsComplement { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int percent)
        {
            var v = IsComplement ? (100 - percent) : percent;
            return new GridLength(Math.Max(v, 1), GridUnitType.Star);
        }

        return new GridLength(1, GridUnitType.Star);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
