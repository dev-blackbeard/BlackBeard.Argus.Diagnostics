using System;
using System.Collections.Generic;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Detectors;
using Argus.Geodesy;
using Argus.State;

namespace Argus.Pipeline;

/// <summary>
/// The reference implementation of <see cref="IEntityStreamMonitor"/>.
/// </summary>
/// <remarks>
/// <para>
/// One <c>Observe</c> call is: resolve the entity's state, work out the two intervals and
/// whether the position is usable, run every detector, then update state. The order matters
/// — detectors see the state as it was <i>before</i> this sample, because a detector
/// comparing a sample against a history that already contains it measures nothing.
/// </para>
/// <para>
/// State is updated from valid samples only. That single rule is what stops the tick after
/// an unusable position from fabricating a jump, which the prototype did every time.
/// </para>
/// <para>
/// Threading: see <c>docs/threading.md</c>. The store is safe for concurrent structural
/// access; one entity must not be observed from two threads at once.
/// </para>
/// </remarks>
public sealed class EntityHealthMonitor : IEntityStreamMonitor
{
    private static readonly IReadOnlyList<HealthFinding> NoFindings = new List<HealthFinding>().AsReadOnly();

    private readonly IReadOnlyList<IDetector> _detectors;

    /// <summary>Creates a monitor with the full catalogue and default options.</summary>
    public EntityHealthMonitor()
        : this(new MonitorOptions(), null)
    {
    }

    /// <summary>Creates a monitor with the full catalogue.</summary>
    /// <param name="options">How the monitor behaves. Defaults are used when <c>null</c>.</param>
    public EntityHealthMonitor(MonitorOptions? options)
        : this(options, null)
    {
    }

    /// <summary>Creates a monitor.</summary>
    /// <param name="options">How the monitor behaves. Defaults are used when <c>null</c>.</param>
    /// <param name="detectors">
    /// The detectors to run. The full catalogue is used when <c>null</c>. Supplying a subset
    /// is how a host runs, say, only the encoding category.
    /// </param>
    public EntityHealthMonitor(MonitorOptions? options, IEnumerable<IDetector>? detectors)
    {
        Options = options ?? new MonitorOptions();

        var selected = new List<IDetector>();
        IEnumerable<IDetector> source = detectors ?? DetectorCatalogue.CreateAll();
        foreach (IDetector detector in source)
        {
            if (detector == null || Options.DisabledDetectors.Contains(detector.Id))
            {
                continue;
            }

            selected.Add(detector);
        }

        _detectors = selected.AsReadOnly();

        Tracks = new TrackStore(
            Options.MaxTrackedEntities,
            Options.TrackIdleTimeout,
            Options.TrackHistoryCapacity);
    }

    /// <inheritdoc />
    public MonitorOptions Options { get; }

    /// <inheritdoc />
    public TrackStore Tracks { get; }

    /// <summary>The detectors this monitor runs, in catalogue order.</summary>
    public IReadOnlyList<IDetector> Detectors
    {
        get { return _detectors; }
    }

    /// <inheritdoc />
    public GroupTickContext CreateTickContext(IEnumerable<EntitySample> samples, DateTime tickTimeUtc)
    {
        return GroupTickContext.FromSamples(samples, tickTimeUtc, Options.Thresholds.TreatZeroIslandAsSentinel);
    }

