using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>Narrows position fields through 32-bit floating point.</summary>
/// <remarks>
/// The fault this reproduces is a pipeline stage somewhere that stores a position in a
/// <c>float</c>. The position stays correct to within metres and stops being correct to
/// within centimetres, so nothing that checks plausibility notices, and everything that
/// derives velocity from consecutive positions starts producing staircases.
/// </remarks>
public sealed class QuantisationCollapseInjector : ISampleInjector
{
    /// <summary>Creates the injector.</summary>
    /// <param name="fromTickIndex">The tick at which the narrowing starts, so the coarsening is a visible transition.</param>
    public QuantisationCollapseInjector(int fromTickIndex = 0)
    {
        FromTickIndex = fromTickIndex;
    }

    /// <summary>The tick at which the narrowing starts.</summary>
    public int FromTickIndex { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return "quantisation-collapse"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Round-trips the position through 32-bit floating point, coarsening its resolution."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.QuantisationCollapse,
    };

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        if (context.TickIndex < FromTickIndex)
        {
            return sample;
        }

        EntitySample damaged = sample.Clone();

        if (damaged.Latitude.HasValue)
        {
            damaged.Latitude = ByteLevel.NarrowToSingle(damaged.Latitude.Value);
        }

        if (damaged.Longitude.HasValue)
        {
            damaged.Longitude = ByteLevel.NarrowToSingle(damaged.Longitude.Value);
        }

        if (damaged.Altitude.HasValue)
        {
            damaged.Altitude = ByteLevel.NarrowToSingle(damaged.Altitude.Value);
        }

        return damaged;
    }
}
