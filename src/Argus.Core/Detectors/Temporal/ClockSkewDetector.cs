using Argus.Contracts;

namespace Argus.Detectors.Temporal;

/// <summary>
/// Reports a disagreement between the time a sample says it was produced and the time it arrived.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Flag when the difference between EntitySample.SourceTimeUtc and EntitySample.ArrivalTimeUtc exceeds DetectorThresholds.MaxClockSkewSeconds. The useful signal is the drift of that difference rather than its absolute value, since a constant offset is a transport latency and a growing one is an undisciplined clock: track the difference's own trend and report which of the two it is, because they have different owners.</para>
/// </remarks>
public sealed class ClockSkewDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.temporal.clock-skew";

    // TODO(argus): ClockSkew
    /// <summary>Creates the stub.</summary>
    public ClockSkewDetector()
        : base(DetectorId, HealthFlags.ClockSkew)
    {
    }
}
