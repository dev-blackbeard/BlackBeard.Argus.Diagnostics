using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>Holds an entity's position still while its reported velocity keeps claiming motion.</summary>
/// <remarks>
/// This is what a stuck source looks like from the consumer's side, and the contradiction is
/// the whole signal: the position says stationary, the velocity field says moving, and a
/// consumer that reads only one of them sees nothing wrong. A genuinely stationary entity
/// reports a velocity of zero and is not frozen.
/// </remarks>
public sealed class FreezeInjector : ISampleInjector
{
    private readonly Dictionary<string, EntitySample> _frozen = new Dictionary<string, EntitySample>();

    /// <summary>Creates the injector.</summary>
    /// <param name="fromTickIndex">The tick at which entities stop moving.</param>
    /// <param name="entityIdFilter">Freeze only this entity, or <c>null</c> to freeze all of them.</param>
    public FreezeInjector(int fromTickIndex = 2, string? entityIdFilter = null)
    {
        FromTickIndex = fromTickIndex;
        EntityIdFilter = entityIdFilter;
    }

    /// <summary>The tick at which entities stop moving.</summary>
    public int FromTickIndex { get; }

    /// <summary>The entity frozen, or <c>null</c> for all of them.</summary>
    public string? EntityIdFilter { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return "freeze"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Repeats the last position while leaving reported velocity claiming motion."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.FrozenEntity,
    };

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        if (EntityIdFilter != null && !string.Equals(EntityIdFilter, sample.EntityId, System.StringComparison.Ordinal))
        {
            return sample;
        }

        if (context.TickIndex < FromTickIndex)
        {
            _frozen[sample.EntityId] = sample;
            return sample;
        }

        EntitySample? held;
        if (!_frozen.TryGetValue(sample.EntityId, out held))
        {
            _frozen[sample.EntityId] = sample;
            return sample;
        }

        EntitySample damaged = sample.Clone();
        damaged.Latitude = held.Latitude;
        damaged.Longitude = held.Longitude;
        damaged.Altitude = held.Altitude;
        return damaged;
    }
}
