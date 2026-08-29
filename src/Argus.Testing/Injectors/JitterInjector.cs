using System;
using System.Collections.Generic;
using Argus.Contracts;
using Argus.Geodesy;

namespace Argus.Testing.Injectors;

/// <summary>Adds a small random offset to each position.</summary>
/// <remarks>
/// Dither rather than displacement: the offsets are symmetric about zero, so the entity's
/// mean path is unchanged and every individual position is plausible. What the detector has
/// to notice is that successive displacements cancel instead of accumulating.
/// </remarks>
public sealed class JitterInjector : ISampleInjector
{
    /// <summary>Creates the injector.</summary>
    /// <param name="amplitudeMeters">The largest offset applied on each axis, in metres.</param>
    public JitterInjector(double amplitudeMeters = 25.0)
    {
        AmplitudeMeters = amplitudeMeters;
    }

    /// <summary>The largest offset applied on each axis, in metres.</summary>
    public double AmplitudeMeters { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return "jitter"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Adds a symmetric random offset to each position, so the entity dithers about its true path."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.Jitter,
    };

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        if (!sample.Latitude.HasValue || !sample.Longitude.HasValue)
        {
            return sample;
        }

        double metersPerDegreeLatitude = Geo.EarthRadiusMeters * Geo.DegreesToRadians;
        double northMeters = ((context.Random.NextDouble() * 2.0) - 1.0) * AmplitudeMeters;
        double eastMeters = ((context.Random.NextDouble() * 2.0) - 1.0) * AmplitudeMeters;

        double latitude = sample.Latitude.Value + (northMeters / metersPerDegreeLatitude);
        double cosLatitude = Math.Cos(latitude * Geo.DegreesToRadians);
        if (Math.Abs(cosLatitude) < 1e-9)
        {
            cosLatitude = 1e-9;
        }

        EntitySample damaged = sample.Clone();
        damaged.Latitude = latitude;
        damaged.Longitude = Geo.NormaliseLongitude(
            sample.Longitude.Value + (eastMeters / (metersPerDegreeLatitude * cosLatitude)));

        return damaged;
    }
}
