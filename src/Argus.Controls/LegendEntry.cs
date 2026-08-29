using Argus.Contracts;
using Microsoft.Maui.Graphics;

namespace Argus.Controls;

/// <summary>One row of a legend: a title, a one-line definition, and a colour.</summary>
/// <remarks>
/// <see cref="Flag"/> is <c>null</c> for the two health-state rows (healthy, not evaluated),
/// which have no icon of their own — a legend renders those as a plain colour swatch and
/// everything else as <see cref="Argus.Graphics.AlarmIconPainter"/>'s icon for <see cref="Flag"/>.
/// </remarks>
public sealed class LegendEntry
{
    /// <summary>Creates an entry.</summary>
    /// <param name="title">The short name shown for this row.</param>
    /// <param name="definition">The one-line, self-describing explanation of what this row means.</param>
    /// <param name="color">The colour this row's swatch or icon is drawn in.</param>
    /// <param name="flag">The flag this row explains, or <c>null</c> for a health-state row with no icon.</param>
    public LegendEntry(string title, string definition, Color color, HealthFlags? flag)
    {
        Title = title;
        Definition = definition;
        Color = color;
        Flag = flag;
    }

    /// <summary>The short name shown for this row.</summary>
    public string Title { get; }

    /// <summary>The one-line, self-describing explanation of what this row means.</summary>
    public string Definition { get; }

    /// <summary>The colour this row's swatch or icon is drawn in.</summary>
    public Color Color { get; }

    /// <summary>The flag this row explains, or <c>null</c> for a health-state row with no icon.</summary>
    public HealthFlags? Flag { get; }

    /// <summary>Whether this row has an icon (an alarm row) rather than a plain colour swatch (a health-state row).</summary>
    public bool HasIcon
    {
        get { return Flag.HasValue; }
    }

    /// <summary>The inverse of <see cref="HasIcon"/>, for binding the plain swatch's visibility without a converter.</summary>
    public bool IsSwatchOnly
    {
        get { return !Flag.HasValue; }
    }
}
