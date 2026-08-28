using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>Transposes latitude and longitude.</summary>
/// <remarks>
/// Trivial to describe, genuinely hard to detect. When both values are small the transposed
/// pair is a perfectly ordinary position, and nothing about the sample in isolation says
/// which of the two orderings was intended. The evidence that resolves it is the group: the
/// transposed pair being near the group centroid, and the pair as read not being, is an
/// argument. A latitude beyond ninety degrees is a much easier case and a much rarer one.
/// </remarks>
public sealed class AxisSwapInjector : ISampleInjector
{
    /// <summary>Creates the injector.</summary>
    /// <param name="everyNthTick">Damage one tick in this many. One damages every tick.</param>
    public AxisSwapInjector(int everyNthTick = 1)
    {
        EveryNthTick = everyNthTick < 1 ? 1 : everyNthTick;
    }

    /// <summary>Damage one tick in this many.</summary>
    public int EveryNthTick { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return "axis-swap"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Transposes latitude and longitude."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.AxisSwap,
    };

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        if (context.TickIndex % EveryNthTick != 0)
        {
            return sample;
        }

        EntitySample damaged = sample.Clone();
        double? latitude = damaged.Latitude;
        damaged.Latitude = damaged.Longitude;
        damaged.Longitude = latitude;
        return damaged;
    }
}
