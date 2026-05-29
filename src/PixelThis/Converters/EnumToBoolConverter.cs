using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PixelThis.Converters;

/// <summary>Returns true when the bound enum value equals the ConverterParameter name.</summary>
public class EnumToBoolConverter : IValueConverter
{
    public static readonly EnumToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is not null &&
           string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is not null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Avalonia.Data.BindingOperations.DoNothing;
}
