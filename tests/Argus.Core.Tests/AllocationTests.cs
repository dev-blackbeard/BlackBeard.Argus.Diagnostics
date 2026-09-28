using System;
using System.Collections.Generic;
using System.Linq;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Pipeline;
using Argus.State;
using Xunit;

namespace Argus.Core.Tests;

/// <summary>
/// What one observation allocates. A monitor runs once per sample, often on a device where every
/// collection pauses the UI, so garbage per sample is a cost a host pays in stalls.
/// </summary>
public sealed class AllocationTests
{
    private const int Entities = 20;

    [Fact]
    public void A_steady_healthy_observation_allocates_little_more_than_its_report()
    {
        var monitor = new EntityHealthMonitor(TestStream.Options());
        const int warmUpRounds = 5;
        const int measuredRounds = 20;
        List<List<EntitySample>> rounds = MovingRing(warmUpRounds + measuredRounds);
        GroupTickContext group = monitor.CreateTickContext(rounds[0], TestStream.Epoch);

        for (int round = 0; round < warmUpRounds; round++)
        {
            ObserveRound(monitor, rounds[round], group);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int round = warmUpRounds; round < rounds.Count; round++)
        {
            ObserveRound(monitor, rounds[round], group);
        }

        long perObservation = (GC.GetAllocatedBytesForCurrentThread() - before) / (measuredRounds * Entities);

        // The report and the detector context are all that is left: a few hundred bytes. Before
        // healthy results stopped formatting strings nobody kept, this was several kilobytes.
        Assert.True(perObservation < 512, "bytes allocated per observation: " + perObservation);
    }

    [Fact]
    public void An_entity_whose_findings_are_unchanged_reuses_its_previous_findings_list()
    {
        var monitor = new EntityHealthMonitor(TestStream.Options());
        List<List<EntitySample>> rounds = MovingRing(4);
        GroupTickContext group = monitor.CreateTickContext(rounds[0], TestStream.Epoch);

        monitor.Observe(rounds[0][0], group);
        monitor.Observe(rounds[1][0], group);
        EntityHealthReport third = monitor.Observe(rounds[2][0], group);
        EntityHealthReport fourth = monitor.Observe(rounds[3][0], group);

        // No quaternion and no sequence number were supplied, so those detectors report
        // NotEvaluable on every sample, identically.
        Assert.NotEmpty(third.Findings);
        Assert.All(third.Findings, finding => Assert.Equal(DetectorOutcome.NotEvaluable, finding.Outcome));
        Assert.Same(third.Findings, fourth.Findings);
    }

    [Fact]
    public void A_flag_still_produces_a_fresh_findings_list_with_the_flagged_finding_in_it()
    {
        var monitor = new EntityHealthMonitor(TestStream.Options());
        List<List<EntitySample>> rounds = MovingRing(3);
        GroupTickContext group = monitor.CreateTickContext(rounds[0], TestStream.Epoch);

        monitor.Observe(rounds[0][0], group);
        EntityHealthReport steady = monitor.Observe(rounds[1][0], group);

        EntitySample jumped = rounds[2][0];
        jumped.Latitude += 0.1; // ~11 km in one second
        EntityHealthReport flagged = monitor.Observe(jumped, group);

        Assert.NotSame(steady.Findings, flagged.Findings);
        Assert.Contains(flagged.Findings, f => f.Flag == HealthFlags.Teleport && f.Outcome == DetectorOutcome.Flagged);
        Assert.True((flagged.Flags & HealthFlags.Teleport) != HealthFlags.None);
    }

    [Fact]
    public void Kept_healthy_findings_still_carry_their_measured_and_expected_values()
    {
        MonitorOptions options = TestStream.Options();
        options.IncludeHealthyFindings = true;
        var monitor = new EntityHealthMonitor(options);
        List<List<EntitySample>> rounds = MovingRing(2);
        GroupTickContext group = monitor.CreateTickContext(rounds[0], TestStream.Epoch);

        monitor.Observe(rounds[0][0], group);
        EntityHealthReport report = monitor.Observe(rounds[1][0], group);

        HealthFinding teleport = report.Findings.Single(f => f.Flag == HealthFlags.Teleport);
        Assert.Equal(DetectorOutcome.Healthy, teleport.Outcome);
        Assert.EndsWith(" m", teleport.Measured, StringComparison.Ordinal);
        Assert.StartsWith("at most", teleport.Expected, StringComparison.Ordinal);
        Assert.True(teleport.MeasuredValue.HasValue);
    }

    [Fact]
    public void Not_evaluable_findings_keep_their_reasons()
    {
        var monitor = new EntityHealthMonitor(new MonitorOptions());

        EntityHealthReport first = monitor.Observe(TestStream.Sample("a", 0.0, 0.001, 0.001));
        EntityHealthReport second = monitor.Observe(TestStream.Sample("b", 0.0, 0.001, 0.001));

        HealthFinding teleport = first.Findings.Single(f => f.Flag == HealthFlags.Teleport);
        Assert.Equal(DetectorOutcome.NotEvaluable, teleport.Outcome);
        Assert.Contains("MaxTeleportDistanceMeters", teleport.Reason!, StringComparison.Ordinal);
        Assert.Equal(teleport.Reason, second.Findings.Single(f => f.Flag == HealthFlags.Teleport).Reason);
    }

    [Fact]
    public void Splitting_no_flags_allocates_nothing_and_yields_nothing()
    {
        Assert.Empty(HealthFlagInfo.Split(HealthFlags.None));
        Assert.Same(HealthFlagInfo.Split(HealthFlags.None), HealthFlagInfo.Split(HealthFlags.None));
        Assert.Equal(
            new[] { HealthFlags.Teleport, HealthFlags.GroupOutlier }.OrderBy(f => (ulong)f),
            HealthFlagInfo.Split(HealthFlags.Teleport | HealthFlags.GroupOutlier).OrderBy(f => (ulong)f));
    }

    private static void ObserveRound(EntityHealthMonitor monitor, List<EntitySample> samples, GroupTickContext group)
    {
        for (int i = 0; i < samples.Count; i++)
        {
            monitor.Observe(samples[i], group);
        }
    }

    // A 20-entity ring 1 km across, drifting ~1 m north per one-second round: healthy for every
    // configured detector. Built up front so the measurement sees only the monitor's allocations.
    private static List<List<EntitySample>> MovingRing(int rounds)
    {
        var all = new List<List<EntitySample>>(rounds);
        for (int round = 0; round < rounds; round++)
        {
            List<EntitySample> ring = TestStream.Ring(round, Entities, 500.0);
            foreach (EntitySample sample in ring)
            {
                sample.Latitude += round / TestStream.MetersPerDegreeLatitude;
            }

            all.Add(ring);
        }

        return all;
    }
}
