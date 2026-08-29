using Argus.Contracts;

namespace Argus.Detectors.Kinematic;

/// <summary>
/// Reports a change in derived speed too large to be real.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Difference the derived speed against EntityTrack.LastDerivedSpeedMetersPerSecond over the interval since the last valid sample and compare with DetectorThresholds.MaxAccelerationMetersPerSecondSquared. Needs two prior valid samples, and reports NotEvaluable rather than healthy until it has them.</para>
/// </remarks>
public sealed class ImplausibleAccelerationDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.kinematic.implausible-acceleration";

    // TODO(argus): ImplausibleAcceleration
    /// <summary>Creates the stub.</summary>
    public ImplausibleAccelerationDetector()
        : base(DetectorId, HealthFlags.ImplausibleAcceleration)
    {
    }
}
