using Argus.Contracts;

namespace Argus.Detectors.Encoding;

/// <summary>
/// Reports positional resolution that has abruptly coarsened.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Track the smallest non-zero step between consecutive positions and flag when it grows by more than DetectorThresholds.QuantisationCoarseningFactor. The fault this catches is a value that has passed through a narrower floating-point type somewhere in the pipeline: the position stays correct to within metres and stops being correct to within centimetres, which no plausibility check on a single sample can see.</para>
/// </remarks>
public sealed class QuantisationCollapseDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.encoding.quantisation-collapse";

    // TODO(argus): QuantisationCollapse
    /// <summary>Creates the stub.</summary>
    public QuantisationCollapseDetector()
        : base(DetectorId, HealthFlags.QuantisationCollapse)
    {
    }
}
