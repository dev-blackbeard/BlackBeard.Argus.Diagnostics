using System;
using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Testing;

/// <summary>
/// Every sample that belongs to one moment in the stream.
/// </summary>
/// <remarks>
/// The stream is modelled as a sequence of ticks rather than a flat sequence of samples
/// because the group checks are per tick: they need to know which samples are contemporaries.
/// A flat sequence forces the consumer to reconstruct that grouping, usually by timestamp,
/// usually slightly wrong.
/// </remarks>
public sealed class StreamTick
{
    /// <summary>Creates a tick.</summary>
    /// <param name="index">The tick's ordinal position in the stream, from zero.</param>
    /// <param name="timeUtc">The time the tick represents.</param>
    /// <param name="samples">The samples in the tick.</param>
    public StreamTick(int index, DateTime timeUtc, IReadOnlyList<EntitySample> samples)
    {
        Index = index;
        TimeUtc = timeUtc;
        Samples = samples;
    }

    /// <summary>The tick's ordinal position in the stream, from zero.</summary>
    public int Index { get; }

    /// <summary>The time the tick represents.</summary>
    public DateTime TimeUtc { get; }

    /// <summary>The samples in the tick.</summary>
    public IReadOnlyList<EntitySample> Samples { get; }
}
