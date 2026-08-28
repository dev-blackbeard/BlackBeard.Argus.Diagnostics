using System;
using System.Collections.Generic;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Detectors;
using Argus.Pipeline;
using Argus.State;
using Xunit;

namespace Argus.Core.Tests;

/// <summary>
/// One test per defect in the prototype, named after what actually went wrong.
/// </summary>
/// <remarks>
/// These are regression tests in the strict sense: each one failed against the prototype's
/// behaviour and passes against this library's. Treat a failure here as a report that a
/// specific, previously diagnosed fault has come back, not as a test that needs adjusting.
/// </remarks>
public sealed class PrototypeDefectTests
{
    // Defect 1: the dt <= 0 guard early-returned AFTER incrementing the sample counter, so a
    // stale or duplicated arrival deflated the health percentage while raising no flag of its
    // own. The percentage moved and nothing explained why.
    [Fact]
    public void Defect1_StaleArrival_RaisesItsOwnFlag_AndDoesNotSilentlyDeflateHealth()
    {
        var monitor = new EntityHealthMonitor(TestStream.Options());

        monitor.Observe(TestStream.Sample("a", 0.0, 0.001, 0.001));
        EntityHealthReport stale = monitor.Observe(TestStream.Sample("a", -1.0, 0.002, 0.002));

        Assert.True(
            (stale.Flags & HealthFlags.NonPositiveDeltaTime) != HealthFlags.None,
            "a sample arriving before its predecessor must raise NonPositiveDeltaTime, not vanish");

        // Observed counts the arrival; evaluated does not. That split is what stops the health
        // percentage moving without a finding behind it.
        Assert.Equal(2L, stale.SamplesObserved);
        Assert.Equal(1L, stale.SamplesEvaluated);
    }

    [Fact]
    public void Defect1_DuplicateArrival_IsAFirstClassFinding()
    {
        var monitor = new EntityHealthMonitor(TestStream.Options());

        monitor.Observe(TestStream.Sample("a", 0.0, 0.001, 0.001));
        EntityHealthReport duplicate = monitor.Observe(TestStream.Sample("a", 1.0, 0.001, 0.001));

        Assert.True((duplicate.Flags & HealthFlags.DuplicateSample) != HealthFlags.None);
    }

    [Fact]
    public void Defect1_OutOfOrderSequence_IsAFirstClassFinding()
    {
        var monitor = new EntityHealthMonitor(TestStream.Options());

        monitor.Observe(TestStream.Sample("a", 0.0, 0.001, 0.001, sequenceNumber: 10));
        EntityHealthReport reordered = monitor.Observe(TestStream.Sample("a", 1.0, 0.002, 0.002, sequenceNumber: 9));

        Assert.True((reordered.Flags & HealthFlags.OutOfOrderSequence) != HealthFlags.None);
    }

    // Defect 2: state was updated from invalid samples, so the tick following a (0,0) always
    // fabricated a spurious jump measured from the origin.
    [Fact]
    public void Defect2_TickAfterAnInvalidPosition_DoesNotFabricateAJump()
    {
        var monitor = new EntityHealthMonitor(TestStream.Options());

        // Three samples ten metres apart, with an uninitialised (0,0) in the middle.
        double lat0;
        double lon0;
        TestStream.Offset(1000.0, 0.0, out lat0, out lon0);

        double lat2;
        double lon2;
        TestStream.Offset(1010.0, 0.0, out lat2, out lon2);

        monitor.Observe(TestStream.Sample("a", 0.0, lat0, lon0));
        monitor.Observe(TestStream.Sample("a", 1.0, 0.0, 0.0));
        EntityHealthReport after = monitor.Observe(TestStream.Sample("a", 2.0, lat2, lon2));

        Assert.False(
            (after.Flags & HealthFlags.Teleport) != HealthFlags.None,
            "the sample after an unusable position must be compared against the last VALID position, not the unusable one");

        EntityTrack? track;
        Assert.True(monitor.TryGetTrack("a", out track));
        Assert.NotNull(track);
        Assert.Equal(1L, track!.SamplesRejected);
    }

    [Fact]
    public void Defect2_LastSeenAndLastValidAreTrackedSeparately()
    {
        var monitor = new EntityHealthMonitor(TestStream.Options());

        monitor.Observe(TestStream.Sample("a", 0.0, 0.01, 0.01));
        monitor.Observe(TestStream.Sample("a", 1.0, 0.0, 0.0));

        EntityTrack? track;
        Assert.True(monitor.TryGetTrack("a", out track));
        Assert.NotNull(track);

        Assert.Equal(0.0, track!.LastSeenSample!.Latitude.Value);
        Assert.Equal(0.01, track.LastValidSample!.Latitude.Value);
    }

