using System.Collections.Generic;

namespace Argus.Testing;

/// <summary>
/// Something that produces a stream of ticks.
/// </summary>
/// <remarks>
/// Implemented by the synthetic generator, by the injectors that wrap it, and — in a
/// consuming application — by whatever taps the real stream. That shared shape is what lets
/// a detector be tested against a synthetic fault and then run unchanged against a capture.
/// </remarks>
public interface IEntityStreamSource
{
    /// <summary>A name for the source, used in test output.</summary>
    string Name { get; }

    /// <summary>Produces the ticks.</summary>
    /// <returns>The stream, which may be lazy.</returns>
    IEnumerable<StreamTick> Read();
}
