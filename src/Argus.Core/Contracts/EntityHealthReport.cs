using System;
using System.Collections.Generic;

namespace Argus.Contracts;

/// <summary>
/// Everything Argus concluded about one entity from one sample.
/// </summary>
/// <remarks>
/// <see cref="Flags"/> and <see cref="NotEvaluableFlags"/> are disjoint by construction and
/// deliberately separate. A caller that treats "not flagged" as "verified healthy" is
/// making a claim the data does not support, so the report forces the distinction to be
/// visible (architecture rule 6).
/// </remarks>
public sealed class EntityHealthReport
{
    /// <summary>Creates a report.</summary>
    /// <param name="entityId">The entity the report is about.</param>
    /// <param name="timestampUtc">The arrival time of the sample the report was produced from.</param>
    /// <param name="findings">Every finding produced for the sample, in detector order.</param>
    /// <param name="samplesObserved">How many samples have arrived for this entity, including ones no detector could evaluate.</param>
    /// <param name="samplesEvaluated">How many samples were actually evaluated.</param>
    /// <param name="samplesFlagged">How many evaluated samples raised at least one flag.</param>
    public EntityHealthReport(
        string entityId,
        DateTime timestampUtc,
        IReadOnlyList<HealthFinding> findings,
        long samplesObserved,
        long samplesEvaluated,
        long samplesFlagged)
    {
        EntityId = entityId;
        TimestampUtc = timestampUtc;
        Findings = findings;
        SamplesObserved = samplesObserved;
        SamplesEvaluated = samplesEvaluated;
        SamplesFlagged = samplesFlagged;

        HealthFlags flagged = HealthFlags.None;
        HealthFlags notEvaluable = HealthFlags.None;
        for (int i = 0; i < findings.Count; i++)
        {
            HealthFinding finding = findings[i];
            if (finding.Outcome == DetectorOutcome.Flagged)
            {
                flagged |= finding.Flag;
            }
            else if (finding.Outcome == DetectorOutcome.NotEvaluable)
            {
                notEvaluable |= finding.Flag;
            }
        }

        Flags = flagged;
        NotEvaluableFlags = notEvaluable & ~flagged;
    }

    /// <summary>The entity the report is about.</summary>
    public string EntityId { get; }

    /// <summary>The arrival time of the sample the report was produced from.</summary>
    public DateTime TimestampUtc { get; }

    /// <summary>The union of every flag that was actually detected.</summary>
    public HealthFlags Flags { get; }

    /// <summary>
    /// The union of every flag whose detector could not run. These are neither healthy nor
    /// unhealthy: they were not checked.
    /// </summary>
    public HealthFlags NotEvaluableFlags { get; }

    /// <summary>Every finding produced for the sample, including healthy and not-evaluable ones.</summary>
    public IReadOnlyList<HealthFinding> Findings { get; }

    /// <summary>How many samples have arrived for this entity, including ones no detector could evaluate.</summary>
    /// <remarks>
    /// Kept separate from <see cref="SamplesEvaluated"/> on purpose. The prototype counted an
    /// arrival, then early-returned on a non-positive interval, so stale and duplicate
    /// samples silently deflated the health percentage while raising no flag of their own.
    /// Splitting the counters means a deflated percentage now always has a finding behind it.
    /// </remarks>
    public long SamplesObserved { get; }

    /// <summary>How many samples were actually evaluated.</summary>
    public long SamplesEvaluated { get; }

    /// <summary>How many evaluated samples raised at least one flag.</summary>
    public long SamplesFlagged { get; }

    /// <summary>
    /// The proportion of evaluated samples that raised no flag, as a percentage, or
    /// <c>null</c> when nothing has been evaluated yet.
    /// </summary>
    public double? HealthPercent
    {
        get
        {
            if (SamplesEvaluated <= 0)
            {
                return null;
            }

            return 100.0 * (SamplesEvaluated - SamplesFlagged) / SamplesEvaluated;
        }
    }

    /// <summary>Whether the sample raised no flags at all.</summary>
    /// <remarks>
    /// This says nothing about <see cref="NotEvaluableFlags"/>. Use
    /// <see cref="IsFullyEvaluated"/> when the difference matters.
    /// </remarks>
    public bool IsHealthy
    {
        get { return Flags == HealthFlags.None; }
    }

    /// <summary>Whether every detector in the registry was able to run against this sample.</summary>
    public bool IsFullyEvaluated
    {
        get { return NotEvaluableFlags == HealthFlags.None; }
    }

    /// <summary>Only the findings that actually flagged something.</summary>
    /// <returns>The flagged findings, in detector order.</returns>
    public IEnumerable<HealthFinding> FlaggedFindings()
    {
        for (int i = 0; i < Findings.Count; i++)
        {
            if (Findings[i].Outcome == DetectorOutcome.Flagged)
            {
                yield return Findings[i];
            }
        }
    }

    /// <summary>Only the findings whose detector could not run.</summary>
    /// <returns>The not-evaluable findings, in detector order.</returns>
    public IEnumerable<HealthFinding> NotEvaluableFindings()
    {
        for (int i = 0; i < Findings.Count; i++)
        {
            if (Findings[i].Outcome == DetectorOutcome.NotEvaluable)
            {
                yield return Findings[i];
            }
        }
    }

    /// <summary>Renders a one-line summary of the report.</summary>
    /// <returns>The entity id and the flags that were raised.</returns>
    public override string ToString()
    {
        return EntityId + ": " + HealthFlagInfo.Describe(Flags);
    }
}
