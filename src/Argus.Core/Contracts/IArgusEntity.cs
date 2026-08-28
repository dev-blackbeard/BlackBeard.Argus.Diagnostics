namespace Argus.Contracts;

/// <summary>
/// The fastest of the three ways an application type can expose its position to Argus.
/// </summary>
/// <remarks>
/// Implementing this is optional. The compatibility facade resolves position from an
/// arbitrary <c>TEntity</c> in this precedence order:
/// <list type="number">
/// <item><description><c>TEntity : IArgusEntity</c> — direct property access, no reflection.</description></item>
/// <item><description>An accessor delegate registered on <c>MonitorOptions.Accessors</c>.</description></item>
/// <item><description>Convention — a compiled expression tree over configurable property-name candidates.</description></item>
/// </list>
/// Applications that cannot take a dependency on Argus from their model types use one of
/// the other two routes. Nothing about this interface is required for the library to work.
/// </remarks>
public interface IArgusEntity
{
    /// <summary>Stable identity of the entity within the stream.</summary>
    string EntityId { get; }

    /// <summary>Latitude in degrees, positive north, or <c>null</c> if not known.</summary>
    double? Latitude { get; }

    /// <summary>Longitude in degrees, positive east, or <c>null</c> if not known.</summary>
    double? Longitude { get; }

    /// <summary>Altitude in metres above the reference ellipsoid, or <c>null</c> if not known.</summary>
    double? Altitude { get; }
}
