using System.Collections.Generic;
using Microsoft.Maui.Graphics;

namespace Argus.Graphics;

/// <summary>
/// Makes a flagged colour pulse, on a cadence measured in renders.
/// </summary>
/// <remarks>
/// <para>
/// This is here rather than in a detector, and that relocation is one of the acceptance
/// criteria for this library. The prototype counted renders inside the detection code, which
/// made two unrelated things one thing: how often the screen redrew, and what the diagnostics
/// concluded. A slower machine redrew less often and therefore produced different results
/// from the same stream — and a headless replay of a capture, which redraws never, produced
/// results that could not be compared with the live run at all.
/// </para>
/// <para>
/// A render count is presentation state. It belongs to whatever is rendering.
/// </para>
/// </remarks>
public sealed class FlashCadence
{
    // The dimmed variant of each colour seen, so the dim phase does not allocate a new Color for
    // every flagged row on every render. Colours come from a small policy palette, so this stays
    // tiny; it is cleared if DimAlpha changes, and capped in case a host feeds it arbitrary colours.
    private const int DimmedCacheLimit = 64;
    private readonly Dictionary<Color, Color> _dimmed = new Dictionary<Color, Color>();
    private readonly object _dimmedGate = new object();
    private float _dimmedAlpha = float.NaN;

    /// <summary>How many renders each phase of the pulse lasts.</summary>
    /// <remarks>Fifteen: roughly a quarter-second phase at a common refresh rate, without this type having to know what the refresh rate is.</remarks>
    public int RendersPerPhase { get; set; } = 15;

    /// <summary>The alpha applied during the dim phase.</summary>
    public float DimAlpha { get; set; } = 0.35f;

    /// <summary>Applies the cadence to a colour.</summary>
    /// <param name="color">The colour chosen for the report.</param>
    /// <param name="renderCount">The host's render counter.</param>
    /// <returns>The colour, dimmed during the dim phase.</returns>
    public Color Apply(Color color, long renderCount)
    {
        if (RendersPerPhase <= 0)
        {
            return color;
        }

        long phase = renderCount / RendersPerPhase;
        bool dim = (phase & 1L) == 1L;

        if (!dim)
        {
            return color;
        }

        float alpha = DimAlpha;
        lock (_dimmedGate)
        {
            if (!alpha.Equals(_dimmedAlpha) || _dimmed.Count >= DimmedCacheLimit)
            {
                _dimmed.Clear();
                _dimmedAlpha = alpha;
            }

            Color dimmed;
            if (!_dimmed.TryGetValue(color, out dimmed))
            {
                dimmed = new Color(color.Red, color.Green, color.Blue, alpha);
                _dimmed[color] = dimmed;
            }

            return dimmed;
        }
    }
}
