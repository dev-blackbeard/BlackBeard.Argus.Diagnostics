using System;
using Argus.Configuration;
using Argus.Contracts;
using Argus.State;

namespace Argus.Detectors;

/// <summary>
/// Everything a detector is given about one sample.
/// </summary>
/// <remarks>
/// Built once per observation and handed to every detector, so no detector recomputes what
/// another already worked out — the elapsed interval and the position's usability in
/// particular, both of which the prototype recalculated inline and inconsistently.
/// </remarks>
public sealed class DetectorContext
{
    /// <summary>Creates a context.</summary>
    /// <param name="sample">The sample under evaluation.</param>
    /// <param name="track">The entity's state as it was <i>before</i> this sample was applied.</param>
    /// <param name="group">The tick's group statistics, or <c>null</c> if the caller supplied none.</param>
    /// <param name="thresholds">The thresholds in force for this observation.</param>
    /// <param name="deltaTimeSeconds">Seconds since the previous arrival, or <c>null</c> if this is the first.</param>
    /// <param name="validDeltaTimeSeconds">Seconds since the previous <i>valid</i> sample, or <c>null</c> if there was none.</param>
    /// <param name="positionIsUsable">Whether this sample's position may be used as a measurement.</param>
    public DetectorContext(
        EntitySample sample,
        EntityTrack track,
        GroupTickContext? group,
        DetectorThresholds thresholds,
        double? deltaTimeSeconds,
        double? validDeltaTimeSeconds,
        bool positionIsUsable)
    {
        Sample = sample;
        Track = track;
        Group = group;
        Thresholds = thresholds;
        DeltaTimeSeconds = deltaTimeSeconds;
        ValidDeltaTimeSeconds = validDeltaTimeSeconds;
        PositionIsUsable = positionIsUsable;
    }

    /// <summary>The sample under evaluation.</summary>
    public EntitySample Sample { get; }

    /// <summary>
    /// The entity's state as it was before this sample was applied.
    /// </summary>
    /// <remarks>
    /// Detectors see the <i>previous</i> state on purpose: comparing a sample against a
    /// history that already includes it is how a jump detector ends up measuring zero.
    /// </remarks>
    public EntityTrack Track { get; }

    /// <summary>The tick's group statistics, or <c>null</c> if the caller supplied none.</summary>
    public GroupTickContext? Group { get; }

    /// <summary>The thresholds in force for this observation.</summary>
    public DetectorThresholds Thresholds { get; }

    /// <summary>
    /// Seconds since the previous arrival for this entity, whether or not that arrival was
    /// usable, or <c>null</c> if this is the first sample.
    /// </summary>
    public double? DeltaTimeSeconds { get; }

    /// <summary>
    /// Seconds since the previous <i>valid</i> sample for this entity, or <c>null</c> if
    /// there has not been one.
    /// </summary>
    /// <remarks>
    /// Kinematic detectors divide by this rather than by <see cref="DeltaTimeSeconds"/>. If
    /// three of the last four samples were unusable, the entity's real displacement happened
    /// over four intervals, and dividing it by one produces a speed four times too high — a
    /// fabricated finding produced by the diagnostic tool itself.
    /// </remarks>
    public double? ValidDeltaTimeSeconds { get; }

    /// <summary>Whether this sample's position may be used as a measurement.</summary>
    public bool PositionIsUsable { get; }

    /// <summary>The previous valid sample, or <c>null</c> if there has not been one.</summary>
    public EntitySample? PreviousValidSample
    {
        get { return Track.LastValidSample; }
    }

    /// <summary>The previous arrival, valid or not, or <c>null</c> if this is the first.</summary>
    public EntitySample? PreviousSeenSample
    {
        get { return Track.LastSeenSample; }
    }

    /// <summary>
    /// The distance between the previous valid position and this one, in metres, or
    /// <c>null</c> if either is unavailable.
    /// </summary>
    /// <remarks>Computed on demand and not cached; the kinematic detectors that use it are few.</remarks>
    public double? DistanceFromPreviousValidMeters()
    {
        EntitySample? previous = PreviousValidSample;
        if (previous == null || !PositionIsUsable || !previous.Latitude.HasValue || !previous.Longitude.HasValue
            || !Sample.Latitude.HasValue || !Sample.Longitude.HasValue)
        {
            return null;
        }

        double distance = Argus.Geodesy.Geo.DistanceMeters(
            previous.Latitude.Value,
            previous.Longitude.Value,
            Sample.Latitude.Value,
            Sample.Longitude.Value);

        return Argus.Geodesy.Geo.IsFinite(distance) ? distance : (double?)null;
    }

    /// <summary>
    /// The speed implied by the previous valid position and this one, in metres per second,
    /// or <c>null</c> if it cannot be derived.
    /// </summary>
    public double? DerivedSpeedMetersPerSecond()
    {
        double? distance = DistanceFromPreviousValidMeters();
        if (!distance.HasValue || !ValidDeltaTimeSeconds.HasValue || ValidDeltaTimeSeconds.Value <= 0.0)
        {
            return null;
        }

        return distance.Value / ValidDeltaTimeSeconds.Value;
    }

    /// <summary>The reported speed over the ground, in metres per second, or <c>null</c> if velocity was not supplied.</summary>
    public double? ReportedGroundSpeedMetersPerSecond()
    {
        double? north = Sample.VelocityNorthMetersPerSecond;
        double? east = Sample.VelocityEastMetersPerSecond;
        if (!north.HasValue || !east.HasValue)
        {
            return null;
        }

        return Math.Sqrt((north.Value * north.Value) + (east.Value * east.Value));
    }
}
