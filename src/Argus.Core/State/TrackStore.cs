using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace Argus.State;

/// <summary>
/// The per-entity state the monitor keeps between samples, bounded and evictable.
/// </summary>
/// <remarks>
/// <para>
/// The prototype held this as a plain <c>Dictionary</c> that was written from the network
/// thread and read from the UI thread, and that never removed anything. Both halves of that
/// are defects. The unsynchronised dictionary is a torn-read and resize race that shows up
/// as an occasional corrupted or missing entry under load, which is indistinguishable from
/// the stream faults the tool exists to find — the diagnostic tool becoming a source of
/// false findings is the worst possible failure mode. The unbounded growth is a leak whose
/// rate is set by how many distinct entity identifiers the stream ever mentions, which for
/// a stream carrying a churning population is unbounded.
/// </para>
/// <para>
/// Both are fixed here: a <see cref="ConcurrentDictionary{TKey, TValue}"/> for the
/// structural safety, and two eviction rules — an idle timeout and a hard capacity — for
/// the growth. Eviction is amortised: it runs every <see cref="EvictionInterval"/> touches
/// rather than on every one, so the common path stays a single dictionary lookup.
/// </para>
/// <para>
/// The store makes structural operations safe. It does <b>not</b> make a single
/// <see cref="EntityTrack"/> safe to mutate from two threads at once; see
/// <c>docs/threading.md</c> for what the caller must guarantee.
/// </para>
/// </remarks>
public sealed class TrackStore
{
    /// <summary>How many touches pass between eviction sweeps.</summary>
    public const int EvictionInterval = 512;

    private readonly ConcurrentDictionary<string, EntityTrack> _tracks =
        new ConcurrentDictionary<string, EntityTrack>(StringComparer.Ordinal);

    private readonly object _evictionGate = new object();
    private readonly int _maxTrackedEntities;
    private readonly TimeSpan _idleTimeout;
    private readonly int _historyCapacity;

    private int _touchesSinceEviction;
    private long _evictionCount;

    /// <summary>Creates a store.</summary>
    /// <param name="maxTrackedEntities">The hard cap on retained entities. Values below one are treated as one.</param>
    /// <param name="idleTimeout">How long an entity may go unmentioned before its state is discarded.</param>
    /// <param name="historyCapacity">How many recent valid positions each track retains.</param>
    public TrackStore(int maxTrackedEntities, TimeSpan idleTimeout, int historyCapacity)
    {
        _maxTrackedEntities = maxTrackedEntities < 1 ? 1 : maxTrackedEntities;
        _idleTimeout = idleTimeout;
        _historyCapacity = historyCapacity;
    }

    /// <summary>How many entities currently have state.</summary>
    public int Count
    {
        get { return _tracks.Count; }
    }

    /// <summary>How many tracks have been evicted since the store was created.</summary>
    public long EvictionCount
    {
        get { return Interlocked.Read(ref _evictionCount); }
    }

    /// <summary>
    /// Returns the state for an entity, creating it if this is the first time the entity has
    /// been seen, and runs an eviction sweep every <see cref="EvictionInterval"/> calls.
    /// </summary>
    /// <param name="entityId">The entity.</param>
    /// <param name="nowUtc">The current time, used for idle eviction.</param>
    /// <returns>The entity's state.</returns>
    public EntityTrack Touch(string entityId, DateTime nowUtc)
    {
        if (entityId == null)
        {
            throw new ArgumentNullException(nameof(entityId));
        }

        EntityTrack track = _tracks.GetOrAdd(entityId, CreateTrack);
        track.LastTouchedUtc = nowUtc;

        if (Interlocked.Increment(ref _touchesSinceEviction) >= EvictionInterval)
        {
            Interlocked.Exchange(ref _touchesSinceEviction, 0);
            Evict(nowUtc);
        }

        return track;
    }

    /// <summary>Returns the state for an entity if it has any.</summary>
    /// <param name="entityId">The entity.</param>
    /// <param name="track">The entity's state, if present.</param>
    /// <returns><c>true</c> if state was found.</returns>
    public bool TryGet(string entityId, out EntityTrack? track)
    {
        return _tracks.TryGetValue(entityId, out track);
    }

    /// <summary>Discards the state for one entity.</summary>
    /// <param name="entityId">The entity.</param>
    /// <returns><c>true</c> if state was present and has been discarded.</returns>
    public bool Forget(string entityId)
    {
        EntityTrack? removed;
        return _tracks.TryRemove(entityId, out removed);
    }

    /// <summary>Discards all state.</summary>
    public void Clear()
    {
        _tracks.Clear();
    }

    /// <summary>
    /// Runs an eviction sweep: first the idle timeout, then the hard capacity.
    /// </summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>How many tracks were discarded.</returns>
    /// <remarks>
    /// Called automatically from <see cref="Touch"/>. Exposed so a host that observes bursts
    /// separated by long silences can sweep on its own schedule rather than waiting for the
    /// next burst to pay for the previous one.
    /// </remarks>
    public int Evict(DateTime nowUtc)
    {
        lock (_evictionGate)
        {
            int evicted = 0;

            if (_idleTimeout > TimeSpan.Zero)
            {
                DateTime cutoff = nowUtc - _idleTimeout;
                foreach (KeyValuePair<string, EntityTrack> pair in _tracks)
                {
                    if (pair.Value.LastTouchedUtc < cutoff)
                    {
                        EntityTrack? removed;
                        if (_tracks.TryRemove(pair.Key, out removed))
                        {
                            evicted++;
                        }
                    }
                }
            }

            int excess = _tracks.Count - _maxTrackedEntities;
            if (excess > 0)
            {
                // Oldest touch first. A full sort is O(n log n), but this runs once every
                // EvictionInterval touches and only when the cap is actually exceeded, which
                // is the shape of a bounded cache rather than a hot path.
                var byAge = new List<KeyValuePair<string, EntityTrack>>(_tracks);
                byAge.Sort(CompareByLastTouched);

                for (int i = 0; i < byAge.Count && excess > 0; i++)
                {
                    EntityTrack? removed;
                    if (_tracks.TryRemove(byAge[i].Key, out removed))
                    {
                        evicted++;
                        excess--;
                    }
                }
            }

            Interlocked.Add(ref _evictionCount, evicted);
            return evicted;
        }
    }

    private static int CompareByLastTouched(KeyValuePair<string, EntityTrack> left, KeyValuePair<string, EntityTrack> right)
    {
        return left.Value.LastTouchedUtc.CompareTo(right.Value.LastTouchedUtc);
    }

    private EntityTrack CreateTrack(string entityId)
    {
        return new EntityTrack(entityId, _historyCapacity);
    }
}
