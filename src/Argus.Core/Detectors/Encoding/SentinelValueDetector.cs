using Argus.Contracts;

namespace Argus.Detectors.Encoding;

/// <summary>
/// Reports values characteristic of uninitialised or filler memory.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Test fields against the values that memory carries when nobody has written a measurement into it: exact zero, minus one, an all-bits-set pattern, and the extremes of the type. Exact (0,0) is governed by DetectorThresholds.TreatZeroIslandAsSentinel, because it is simultaneously a legal position and the commonest filler value, and no examination of the value alone can separate the two.</para>
/// </remarks>
public sealed class SentinelValueDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.encoding.sentinel-value";

    // TODO(argus): SentinelValue
    /// <summary>Creates the stub.</summary>
    public SentinelValueDetector()
        : base(DetectorId, HealthFlags.SentinelValue)
    {
    }
}
