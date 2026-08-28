using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>The value a field carries when nobody has written a measurement into it.</summary>
public enum SentinelKind
{
    /// <summary>Exact zero — a zeroed buffer.</summary>
    Zero = 0,

    /// <summary>Minus one — the conventional "no value" for a signed field.</summary>
    MinusOne = 1,

    /// <summary>The largest representable double — an uninitialised maximum.</summary>
    MaxValue = 2,

    /// <summary>All bits set, which as a double is NaN.</summary>
    AllBitsSet = 3,
}

/// <summary>Replaces position fields with values characteristic of uninitialised memory.</summary>
/// <remarks>
/// <see cref="SentinelKind.Zero"/> is the interesting one, and the reason
/// <c>DetectorThresholds.TreatZeroIslandAsSentinel</c> exists: <c>(0,0)</c> is simultaneously a
/// legal position and the commonest filler value in the world, and nothing about the value
/// itself distinguishes the two cases.
/// </remarks>
public sealed class SentinelInjector : ISampleInjector
{
    /// <summary>Creates the injector.</summary>
    /// <param name="kind">Which sentinel to write.</param>
    /// <param name="everyNthTick">Damage one tick in this many.</param>
    public SentinelInjector(SentinelKind kind = SentinelKind.Zero, int everyNthTick = 5)
    {
        Kind = kind;
        EveryNthTick = everyNthTick < 1 ? 1 : everyNthTick;
    }

    /// <summary>Which sentinel is written.</summary>
    public SentinelKind Kind { get; }

    /// <summary>Damage one tick in this many.</summary>
    public int EveryNthTick { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return "sentinel-" + Kind.ToString().ToLowerInvariant(); }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Replaces the position with a value characteristic of uninitialised or filler memory."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags
    {
        get
        {
            return Kind == SentinelKind.AllBitsSet
                ? new[] { HealthFlags.SentinelValue, HealthFlags.NonFiniteValue }
                : new[] { HealthFlags.SentinelValue };
        }
    }

    /// <inheritdoc />
    public EntitySample? Inject(EntitySample sample, InjectionContext context)
    {
        if (context.TickIndex % EveryNthTick != 0)
        {
            return sample;
        }

        double value = Value();
        EntitySample damaged = sample.Clone();
        damaged.Latitude = value;
        damaged.Longitude = value;
        damaged.Altitude = value;
        return damaged;
    }

    private double Value()
    {
        switch (Kind)
        {
            case SentinelKind.MinusOne:
                return -1.0;
            case SentinelKind.MaxValue:
                return double.MaxValue;
            case SentinelKind.AllBitsSet:
                return System.BitConverter.Int64BitsToDouble(-1L);
            default:
                return 0.0;
        }
    }
}
