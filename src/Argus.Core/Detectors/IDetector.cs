using Argus.Contracts;

namespace Argus.Detectors;

/// <summary>Whether a detector is implemented and available to run.</summary>
public enum DetectorStatus
{
    /// <summary>The detector is implemented and will run.</summary>
    Implemented = 0,

    /// <summary>
    /// The detector is declared but not yet implemented. The registry does not call it.
    /// </summary>
    /// <remarks>
    /// Declared-but-unimplemented is a deliberate state rather than an omission. The
    /// catalogue is the specification, and a condition that is specified but not yet checked
    /// should be visible as such — turn on <c>MonitorOptions.IncludeUnimplementedDetectors</c>
    /// and it appears in every report as <c>NotEvaluable</c>. The alternative, leaving it out
    /// of the catalogue until someone writes the code, is how the prototype ended up with
    /// detector comments numbered 1, 3, 4.
    /// </remarks>
    NotImplemented = 1,
}

/// <summary>
/// One condition, checked against one sample.
/// </summary>
/// <remarks>
/// <para>
/// Detectors are stateless and must stay that way: all per-entity state lives on
/// <c>EntityTrack</c>, reached through the context. A detector that keeps its own state
/// cannot be shared between monitors, cannot be reasoned about across threads, and quietly
/// couples the order detectors run in to the answers they give.
/// </para>
/// <para>
/// Every detector runs on every sample. There is no short-circuiting and no ordering
/// significance: architecture rule 4 exists because the prototype chained its checks with
/// <c>else if</c>, so an entity that had jumped could not also be reported as a group
/// outlier — and the two together are a much more specific diagnosis than either alone.
/// </para>
/// </remarks>
public interface IDetector
{
    /// <summary>A stable identifier for this detector, carried on every finding it produces.</summary>
    string Id { get; }

    /// <summary>The condition this detector checks for.</summary>
    HealthFlags Flag { get; }

    /// <summary>Whether this detector is implemented.</summary>
    DetectorStatus Status { get; }

    /// <summary>Evaluates one sample.</summary>
    /// <param name="context">Everything known about the sample.</param>
    /// <returns>The detector's conclusion. Never <c>null</c>, and never "healthy" when the inputs were missing.</returns>
    DetectorResult Evaluate(DetectorContext context);
}
