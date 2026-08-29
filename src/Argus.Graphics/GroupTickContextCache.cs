using System;
using System.Collections.Generic;
using Argus.Contracts;
using Argus.State;

namespace Argus.Graphics;

/// <summary>
/// Reuses one tick's group statistics across the per-entity calls the facade receives.
/// </summary>
/// <remarks>
/// <para>
/// The compatibility facade is called once per entity and handed the whole collection each
/// time, so building the group statistics on every call would restore exactly the O(n²)
/// behaviour the <c>GroupTickContext</c> design exists to remove. This cache keeps the
/// context built for a collection and reuses it while two things hold: the caller passed the
/// <i>same collection instance</i>, and the sample timestamps have not advanced past the
/// staleness bound.
/// </para>
/// <para>
/// Reference identity is the right key here because it is the one thing that is cheap and
/// cannot be wrong. The failure mode is a miss, not a stale answer: a caller that
/// materialises a fresh list per entity — or passes a LINQ query that re-enumerates —
/// rebuilds every time and gets correct results at the original cost. That caller should use
/// <c>IEntityStreamMonitor</c> directly and build one context per tick, which is what the
/// facade's documentation says.
/// </para>
/// </remarks>
internal sealed class GroupTickContextCache
{
    private readonly object _gate = new object();

    private object? _source;
    private GroupTickContext? _context;
    private DateTime _builtAtUtc;

    internal GroupTickContext GetOrBuild<TEntity>(
        IEnumerable<TEntity>? entities,
        DateTime timestampUtc,
        TimeSpan staleness,
        Func<TEntity, EntitySnapshot> accessor,
        bool treatZeroIslandAsInvalid)
    {
        if (entities == null)
        {
            return GroupTickContext.Empty;
        }

        lock (_gate)
        {
            if (_context != null
                && ReferenceEquals(_source, entities)
                && timestampUtc >= _builtAtUtc
                && (timestampUtc - _builtAtUtc) <= staleness)
            {
                return _context;
            }

            var builder = new GroupTickContextBuilder(timestampUtc, treatZeroIslandAsInvalid);
            foreach (TEntity entity in entities)
            {
                builder.AddSnapshot(accessor(entity));
            }

            _context = builder.Build();
            _source = entities;
            _builtAtUtc = timestampUtc;
            return _context;
        }
    }

    internal void Invalidate()
    {
        lock (_gate)
        {
            _source = null;
            _context = null;
            _builtAtUtc = default(DateTime);
        }
    }
}
