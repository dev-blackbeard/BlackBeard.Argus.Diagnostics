using System;
using System.Collections.Generic;
using System.Globalization;
using Argus.Contracts;
using Microsoft.Maui.Controls;

namespace Argus.Controls.Maui;

/// <summary>Converts a possibly-null string to whether it should be shown at all.</summary>
internal sealed class StringNotNullOrEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Converts <see cref="EntityHealthItemViewModel.IsExpanded"/> to the expander button's label.</summary>
internal sealed class ExpanderLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool expanded = value is bool flag && flag;
        return expanded ? "▾ less" : "▸ more";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Converts one <see cref="EntityHealthItemViewModel.FlagCounts"/> entry to a display string.</summary>
internal sealed class FlagCountConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is KeyValuePair<HealthFlags, long> pair)
        {
            return pair.Key.ToString() + " ×" + pair.Value.ToString(CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
