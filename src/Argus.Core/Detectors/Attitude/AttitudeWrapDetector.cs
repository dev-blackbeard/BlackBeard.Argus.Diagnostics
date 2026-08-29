using Argus.Contracts;

namespace Argus.Detectors.Attitude;

/// <summary>
/// Reports an attitude angle jumping across a wrap boundary.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Use Geo.AngularDifferenceDegrees so that a rotation through north reads as a small change rather than a full turn, then flag a step beyond DetectorThresholds.MaxAttitudeStepDegrees. The fault this catches is a producer and a consumer disagreeing about a convention - zero to three-sixty against plus or minus a half turn - which shows up only at the boundary and therefore only intermittently.</para>
/// </remarks>
public sealed class AttitudeWrapDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.attitude.wrap-discontinuity";

    // TODO(argus): AttitudeWrapDiscontinuity
    /// <summary>Creates the stub.</summary>
    public AttitudeWrapDetector()
        : base(DetectorId, HealthFlags.AttitudeWrapDiscontinuity)
    {
    }
}
