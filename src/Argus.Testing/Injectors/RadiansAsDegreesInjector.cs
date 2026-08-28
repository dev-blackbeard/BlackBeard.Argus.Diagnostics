using System.Collections.Generic;
using Argus.Contracts;
using Argus.Geodesy;

namespace Argus.Testing.Injectors;

/// <summary>Supplies angular values in radians where the field is specified in degrees.</summary>
/// <remarks>
/// The reason this needs a run of samples to detect rather than a single one: the damaged
/// values are still in range, still finite, and still describe a position. They describe a
/// position roughly fifty-seven times closer to the origin than the real one, which on a map
/// looks like an entity that is somewhere else, not like an entity whose units are wrong.
/// </remarks>
public sealed class RadiansAsDegreesInjector : ISampleInjector
{
    /// <inheritdoc />
    public string Name
    {
        get { return "radians-as-degrees"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Converts angular fields to radians while leaving them in a field specified in degrees."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.RadiansAsDegrees,
    };

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        EntitySample damaged = sample.Clone();

        if (damaged.Latitude.HasValue)
        {
            damaged.Latitude = damaged.Latitude.Value * Geo.DegreesToRadians;
        }

        if (damaged.Longitude.HasValue)
        {
            damaged.Longitude = damaged.Longitude.Value * Geo.DegreesToRadians;
        }

        if (damaged.HeadingDegrees.HasValue)
        {
            damaged.HeadingDegrees = damaged.HeadingDegrees.Value * Geo.DegreesToRadians;
        }

        if (damaged.YawDegrees.HasValue)
        {
            damaged.YawDegrees = damaged.YawDegrees.Value * Geo.DegreesToRadians;
        }

        return damaged;
    }
}
