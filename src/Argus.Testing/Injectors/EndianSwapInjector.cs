using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>Reads position fields in the opposite byte order.</summary>
/// <remarks>
/// The classic producer/consumer disagreement. What makes it worth a detector rather than a
/// code review is that the result is not obviously wrong: a byte-swapped double is usually
/// either enormous, tiny, or NaN, and the last two render as a position at the origin.
/// </remarks>
public sealed class EndianSwapInjector : ISampleInjector
{
    /// <summary>Creates the injector.</summary>
    /// <param name="everyNthTick">Damage one tick in this many. One damages every tick.</param>
    public EndianSwapInjector(int everyNthTick = 1)
    {
        EveryNthTick = everyNthTick < 1 ? 1 : everyNthTick;
    }

    /// <summary>Damage one tick in this many.</summary>
    public int EveryNthTick { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return "endian-swap"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Reverses the bytes of latitude, longitude and altitude, as a byte-order mismatch between producer and consumer does."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.ByteOrderSwap,
    };

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        if (context.TickIndex % EveryNthTick != 0)
        {
            return sample;
        }

        EntitySample damaged = sample.Clone();

        if (damaged.Latitude.HasValue)
        {
            damaged.Latitude = ByteLevel.SwapEndian(damaged.Latitude.Value);
        }

        if (damaged.Longitude.HasValue)
        {
            damaged.Longitude = ByteLevel.SwapEndian(damaged.Longitude.Value);
        }

        if (damaged.Altitude.HasValue)
        {
            damaged.Altitude = ByteLevel.SwapEndian(damaged.Altitude.Value);
        }

        return damaged;
    }
}
