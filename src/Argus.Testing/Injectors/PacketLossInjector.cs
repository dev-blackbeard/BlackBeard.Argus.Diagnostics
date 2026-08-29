using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>Drops samples from the stream, leaving gaps in the sequence numbering.</summary>
/// <remarks>
/// Loss is only detectable because the producer numbers its frames. Without a sequence
/// number a dropped sample is indistinguishable from an entity that was not reported this
/// tick, which is why the sequence-gap detector reports <c>NotEvaluable</c> rather than
/// "healthy" when the field is absent.
/// </remarks>
public sealed class PacketLossInjector : ISampleInjector
{
    /// <summary>Creates the injector.</summary>
    /// <param name="dropEveryNthTick">Drop the samples in one tick out of this many.</param>
    /// <param name="entityIdFilter">Drop only this entity's samples, or <c>null</c> for all of them.</param>
    public PacketLossInjector(int dropEveryNthTick = 4, string? entityIdFilter = null)
    {
        DropEveryNthTick = dropEveryNthTick < 2 ? 2 : dropEveryNthTick;
        EntityIdFilter = entityIdFilter;
    }

    /// <summary>Drop the samples in one tick out of this many.</summary>
    public int DropEveryNthTick { get; }

    /// <summary>The entity whose samples are dropped, or <c>null</c> for all of them.</summary>
    public string? EntityIdFilter { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return "packet-loss"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Drops samples, leaving the sequence numbering discontinuous."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.SequenceGap,
    };

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        if (EntityIdFilter != null && !string.Equals(EntityIdFilter, sample.EntityId, System.StringComparison.Ordinal))
        {
            return sample;
        }

        // Never drop the first tick: an entity whose very first sample is missing is simply an
        // entity that has not appeared yet, which is not a fault.
        if (context.TickIndex > 0 && context.TickIndex % DropEveryNthTick == 0)
        {
            return null;
        }

        return sample;
    }
}
