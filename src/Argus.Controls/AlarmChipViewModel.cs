using Argus.Contracts;
using Microsoft.Maui.Graphics;

namespace Argus.Controls;

/// <summary>
/// One raised flag on a row, with the count of times it has fired and the colour it should be
/// drawn in — everything a per-flag alarm chip needs, already resolved.
/// </summary>
/// <remarks>
/// Immutable and rebuilt whenever <see cref="EntityHealthItemViewModel.AlarmChips"/> refreshes,
/// rather than mutated in place, so a UI binding sees a clean new list each time instead of
/// having to track in-place edits.
/// </remarks>
public sealed class AlarmChipViewModel
{
    /// <summary>Creates a chip.</summary>
    /// <param name="flag">The flag this chip represents.</param>
    /// <param name="count">How many times this flag has fired for the row, so far.</param>
    /// <param name="color">The chip's solid background colour.</param>
    /// <param name="foreground">The colour to draw the chip's icon and count in, guaranteed to read against <paramref name="color"/>.</param>
    public AlarmChipViewModel(HealthFlags flag, long count, Color color, Color foreground)
    {
        Flag = flag;
        Count = count;
        Color = color;
        Foreground = foreground;
    }

    /// <summary>The flag this chip represents.</summary>
    public HealthFlags Flag { get; }

    /// <summary>How many times this flag has fired for the row, so far.</summary>
    public long Count { get; }

    /// <summary>The chip's solid background colour.</summary>
    public Color Color { get; }

    /// <summary>
    /// The colour to draw the chip's icon and count in. Always legible against <see cref="Color"/>
    /// (see <see cref="Argus.Graphics.ContrastColor"/>) — deliberately not left to whatever a
    /// host's own default text colour happens to be, which has no guaranteed relationship to a
    /// severity colour this library chose.
    /// </summary>
    public Color Foreground { get; }
}
