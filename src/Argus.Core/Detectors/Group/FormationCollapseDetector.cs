using Argus.Contracts;

namespace Argus.Detectors.Group;

/// <summary>
/// Reports a group whose geometric arrangement has degenerated.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Flag when GroupTickContext.SpreadMeters falls below DetectorThresholds.GroupCollapseSpreadMeters - every contributor reporting nearly the same position, which is what a stuck or duplicated source looks like from the group's point of view - and when the arrangement loses structure entirely. Requires at least DetectorThresholds.MinimumGroupContributors contributors before it means anything.</para>
/// </remarks>
public sealed class FormationCollapseDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.group.formation-collapse";

    // TODO(argus): FormationCollapse
    /// <summary>Creates the stub.</summary>
    public FormationCollapseDetector()
        : base(DetectorId, HealthFlags.FormationCollapse)
    {
    }
}