    /// <inheritdoc />
    public EntityHealthReport Observe(EntitySample sample, GroupTickContext? group = null, DetectorThresholds? thresholds = null)
    {
        if (sample == null)
        {
            throw new ArgumentNullException(nameof(sample));
        }

        DetectorThresholds effectiveThresholds = thresholds ?? Options.Thresholds;
        EntityTrack track = Tracks.Touch(sample.EntityId, sample.ArrivalTimeUtc);

        track.SamplesObserved++;

        double? deltaTimeSeconds = null;
        if (track.LastSeenSample != null)
        {
            deltaTimeSeconds = (sample.ArrivalTimeUtc - track.LastSeenSample.ArrivalTimeUtc).TotalSeconds;
        }

        double? validDeltaTimeSeconds = null;
        if (track.LastValidSample != null)
        {
            validDeltaTimeSeconds = (sample.ArrivalTimeUtc - track.LastValidSample.ArrivalTimeUtc).TotalSeconds;
        }

        bool positionIsUsable = PositionValidity.IsUsable(
            sample.Latitude,
            sample.Longitude,
            effectiveThresholds.TreatZeroIslandAsSentinel);

        var context = new DetectorContext(
            sample,
            track,
            group,
            effectiveThresholds,
            deltaTimeSeconds,
            validDeltaTimeSeconds,
            positionIsUsable);

        var findings = new List<HealthFinding>();
        HealthFlags flagged = HealthFlags.None;

        for (int i = 0; i < _detectors.Count; i++)
        {
            IDetector detector = _detectors[i];

            if (detector.Status == DetectorStatus.NotImplemented)
            {
                // Never called: NotImplementedDetector.Evaluate throws by design. Surfacing it
                // as NotEvaluable when asked keeps the gap visible without pretending to check.
                if (Options.IncludeUnimplementedDetectors)
                {
                    findings.Add(HealthFinding.NotEvaluable(
                        detector.Flag,
                        detector.Id,
                        "this detector is declared in the catalogue but not yet implemented"));
                }

                continue;
            }

            DetectorResult result = detector.Evaluate(context);
            HealthFinding finding = result.Finding;
            if (finding == null)
            {
                continue;
            }

            // Architecture rule 4: no detector's result suppresses another's. Every detector
            // runs and every outcome is recorded, so a jump and an outlier are both reported
            // rather than the first one winning.
            if (finding.Outcome == DetectorOutcome.Flagged)
            {
                flagged |= finding.Flag;
                findings.Add(finding);
            }
            else if (finding.Outcome == DetectorOutcome.NotEvaluable)
            {
                findings.Add(finding);
            }
            else if (Options.IncludeHealthyFindings)
            {
                findings.Add(finding);
            }
        }

        bool evaluated = !deltaTimeSeconds.HasValue || deltaTimeSeconds.Value > 0.0;
        if (evaluated)
        {
            track.SamplesEvaluated++;
            if (flagged != HealthFlags.None)
            {
                track.SamplesFlagged++;
            }
        }

        UpdateState(track, sample, positionIsUsable, deltaTimeSeconds, validDeltaTimeSeconds, effectiveThresholds);

        return new EntityHealthReport(
            sample.EntityId,
            sample.ArrivalTimeUtc,
            findings.Count == 0 ? NoFindings : findings.AsReadOnly(),
            track.SamplesObserved,
            track.SamplesEvaluated,
            track.SamplesFlagged);
    }

    /// <inheritdoc />
    public bool TryGetTrack(string entityId, out EntityTrack? track)
    {
        return Tracks.TryGet(entityId, out track);
    }

    /// <inheritdoc />
    public bool Forget(string entityId)
    {
        return Tracks.Forget(entityId);
    }

    /// <inheritdoc />
    public void Reset()
    {
        Tracks.Clear();
    }

    private static void UpdateState(
        EntityTrack track,
        EntitySample sample,
        bool positionIsUsable,
        double? deltaTimeSeconds,
        double? validDeltaTimeSeconds,
        DetectorThresholds thresholds)
    {
        bool movesForward = !deltaTimeSeconds.HasValue || deltaTimeSeconds.Value > 0.0;

        if (movesForward)
        {
            if (deltaTimeSeconds.HasValue && deltaTimeSeconds.Value > 0.0)
            {
                // An exponentially weighted mean, so cadence can be assessed at O(1) cost and
                // constant memory per entity. The weight is a half, which is a two-sample time
                // constant: fast enough to notice a cadence change within a few samples,
                // damped enough that one late frame does not read as one.
                track.MeanUpdateIntervalSeconds = track.MeanUpdateIntervalSeconds.HasValue
                    ? (0.5 * track.MeanUpdateIntervalSeconds.Value) + (0.5 * deltaTimeSeconds.Value)
                    : deltaTimeSeconds.Value;
            }

            if (sample.SequenceNumber.HasValue)
            {
                long sequence = sample.SequenceNumber.Value;
                if (!track.HighestSequenceNumber.HasValue || sequence > track.HighestSequenceNumber.Value)
                {
                    track.HighestSequenceNumber = sequence;
                }
            }
        }

        if (positionIsUsable && movesForward)
        {
            EntitySample? previousValid = track.LastValidSample;

            if (previousValid != null && previousValid.Latitude.HasValue && previousValid.Longitude.HasValue)
            {
                // sample's own non-null-ness comes from the positionIsUsable guard on the
                // outer if, not from a check the compiler can see right here.
                double distance = Geo.DistanceMeters(
                    previousValid.Latitude.Value,
                    previousValid.Longitude.Value,
                    sample.Latitude!.Value,
                    sample.Longitude!.Value);

                if (Geo.IsFinite(distance))
                {
                    track.StaticSampleRun = distance <= thresholds.StaticPositionEpsilonMeters
                        ? track.StaticSampleRun + 1
                        : 0;

                    if (validDeltaTimeSeconds.HasValue && validDeltaTimeSeconds.Value > 0.0)
                    {
                        track.LastDerivedSpeedMetersPerSecond = distance / validDeltaTimeSeconds.Value;
                    }
                }
            }

            track.RecordPoint(new TrackPoint(
                sample.ArrivalTimeUtc,
                sample.Latitude!.Value,
                sample.Longitude!.Value,
                sample.Altitude));

            // The one rule that fixes the fabricated-jump defect: only a usable sample becomes
            // the reference the next sample is measured against.
            track.LastValidSample = sample;
        }
        else if (!positionIsUsable)
        {
            track.SamplesRejected++;
        }

        // Always updated, valid or not: what arrived is a fact about arrivals, and the
        // temporal detectors need it even when the position in it was unusable.
        track.LastSeenSample = sample;
    }
}
