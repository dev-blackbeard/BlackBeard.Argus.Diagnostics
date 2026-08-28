using Argus.Contracts;

namespace Argus.Detectors.Encoding;

/// <summary>
/// Reports angular values that are confined to a range consistent with radians.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Accumulate, per entity, how many consecutive samples have kept every angular field within DetectorThresholds.RadiansSuspicionBound, and flag once that run reaches DetectorThresholds.RadiansSuspicionMinimumSamples. The run is what makes this safe: on any single sample the fault is indistinguishable from an entity that is genuinely near the origin and pointing near north.</para>
/// </remarks>
public sealed class RadiansAsDegreesDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.encoding.radians-as-degrees";

    // TODO(argus): RadiansAsDegrees
    /// <summary>Creates the stub.</summary>
    public RadiansAsDegreesDetector()
        : base(DetectorId, HealthFlags.RadiansAsDegrees)
    {
    }
}
