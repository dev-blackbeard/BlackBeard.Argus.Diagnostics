using Argus.Contracts;

namespace Argus.Detectors.Kinematic;

/// <summary>
/// Reports a rate of altitude change beyond the vertical gate.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Difference altitude over the interval since the last valid sample and compare with DetectorThresholds.MaxAltitudeRateMetersPerSecond. Worth separating from horizontal speed because the vertical channel is often encoded differently from the horizontal one - a different width, a different scale, sometimes a different datum - so it fails independently.</para>
/// </remarks>
public sealed class ImplausibleAltitudeRateDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.kinematic.implausible-altitude-rate";

    // TODO(argus): ImplausibleAltitudeRate
    /// <summary>Creates the stub.</summary>
    public ImplausibleAltitudeRateDetector()
        : base(DetectorId, HealthFlags.ImplausibleAltitudeRate)
    {
    }
}
