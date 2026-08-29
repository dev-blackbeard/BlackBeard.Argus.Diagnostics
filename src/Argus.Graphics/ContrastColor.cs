using Microsoft.Maui.Graphics;

namespace Argus.Graphics;

/// <summary>Picks a legible black-or-white foreground for a given background colour.</summary>
/// <remarks>
/// Exists so an icon or a count drawn on top of a solid, data-driven background (a severity
/// colour, here) is never left to a UI framework's own default text colour, which only happens
/// to make sense against whatever <i>that</i> framework's own default background is — not
/// against a colour this library chose. A pill whose background is "whichever category fired"
/// and whose foreground is "whatever the OS theme defaults to" are two independently varying
/// facts with no guaranteed relationship between them, and that mismatch — not any one colour
/// being wrong on its own — is what loses the contrast.
/// </remarks>
public static class ContrastColor
{
    // Standard perceptual luma weights (Rec. 601) -- good enough for a binary light/dark call;
    // this is choosing between two fixed answers; not measuring or reporting a colour value, so
    // it does not need to be more precise than "which one reads better."
    private const float RedWeight = 0.299f;
    private const float GreenWeight = 0.587f;
    private const float BlueWeight = 0.114f;

    /// <summary>The luma below which white reads better than black.</summary>
    private const float DarkThreshold = 0.5f;

    /// <summary>Returns white or black, whichever reads better against <paramref name="background"/>.</summary>
    /// <param name="background">The colour text or an icon will be drawn on top of.</param>
    /// <returns>White for a dark background, black for a light one.</returns>
    public static Color ForBackground(Color background)
    {
        float luma = (RedWeight * background.Red) + (GreenWeight * background.Green) + (BlueWeight * background.Blue);
        return luma < DarkThreshold ? Colors.White : Colors.Black;
    }
}