    // Defect 3: detection used if/else-if, so results were mutually exclusive and a jump
    // masked an outlier.
    [Fact]
    public void Defect3_ConditionsAreNotMutuallyExclusive()
    {
        MonitorOptions options = TestStream.Options();
        options.Thresholds.MaxTeleportDistanceMeters = 100.0;
        options.Thresholds.GroupOutlierRadiusMeters = 1000.0;

        var monitor = new EntityHealthMonitor(options);

        List<EntitySample> first = TestStream.Ring(0.0, 5, 100.0);
        GroupTickContext firstTick = monitor.CreateTickContext(first, TestStream.Epoch);
        for (int i = 0; i < first.Count; i++)
        {
            monitor.Observe(first[i], firstTick);
        }

        // Move entity-0 far away, and give it a non-normalised quaternion at the same time.
        List<EntitySample> second = TestStream.Ring(1.0, 5, 100.0);
        double latitude;
        double longitude;
        TestStream.Offset(50000.0, 0.0, out latitude, out longitude);
        second[0].Latitude = latitude;
        second[0].Longitude = longitude;
        second[0].QuaternionX = 0.0;
        second[0].QuaternionY = 0.0;
        second[0].QuaternionZ = 0.0;
        second[0].QuaternionW = 0.5;

        GroupTickContext secondTick = monitor.CreateTickContext(second, TestStream.Epoch.AddSeconds(1.0));
        EntityHealthReport report = monitor.Observe(second[0], secondTick);

        Assert.True((report.Flags & HealthFlags.Teleport) != HealthFlags.None, "expected the jump");
        Assert.True((report.Flags & HealthFlags.GroupOutlier) != HealthFlags.None, "expected the outlier as well as the jump");
        Assert.True((report.Flags & HealthFlags.NonNormalisedQuaternion) != HealthFlags.None, "expected the quaternion as well");
    }

    // Defect 4: the group centroid included the entity under test AND invalid entities, so a
    // single (999,999) poisoned cohesion for every entity in the group.
    [Fact]
    public void Defect4_OneInvalidEntityDoesNotPoisonTheGroupForEveryoneElse()
    {
        MonitorOptions options = TestStream.Options();
        options.Thresholds.GroupOutlierRadiusMeters = 1000.0;

        var monitor = new EntityHealthMonitor(options);

        List<EntitySample> samples = TestStream.Ring(0.0, 6, 100.0);
        samples[5].Latitude = 999.0;
        samples[5].Longitude = 999.0;

        GroupTickContext tick = monitor.CreateTickContext(samples, TestStream.Epoch);

        for (int i = 0; i < 5; i++)
        {
            EntityHealthReport report = monitor.Observe(samples[i], tick);
            Assert.False(
                (report.Flags & HealthFlags.GroupOutlier) != HealthFlags.None,
                "an out-of-range entity must not be allowed into the centroid the others are measured against");
        }
    }

    [Fact]
    public void Defect4_TheEntityUnderTestIsExcludedFromItsOwnCentroid()
    {
        List<EntitySample> samples = TestStream.Ring(0.0, 4, 1000.0);
        GroupTickContext tick = GroupTickContext.FromSamples(samples, TestStream.Epoch);

        double allLatitude;
        double allLongitude;
        int allCount;
        Assert.True(tick.TryGetCentroidExcluding(null, 1, out allLatitude, out allLongitude, out allCount));

        double withoutFirstLatitude;
        double withoutFirstLongitude;
        int withoutFirstCount;
        Assert.True(tick.TryGetCentroidExcluding("entity-0", 1, out withoutFirstLatitude, out withoutFirstLongitude, out withoutFirstCount));

        Assert.Equal(4, allCount);
        Assert.Equal(3, withoutFirstCount);
        Assert.NotEqual(allLatitude, withoutFirstLatitude);
    }

    [Fact]
    public void Defect4_GroupChecksNeedAMinimumContributorCount()
    {
        MonitorOptions options = TestStream.Options();
        options.Thresholds.MinimumGroupContributors = 3;

        var monitor = new EntityHealthMonitor(options);

        // Two entities: excluding the one under test leaves one, which is not a group.
        List<EntitySample> samples = TestStream.Ring(0.0, 2, 100.0);
        GroupTickContext tick = monitor.CreateTickContext(samples, TestStream.Epoch);
        EntityHealthReport report = monitor.Observe(samples[0], tick);

        Assert.True(
            (report.NotEvaluableFlags & HealthFlags.GroupOutlier) != HealthFlags.None,
            "too few contributors must report NotEvaluable, never healthy");
        Assert.False((report.Flags & HealthFlags.GroupOutlier) != HealthFlags.None);
    }

    // Defect 5: .Count() and .Average() re-enumerated a possibly-lazy sequence per entity per
    // tick, which is O(n squared) and re-runs the query.
    [Fact]
    public void Defect5_TheTickSequenceIsEnumeratedExactlyOnce()
    {
        var counter = new CountingEnumerable<EntitySample>(TestStream.Ring(0.0, 8, 100.0));
        var monitor = new EntityHealthMonitor(TestStream.Options());

        GroupTickContext tick = monitor.CreateTickContext(counter, TestStream.Epoch);
        foreach (EntitySample sample in counter.Items)
        {
            monitor.Observe(sample, tick);
        }

        Assert.Equal(1, counter.EnumerationCount);
    }

