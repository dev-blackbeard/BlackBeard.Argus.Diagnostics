namespace Argus.Graphics;

/// <summary>
/// Fades an opacity from a peak down to zero, on a cadence measured in renders, counted from
/// whenever a row was last updated.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately a separate signal from <see cref="FlashCadence"/> and from
/// <see cref="ColorPolicy"/>'s severity colour. A finding's colour is meant to be noticed and
/// stay noticed until it is resolved; a new sample arriving is neither of those things — it
/// happens on every healthy tick too, and needs to read as a receipt, not an alarm. Sharing one
/// visual channel between "a value changed" and "something is wrong" would make the important
/// one harder to see, not easier, which is why this is its own primitive rather than a mode of
/// <see cref="FlashCadence"/>. <see cref="PeakOpacity"/> defaults well under fully opaque for the
/// same reason: it should read as a quiet receipt next to a severity colour, never compete with
/// one.
/// </para>
/// <para>
/// Render-count driven for the same reason <see cref="FlashCadence"/> is: a render count is
/// presentation state, so a slower machine or a headless replay must not change what a detector
/// concluded, only how often the fade is repainted.
/// </para>
/// </remarks>
public sealed class ReceiptPulse
{
    /// <summary>How many renders the fade from <see cref="PeakOpacity"/> to zero takes.</summary>
    /// <remarks>
    /// Eight: brief enough to read as "this just changed" rather than a persistent status, long
    /// enough to actually be seen at a host's typical render-tick rate.
    /// </remarks>
    public int FadeRenders { get; set; } = 8;

    /// <summary>The opacity at the instant of receipt, before any fade has been applied.</summary>
    /// <remarks>
    /// Well under fully opaque by default, so the receipt reads as subtle next to a flagged row's
    /// severity colour rather than competing with it.
    /// </remarks>
    public double PeakOpacity { get; set; } = 0.6;

    /// <summary>Resolves the current opacity for a row.</summary>
    /// <param name="rendersSinceUpdate">
    /// The host's render counter minus the render count recorded when the row was last updated.
    /// Zero or negative means "updated this render."
    /// </param>
    /// <returns><see cref="PeakOpacity"/> at zero, fading linearly to zero by <see cref="FadeRenders"/>.</returns>
    public double Resolve(long rendersSinceUpdate)
    {
        if (rendersSinceUpdate <= 0)
        {
            return PeakOpacity;
        }

        if (FadeRenders <= 0 || rendersSinceUpdate >= FadeRenders)
        {
            return 0.0;
        }

        double remaining = 1.0 - ((double)rendersSinceUpdate / FadeRenders);
        return PeakOpacity * remaining;
    }
}
