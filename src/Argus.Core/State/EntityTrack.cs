using System;
using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.State;

/// <summary>
/// One historical position, kept so detectors that need a window rather than a single
/// predecessor have one.
/// </summary>
public readonly struct TrackPoint
{
    /// <summary>Creates a point.</summary>
    /// <param name="timeUtc">Arrival time of the sample.</param>
    /// <param name="latitudeDegrees">Latitude in degrees.</param>
    /// <param name="longitudeDegrees">Longitude in degrees.</param>
    /// <param name="altitude">Altitude in metres, or <c>null</c>.</param>
    public TrackPoint(DateTime timeUtc, double latitudeDegrees, double longitudeDegrees, double? altitude)
    {
        TimeUtc = timeUtc;
        LatitudeDegrees = latitudeDegrees;
        LongitudeDegrees = longitudeDegrees;
        Altitude = altitude;
    }

    /// <summary>Arrival time of the sample.</summary>
    public DateTime TimeUtc { get; }

    /// <summary>Latitude in degrees.</summary>
    public double LatitudeDegrees { get; }

    /// <summary>Longitude in degrees.</summary>
    public double LongitudeDegrees { get; }

    /// <summary>Altitude in metres, or <c>null</c>.</summary>
    public double? Altitude { get; }
}

/// <summary>
/// Everything Argus remembers about one entity between samples.
/// </summary>
/// <remarks>
/// <para>
/// The distinction that matters here is <see cref="LastSeenSample"/> versus
/// <see cref="LastValidSample"/>. The prototype kept one field and updated it from every
/// arrival, including arrivals it had just rejected — so the tick after a <c>(0,0)</c>
/// measured its jump from the origin and fabricated a displacement of thousands of
/// kilometres. Every kinematic comparison here is made against
/// <see cref="LastValidSample"/>; <see cref="LastSeenSample"/> exists so temporal detectors
/// can still reason about what actually arrived.
/// </para>
/// <para>
/// Threading: an instance is mutated only by the monitor, and only while holding that
/// entity's slot. See <c>docs/threading.md</c> for the full contract.
/// </para>
/// </remarks>
public sealed class EntityTrack
{
    private readonly Queue<TrackPoint> _recentPoints = new Queue<TrackPoint>();
    private int _historyCapacity;

    /// <summary>Creates a track.</summary>
    /// <param name="entityId">The entity this track belongs to.</param>
    /// <param name="historyCapacity">How many recent valid positions to retain.</param>
    public EntityTrack(string entityId, int historyCapacity)
    {
        EntityId = entityId;
        _historyCapacity = historyCapacity < 1 ? 1 : historyCapacity;
    }

    /// <summary>The entity this track belongs to.</summary>
    public string EntityId { get; }

    /// <summary>
    /// The previous sample that arrived, whether or not it was usable.
    /// </summary>
    /// <remarks>
    /// Temporal detectors compare against this: whether a sample duplicated its
    /// predecessor, or arrived out of order, is a fact about arrivals and has nothing to do
    /// with whether the position in them was usable.
    /// </remarks>
    public EntitySample? LastSeenSample { get; internal set; }

    /// <summary>
    /// The most recent sample whose position was usable.
    /// </summary>
    /// <remarks>
    /// Kinematic and group detectors compare against this. It is not necessarily the
    /// previous arrival, which is exactly the point.
    /// </remarks>
    public EntitySample? LastValidSample { get; internal set; }

    /// <summary>The speed derived from the two most recent valid samples, in metres per second.</summary>
    public double? LastDerivedSpeedMetersPerSecond { get; internal set; }

    /// <summary>How many samples have arrived for this entity, including unusable ones.</summary>
    public long SamplesObserved { get; internal set; }

    /// <summary>
    /// How many samples were actually evaluated.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SamplesObserved"/> because the prototype conflated them: it
    /// counted an arrival and then bailed out of evaluation, so a stale or duplicated
    /// sample quietly reduced the reported health percentage without producing a finding
    /// that explained why. A percentage that moves without a finding behind it is not
    /// evidence, it is noise.
    /// </remarks>
    public long SamplesEvaluated { get; internal set; }

    /// <summary>How many evaluated samples raised at least one flag.</summary>
    public long SamplesFlagged { get; internal set; }

    /// <summary>How many samples were rejected as having an unusable position.</summary>
    public long SamplesRejected { get; internal set; }

    /// <summary>The highest sequence number observed for this entity, if the protocol carries one.</summary>
    public long? HighestSequenceNumber { get; internal set; }

    /// <summary>When this track was last touched, in UTC. Drives idle eviction.</summary>
    public DateTime LastTouchedUtc { get; internal set; }

    /// <summary>
    /// An exponentially weighted mean of the interval between arrivals, in seconds.
    /// </summary>
    /// <remarks>
    /// Held as a moving mean rather than a window so update-rate drift can be assessed at
    /// O(1) cost per sample and constant memory, which matters when the number of entities
    /// is the thing that scales.
    /// </remarks>
    public double? MeanUpdateIntervalSeconds { get; internal set; }

    /// <summary>How many consecutive valid samples reported a position that had not moved.</summary>
    public int StaticSampleRun { get; internal set; }

    /// <summary>The recent valid positions, oldest first.</summary>
    public IEnumerable<TrackPoint> RecentPoints
    {
        get { return _recentPoints; }
    }

    /// <summary>How many recent valid positions are retained.</summary>
    public int RecentPointCount
    {
        get { return _recentPoints.Count; }
    }

    /// <summary>Resizes the retained history.</summary>
    /// <param name="capacity">The new capacity. Values below one are treated as one.</param>
    internal void SetHistoryCapacity(int capacity)
    {
        _historyCapacity = capacity < 1 ? 1 : capacity;
        Trim();
    }

    /// <summary>Appends a valid position to the retained history.</summary>
    /// <param name="point">The position.</param>
    internal void RecordPoint(TrackPoint point)
    {
        _recentPoints.Enqueue(point);
        Trim();
    }

    /// <summary>Copies the retained history into an array, oldest first.</summary>
    /// <returns>The retained positions.</returns>
    public TrackPoint[] SnapshotRecentPoints()
    {
        return _recentPoints.ToArray();
    }

    private void Trim()
    {
        while (_recentPoints.Count > _historyCapacity)
        {
            _recentPoints.Dequeue();
        }
    }
}
