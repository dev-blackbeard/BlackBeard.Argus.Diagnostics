using System;
using System.Collections.Generic;

namespace Argus.Testing.Injectors;

/// <summary>
/// Wraps a source and applies injectors to what it produces.
/// </summary>
/// <remarks>
/// Sample injectors run first, in the order given, then stream injectors. That order is not
/// arbitrary: a stream injector that drops or reorders ticks must see the damage the sample
/// injectors did, or a dropped sample would take its own corruption with it and the expected
/// flags would depend on evaluation order.
/// </remarks>
public sealed class InjectedStreamSource : IEntityStreamSource
{
    private readonly IEntityStreamSource _inner;
    private readonly IReadOnlyList<ISampleInjector> _sampleInjectors;
    private readonly IReadOnlyList<IStreamInjector> _streamInjectors;
    private readonly int _seed;

    /// <summary>Creates a wrapped source.</summary>
    /// <param name="inner">The clean source.</param>
    /// <param name="seed">The seed for randomised injectors, so damage is reproducible.</param>
    /// <param name="sampleInjectors">Injectors applied per sample.</param>
    /// <param name="streamInjectors">Injectors applied to the whole stream.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <c>null</c>.</exception>
    public InjectedStreamSource(
        IEntityStreamSource inner,
        int seed,
        IReadOnlyList<ISampleInjector>? sampleInjectors = null,
        IReadOnlyList<IStreamInjector>? streamInjectors = null)
    {
        if (inner == null)
        {
            throw new ArgumentNullException(nameof(inner));
        }

        _inner = inner;
        _seed = seed;
        _sampleInjectors = sampleInjectors ?? new List<ISampleInjector>().AsReadOnly();
        _streamInjectors = streamInjectors ?? new List<IStreamInjector>().AsReadOnly();
    }

    /// <inheritdoc />
    public string Name
    {
        get
        {
            var parts = new List<string> { _inner.Name };
            for (int i = 0; i < _sampleInjectors.Count; i++)
            {
                parts.Add(_sampleInjectors[i].Name);
            }

            for (int i = 0; i < _streamInjectors.Count; i++)
            {
                parts.Add(_streamInjectors[i].Name);
            }

            return string.Join("+", parts.ToArray());
        }
    }

    /// <inheritdoc />
    public IEnumerable<StreamTick> Read()
    {
        IEnumerable<StreamTick> stream = ApplySampleInjectors(_inner.Read());

        for (int i = 0; i < _streamInjectors.Count; i++)
        {
            stream = _streamInjectors[i].Apply(stream);
        }

        return stream;
    }

    private IEnumerable<StreamTick> ApplySampleInjectors(IEnumerable<StreamTick> ticks)
    {
        // One Random for the whole run, seeded once, so a scenario replays identically.
        var random = new Random(_seed);

        foreach (StreamTick tick in ticks)
        {
            var samples = new List<Argus.Contracts.EntitySample>(tick.Samples.Count);

            for (int sampleIndex = 0; sampleIndex < tick.Samples.Count; sampleIndex++)
            {
                Argus.Contracts.EntitySample? sample = tick.Samples[sampleIndex];

                for (int injectorIndex = 0; injectorIndex < _sampleInjectors.Count && sample != null; injectorIndex++)
                {
                    var context = new InjectionContext(tick.Index, sampleIndex, random);
                    sample = _sampleInjectors[injectorIndex].Inject(sample, context);
                }

                if (sample != null)
                {
                    samples.Add(sample);
                }
            }

            yield return new StreamTick(tick.Index, tick.TimeUtc, samples.AsReadOnly());
        }
    }
}
