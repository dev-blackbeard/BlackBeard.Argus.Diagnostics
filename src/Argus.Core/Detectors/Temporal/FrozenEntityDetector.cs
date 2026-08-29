using Argus.Contracts;

namespace Argus.Detectors.Temporal;

/// <summary>
/// Reports an entity whose position has stopped changing while its reported velocity says it is moving.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Maintain the run of consecutive samples whose position moved less than DetectorThresholds.StaticPositionEpsilonMeters (EntityTrack.StaticSampleRun already holds it) and flag once the run reaches DetectorThresholds.FrozenSampleRun while the reported ground speed exceeds DetectorThresholds.FrozenReportedSpeedMetersPerSecond. The contradiction between the two claims is the finding: a genuinely stationary entity reports a velocity of zero and is not frozen, and an entity with no reported velocity is NotEvaluable rather than healthy.</para>
/// </remarks>
public sealed class FrozenEntityDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.temporal.frozen-entity";

    // TODO(argus): FrozenEntity
    /// <summary>Creates the stub.</summary>
    public FrozenEntityDetector()
        : base(DetectorId, HealthFlags.FrozenEntity)
    {
    }
}
