using Argus.Contracts;

namespace Argus.Detectors.Attitude;

/// <summary>
/// Reports roll, pitch or yaw outside its defined range.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Compare each supplied angle against DetectorThresholds.MaxRollDegrees, MaxPitchDegrees and MaxYawDegrees. Report which angle and which bound, because a pitch beyond a quarter turn and a yaw beyond a full turn are different faults with different likely causes.</para>
/// </remarks>
public sealed class AttitudeRangeDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.attitude.out-of-range";

    // TODO(argus): AttitudeOutOfRange
    /// <summary>Creates the stub.</summary>
    public AttitudeRangeDetector()
        : base(DetectorId, HealthFlags.AttitudeOutOfRange)
    {
    }
}
