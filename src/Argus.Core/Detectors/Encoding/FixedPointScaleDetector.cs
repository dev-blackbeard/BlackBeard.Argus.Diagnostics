using Argus.Contracts;

namespace Argus.Detectors.Encoding;

/// <summary>
/// Reports an angular value read at the wrong fixed-point scale.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Test the value against DetectorThresholds.FixedPointScaleFactor in both directions: a raw scaled integer read as degrees is out of range by that factor, and a degree value read as though it were already scaled is smaller by it. Both directions occur, and they have opposite fixes, so the finding must say which one it saw.</para>
/// </remarks>
public sealed class FixedPointScaleDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.encoding.fixed-point-scale";

    // TODO(argus): FixedPointScaleError
    /// <summary>Creates the stub.</summary>
    public FixedPointScaleDetector()
        : base(DetectorId, HealthFlags.FixedPointScaleError)
    {
    }
}
