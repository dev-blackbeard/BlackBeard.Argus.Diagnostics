using Argus.Contracts;

namespace Argus.Detectors.Kinematic;

/// <summary>
/// Reports reported velocity that disagrees with the velocity implied by consecutive positions.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Compare the reported ground speed with the derived speed against DetectorThresholds.MaxVelocityMismatchMetersPerSecond. This is one of the few detectors that cross-checks two independently encoded fields against each other, which makes it valuable out of proportion to its simplicity: it catches a fault in either channel without needing to know which channel is right.</para>
/// </remarks>
public sealed class VelocityMismatchDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.kinematic.velocity-mismatch";

    // TODO(argus): VelocityMismatch
    /// <summary>Creates the stub.</summary>
    public VelocityMismatchDetector()
        : base(DetectorId, HealthFlags.VelocityMismatch)
    {
    }
}
