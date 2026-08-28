namespace Argus.Geodesy;

/// <summary>
/// Decides whether a reported position is usable as a measurement.
/// </summary>
/// <remarks>
/// One rule, applied in exactly two places: before a sample is allowed to update an
/// entity's last-known-good state, and before an entity is allowed to contribute to a
/// group centroid. Those are the two places the prototype got wrong — it updated state
/// from invalid samples, so the tick after a <c>(0,0)</c> fabricated a jump, and it let a
/// single out-of-range entity poison cohesion for every other entity in the group.
/// </remarks>
public static class PositionValidity
{
    /// <summary>
    /// Whether a latitude and longitude pair can be used as a measurement.
    /// </summary>
    /// <param name="latitudeDegrees">Latitude in degrees, or <c>null</c> if not supplied.</param>
    /// <param name="longitudeDegrees">Longitude in degrees, or <c>null</c> if not supplied.</param>
    /// <param name="treatZeroIslandAsInvalid">
    /// Whether an exact <c>(0, 0)</c> should be rejected. It is a legal position, and it is
    /// also the value a zeroed buffer decodes to, so which of the two it is cannot be
    /// decided from the value alone. Defaulting to rejection is the conservative choice for
    /// a stream that originates from a marshalled struct: the cost of rejecting a real
    /// entity sitting exactly on the origin is one <c>NotEvaluable</c>, and the cost of
    /// accepting an uninitialised frame is a fabricated jump on the following tick.
    /// </param>
    /// <returns><c>true</c> if the position may be used.</returns>
    public static bool IsUsable(double? latitudeDegrees, double? longitudeDegrees, bool treatZeroIslandAsInvalid)
    {
        if (!latitudeDegrees.HasValue || !longitudeDegrees.HasValue)
        {
            return false;
        }

        double latitude = latitudeDegrees.Value;
        double longitude = longitudeDegrees.Value;

        if (!Geo.IsValidLatitude(latitude) || !Geo.IsValidLongitude(longitude))
        {
            return false;
        }

        if (treatZeroIslandAsInvalid && latitude == 0.0 && longitude == 0.0)
        {
            return false;
        }

        return true;
    }
}
