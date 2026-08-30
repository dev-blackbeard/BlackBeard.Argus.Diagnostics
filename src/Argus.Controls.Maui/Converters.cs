using System;
using System.Globalization;
using Argus.Controls;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

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

/// <summary>
/// Converts an <see cref="AlarmChipViewModel"/> or a <see cref="LegendEntry"/> to the
/// <see cref="IDrawable"/> a <c>GraphicsView</c> renders its icon with.
/// </summary>
/// <remarks>
/// Draws in each source's <c>Foreground</c>, not its <c>Color</c>: <c>Color</c> is now the solid
/// backing plate behind the icon (the chip's pill, the legend row's swatch), so drawing the icon
/// in that same colour again would make it disappear into its own background.
/// </remarks>
internal sealed class AlarmIconDrawableConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AlarmChipViewModel chip)
        {
            return new AlarmIconDrawable(chip.Flag, chip.Foreground);
        }

        if (value is LegendEntry entry && entry.Flag.HasValue)
        {
            return new AlarmIconDrawable(entry.Flag.Value, entry.Foreground);
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
