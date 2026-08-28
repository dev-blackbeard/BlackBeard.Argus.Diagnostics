using System;
using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing.Injectors;

/// <summary>
/// Where a sample sits in the stream, for injectors that damage some samples and not others.
/// </summary>
public sealed class InjectionContext
{
    /// <summary>Creates a context.</summary>
    /// <param name="tickIndex">The tick's ordinal position, from zero.</param>
    /// <param name="sampleIndex">The sample's position within the tick, from zero.</param>
    /// <param name="random">The scenario's seeded random source, so damage is reproducible.</param>
    public InjectionContext(int tickIndex, int sampleIndex, Random random)
    {
        TickIndex = tickIndex;
        SampleIndex = sampleIndex;
        Random = random;
    }

    /// <summary>The tick's ordinal position, from zero.</summary>
    public int TickIndex { get; }

    /// <summary>The sample's position within the tick, from zero.</summary>
    public int SampleIndex { get; }

    /// <summary>The scenario's seeded random source.</summary>
    public Random Random { get; }
}

/// <summary>
/// Damages one sample at a time.
/// </summary>
/// <remarks>
/// <para>
/// The harness exists because a detector tested against a hand-written "bad" sample is
/// really being tested against somebody's belief about what bad looks like. An injector
/// applies the actual transformation the wire performs — reverses the bytes, shifts the
/// offsets, rescales the integer — so what reaches the detector is the fault itself.
/// </para>
/// <para>
/// Every injector declares the flags it expects to provoke, which is what
/// <c>Argus.Golden.Tests</c> locks.
/// </para>
/// </remarks>
public interface ISampleInjector
{
    /// <summary>A name for the injector, used in test output.</summary>
    string Name { get; }

    /// <summary>What this injector does to the stream, in one line.</summary>
    string Description { get; }

    /// <summary>The flags a correct detector catalogue should raise on the damaged samples.</summary>
    IReadOnlyList<HealthFlags> ExpectedFlags { get; }

    /// <summary>Damages a sample.</summary>
    /// <param name="sample">The clean sample. Do not mutate it; clone first.</param>
    /// <param name="context">Where the sample sits in the stream.</param>
    /// <returns>The damaged sample, or <c>null</c> to drop it from the stream.</returns>
    EntitySample? Inject(EntitySample sample, InjectionContext context);
}

/// <summary>
/// Damages a stream as a whole, for faults that are about the sequence rather than a sample.
/// </summary>
/// <remarks>
/// Reordering and loss cannot be expressed one sample at a time: they are properties of the
/// order and completeness of the sequence, which is exactly what makes them interesting.
/// </remarks>
public interface IStreamInjector
{
    /// <summary>A name for the injector, used in test output.</summary>
    string Name { get; }

    /// <summary>What this injector does to the stream, in one line.</summary>
    string Description { get; }

    /// <summary>The flags a correct detector catalogue should raise on the damaged stream.</summary>
    IReadOnlyList<HealthFlags> ExpectedFlags { get; }

    /// <summary>Damages a stream.</summary>
    /// <param name="ticks">The clean stream.</param>
    /// <returns>The damaged stream.</returns>
    IEnumerable<StreamTick> Apply(IEnumerable<StreamTick> ticks);
}
