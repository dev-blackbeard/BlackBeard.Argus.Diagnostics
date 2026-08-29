namespace Argus.Contracts;

/// <summary>
/// The minimum an arbitrary application entity has to yield for group checks to run:
/// who it is and where it is.
/// </summary>
/// <remarks>
/// This is what an accessor — interface, delegate or convention-compiled — produces. It is
/// deliberately tiny: the group detectors need identity to exclude the entity under test
/// from its own centroid, and a position to contribute.
/// </remarks>
public readonly struct EntitySnapshot
{
    /// <summary>Creates a snapshot.</summary>
    /// <param name="entityId">The entity's identity, or <c>null</c> if it could not be resolved.</param>
    /// <param name="latitude">Latitude in degrees, or <c>null</c>.</param>
    /// <param name="longitude">Longitude in degrees, or <c>null</c>.</param>
    /// <param name="altitude">Altitude in metres, or <c>null</c>.</param>
    public EntitySnapshot(string? entityId, double? latitude, double? longitude, double? altitude)
    {
        EntityId = entityId;
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
    }

    /// <summary>The entity's identity, or <c>null</c> if the accessor could not resolve one.</summary>
    public string? EntityId { get; }

    /// <summary>Latitude in degrees, positive north.</summary>
    public double? Latitude { get; }

    /// <summary>Longitude in degrees, positive east.</summary>
    public double? Longitude { get; }

    /// <summary>Altitude in metres above the reference ellipsoid.</summary>
    public double? Altitude { get; }
}
