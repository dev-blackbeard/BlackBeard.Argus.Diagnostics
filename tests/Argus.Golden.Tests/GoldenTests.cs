using System;
using System.Collections.Generic;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Detectors;
using Argus.Pipeline;
using Argus.State;
using Argus.Testing;
using Argus.Testing.Injectors;
using Xunit;

namespace Argus.Golden.Tests;

public sealed class GoldenTests
{
    private static readonly DateTime Epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IEnumerable<object[]> LockedCases()
    {
        foreach (GoldenCase golden in GoldenCases.Locked)
        {
            yield return new object[] { golden };
        }
    }

    [Theory]
    [MemberData(nameof(LockedCases))]
    public void LockedCasesProduceExactlyTheExpectedFlags(GoldenCase golden)
    {
        HealthFlags actual = Run(golden.Name);

        Assert.True(
            actual == golden.Expected,
            golden.Name + Environment.NewLine
                + "  expected: " + HealthFlagInfo.Describe(golden.Expected) + Environment.NewLine
                + "  actual:   " + HealthFlagInfo.Describe(actual) + Environment.NewLine
                + "  note:     " + golden.Note);
    }

    /// <summary>
    /// The forcing function: the pending list must be exactly the set of unimplemented detectors.
    /// </summary>
    /// <remarks>
    /// Implement a detector and this fails until its golden case is written and moved into the
    /// locked table. Delete a detector and this fails too. There is no state in which the
    /// catalogue and the golden table can silently drift apart.
    /// </remarks>
    [Fact]
    public void PendingListMatchesTheUnimplementedDetectorsExactly()
    {
        var unimplemented = new SortedSet<string>(StringComparer.Ordinal);
        foreach (IDetector detector in DetectorCatalogue.CreateAll())
        {
            if (detector.Status == DetectorStatus.NotImplemented)
            {
                unimplemented.Add(detector.Flag.ToString());
            }
        }

        var pending = new SortedSet<string>(StringComparer.Ordinal);
        foreach (HealthFlags flag in GoldenCases.Pending)
        {
            pending.Add(flag.ToString());
        }

        Assert.Equal(string.Join(", ", unimplemented), string.Join(", ", pending));
    }

    /// <summary>Every injector declares flags that exist in the catalogue.</summary>
    [Fact]
    public void EveryInjectorMapsOntoCatalogueFlags()
    {
        var known = new HashSet<HealthFlags>(HealthFlagInfo.All);

        foreach (ISampleInjector injector in InjectorCatalogue.CreateSampleInjectors())
        {
            Assert.NotEmpty(injector.ExpectedFlags);
            foreach (HealthFlags flag in injector.ExpectedFlags)
            {
                Assert.Contains(flag, known);
            }
        }

        foreach (IStreamInjector injector in InjectorCatalogue.CreateStreamInjectors())
        {
            Assert.NotEmpty(injector.ExpectedFlags);
            foreach (HealthFlags flag in injector.ExpectedFlags)
            {
                Assert.Contains(flag, known);
            }
        }
    }

    private static MonitorOptions Options()
    {
        var options = new MonitorOptions();
        options.Thresholds.MaxTeleportDistanceMeters = 5000.0;
        options.Thresholds.MaxSpeedMetersPerSecond = 250.0;
        options.Thresholds.GroupOutlierRadiusMeters = 20000.0;
        options.Thresholds.MinimumGroupContributors = 3;
        return options;
    }

    private static ScenarioDefinition Scenario()
    {
        return new ScenarioDefinition
        {
            Name = "golden",
            EntityCount = 6,
            TickCount = 8,
            SpacingMeters = 500.0,
            SpeedMetersPerSecond = 50.0,
            UpdateIntervalSeconds = 1.0,
            StartTimeUtc = Epoch,
        };
    }

    private static HealthFlags Run(string caseName)
    {
        switch (caseName)
        {
            case "clean":
                return Observe(new SyntheticStreamSource(Scenario()), Options());

            case "reorder":
                return Observe(
                    new InjectedStreamSource(
                        new SyntheticStreamSource(Scenario()),
                        1,
                        null,
                        new List<IStreamInjector> { new ReorderInjector(4) }.AsReadOnly()),
                    Options());

            case "teleport":
                return ObserveDisplaced();

            case "non-finite":
                return ObserveMutated(s => s.Latitude = double.NaN);

            case "non-normalised-quaternion":
                return ObserveMutated(s =>
                {
                    s.QuaternionX = 0.0;
                    s.QuaternionY = 0.0;
                    s.QuaternionZ = 0.0;
                    s.QuaternionW = 0.5;
                });

            default:
                throw new ArgumentException("Unknown golden case '" + caseName + "'.", nameof(caseName));
        }
    }

    /// <summary>Runs a whole stream and returns the union of every flag raised.</summary>
    private static HealthFlags Observe(IEntityStreamSource source, MonitorOptions options)
    {
        var monitor = new EntityHealthMonitor(options);
        HealthFlags flags = HealthFlags.None;

        foreach (StreamTick tick in source.Read())
        {
            if (tick.Samples.Count == 0)
            {
                continue;
            }

            GroupTickContext group = monitor.CreateTickContext(tick.Samples, tick.TimeUtc);
            for (int i = 0; i < tick.Samples.Count; i++)
            {
                flags |= monitor.Observe(tick.Samples[i], group).Flags;
            }
        }

        return flags;
    }

    /// <summary>
    /// Damages exactly one entity on exactly one tick, and returns only that entity's flags.
    /// </summary>
    /// <remarks>
    /// Scoped narrowly on purpose. A golden case that damages the whole stream measures the
    /// union of everything every detector noticed, which is not a statement about the fault
    /// being injected.
    /// </remarks>
    private static HealthFlags ObserveMutated(Action<EntitySample> damage)
    {
        var monitor = new EntityHealthMonitor(Options());
        var source = new SyntheticStreamSource(Scenario());
        HealthFlags flags = HealthFlags.None;

        foreach (StreamTick tick in source.Read())
        {
            if (tick.Index == 4)
            {
                damage(tick.Samples[0]);
            }

            GroupTickContext group = monitor.CreateTickContext(tick.Samples, tick.TimeUtc);
            for (int i = 0; i < tick.Samples.Count; i++)
            {
                EntityHealthReport report = monitor.Observe(tick.Samples[i], group);
                if (tick.Index == 4 && i == 0)
                {
                    flags |= report.Flags;
                }
            }
        }

        return flags;
    }

    private static HealthFlags ObserveDisplaced()
    {
        var monitor = new EntityHealthMonitor(Options());
        var source = new SyntheticStreamSource(Scenario());
        HealthFlags flags = HealthFlags.None;

        foreach (StreamTick tick in source.Read())
        {
            if (tick.Index == 4)
            {
                // Half a degree of latitude: tens of kilometres, so it trips the distance gate,
                // the rate gate and the group radius at once.
                EntitySample first = tick.Samples[0];
                first.Latitude = first.Latitude.Value + 0.5;
            }

            GroupTickContext group = monitor.CreateTickContext(tick.Samples, tick.TimeUtc);
            for (int i = 0; i < tick.Samples.Count; i++)
            {
                EntityHealthReport report = monitor.Observe(tick.Samples[i], group);
                if (tick.Index == 4 && i == 0)
                {
                    flags |= report.Flags;
                }
            }
        }

        return flags;
    }
}
