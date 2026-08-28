using Argus.Contracts;

namespace Argus.Detectors.Group;

/// <summary>
/// Reports a group whose spread has grown beyond its cohesion radius.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Compare GroupTickContext.SpreadMeters against DetectorThresholds.GroupCohesionRadiusMeters. This is a property of the tick rather than of the entity, so every entity in the group receives the same finding: that is correct and deliberate, because the condition is about the group.</para>
/// </remarks>
public sealed class CohesionBreakDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.group.cohesion-break";

    // TODO(argus): CohesionBreak
    /// <summary>Creates the stub.</summary>
    public CohesionBreakDetector()
        : base(DetectorId, HealthFlags.CohesionBreak)
    {
    }
}