    // Defect 7: the state dictionary never evicted entries.
    [Fact]
    public void Defect7_TrackStoreIsBounded()
    {
        MonitorOptions options = TestStream.Options();
        options.MaxTrackedEntities = 16;

        var monitor = new EntityHealthMonitor(options);

        for (int i = 0; i < 2000; i++)
        {
            monitor.Observe(TestStream.Sample("entity-" + i, i, 0.001, 0.001));
        }

        Assert.True(
            monitor.Tracks.Count <= options.MaxTrackedEntities + TrackStore.EvictionInterval,
            "the store must stay bounded; it held " + monitor.Tracks.Count);
        Assert.True(monitor.Tracks.EvictionCount > 0L);
    }

    [Fact]
    public void Defect7_IdleTracksAreEvicted()
    {
        var store = new TrackStore(1000, TimeSpan.FromSeconds(10.0), 8);

        store.Touch("a", TestStream.Epoch);
        store.Touch("b", TestStream.Epoch.AddSeconds(30.0));

        store.Evict(TestStream.Epoch.AddSeconds(30.0));

        EntityTrack? evicted;
        Assert.False(store.TryGet("a", out evicted));

        EntityTrack? retained;
        Assert.True(store.TryGet("b", out retained));
    }

    // Defect 8: detector comment numbering ran 1, 3, 4 -- a detector was removed and never
    // replaced. The catalogue is now the source of truth, and every entry in it is either
    // implemented or explicitly a stub.
    [Fact]
    public void Defect8_EveryCatalogueFlagHasExactlyOneDetector()
    {
        IReadOnlyList<IDetector> detectors = DetectorCatalogue.CreateAll();

        var seen = new Dictionary<HealthFlags, string>();
        foreach (IDetector detector in detectors)
        {
            Assert.False(seen.ContainsKey(detector.Flag), detector.Flag + " has more than one detector");
            seen[detector.Flag] = detector.Id;
        }

        foreach (HealthFlags flag in HealthFlagInfo.All)
        {
            Assert.True(seen.ContainsKey(flag), flag + " is in HealthFlags but has no detector in the catalogue");
        }
    }

    [Fact]
    public void Defect8_EveryCatalogueFlagHasADefinition()
    {
        foreach (HealthFlags flag in HealthFlagInfo.All)
        {
            string definition = HealthFlagInfo.GetDefinition(flag);
            Assert.False(string.IsNullOrWhiteSpace(definition), flag + " has no definition");
            Assert.NotEqual(HealthFlagCategory.None, HealthFlagInfo.GetCategory(flag));
        }
    }

    [Fact]
    public void Defect8_UnimplementedDetectorsAreVisibleWhenAsked()
    {
        MonitorOptions options = TestStream.Options();
        options.IncludeUnimplementedDetectors = true;

        var monitor = new EntityHealthMonitor(options);
        EntityHealthReport report = monitor.Observe(TestStream.Sample("a", 0.0, 0.001, 0.001));

        Assert.Contains(report.Findings, f => f.Outcome == DetectorOutcome.NotEvaluable
            && f.Reason != null
            && f.Reason.Contains("not yet implemented"));
    }

    // Defect 9: a render-count colour flash cadence lived inside the detector. Nothing in Core
    // may reference a UI type at all.
    [Fact]
    public void Defect9_CoreDoesNotReferenceAnyUiAssembly()
    {
        foreach (System.Reflection.AssemblyName reference in typeof(EntityHealthMonitor).Assembly.GetReferencedAssemblies())
        {
            string name = reference.Name ?? string.Empty;
            Assert.False(name.StartsWith("Microsoft.Maui", StringComparison.Ordinal), "Argus.Core references " + name);
            Assert.False(name.StartsWith("System.Drawing", StringComparison.Ordinal), "Argus.Core references " + name);
            Assert.False(name.StartsWith("System.Windows", StringComparison.Ordinal), "Argus.Core references " + name);
        }
    }

    [Fact]
    public void Defect9_NoPublicCoreTypeIsNamedForPresentation()
    {
        foreach (Type type in typeof(EntityHealthMonitor).Assembly.GetExportedTypes())
        {
            Assert.DoesNotContain("Color", type.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("Brush", type.Name, StringComparison.Ordinal);
        }
    }
}

/// <summary>Counts how many times a sequence is enumerated.</summary>
internal sealed class CountingEnumerable<T> : IEnumerable<T>
{
    private readonly IReadOnlyList<T> _items;

    internal CountingEnumerable(IReadOnlyList<T> items)
    {
        _items = items;
    }

    internal int EnumerationCount { get; private set; }

    internal IReadOnlyList<T> Items
    {
        get { return _items; }
    }

    public IEnumerator<T> GetEnumerator()
    {
        EnumerationCount++;
        for (int i = 0; i < _items.Count; i++)
        {
            yield return _items[i];
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
