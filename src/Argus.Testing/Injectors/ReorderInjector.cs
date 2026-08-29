using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>Swaps adjacent ticks, so samples arrive in the wrong order.</summary>
/// <remarks>
/// <para>
/// A stream injector rather than a sample injector, because reordering is a property of the
/// sequence and cannot be expressed one sample at a time.
/// </para>
/// <para>
/// The swap keeps each tick's own timestamps and sequence numbers, which is what a real
/// reordering transport does: the frames still say when they were produced, they simply
/// arrive in a different order. That is what makes both the non-positive interval and the
/// out-of-order sequence number appear together, and why they are separate flags — the pair
/// says "reordered", while a non-positive interval alone says "the clock went backwards".
/// </para>
/// </remarks>
public sealed class ReorderInjector : IStreamInjector
{
    /// <summary>Creates the injector.</summary>
    /// <param name="swapEveryNthTick">Swap one pair of ticks out of this many.</param>
    public ReorderInjector(int swapEveryNthTick = 4)
    {
        SwapEveryNthTick = swapEveryNthTick < 2 ? 2 : swapEveryNthTick;
    }

    /// <summary>Swap one pair of ticks out of this many.</summary>
    public int SwapEveryNthTick { get; }

    /// <inheritdoc />
    public string Name
    {
        get { return "reorder"; }
    }

    /// <inheritdoc />
    public string Description
    {
        get { return "Swaps adjacent ticks, so samples arrive out of the order they were produced in."; }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthFlags> ExpectedFlags { get; } = new[]
    {
        HealthFlags.OutOfOrderSequence,
        HealthFlags.NonPositiveDeltaTime,
    };

    /// <inheritdoc />
    public IEnumerable<StreamTick> Apply(IEnumerable<StreamTick> ticks)
    {
        StreamTick? held = null;

        foreach (StreamTick tick in ticks)
        {
            if (held != null)
            {
                yield return tick;
                yield return held;
                held = null;
                continue;
            }

            if (tick.Index > 0 && tick.Index % SwapEveryNthTick == 0)
            {
                held = tick;
                continue;
            }

            yield return tick;
        }

        if (held != null)
        {
            yield return held;
        }
    }
}
