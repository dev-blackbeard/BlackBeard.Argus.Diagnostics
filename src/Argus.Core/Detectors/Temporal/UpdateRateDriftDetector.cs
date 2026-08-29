using Argus.Contracts;

namespace Argus.Detectors.Temporal;

/// <summary>
/// Reports a stream whose cadence has moved away from the interval it is expected to hold.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Compare EntityTrack.MeanUpdateIntervalSeconds against DetectorThresholds.ExpectedUpdateIntervalSeconds, flagging when the ratio leaves one plus or minus DetectorThresholds.UpdateRateDriftTolerance. Use the moving mean rather than the instantaneous interval: a single late frame is jitter, and this flag is for a cadence that has changed.</para>
/// </remarks>
public sealed class UpdateRateDriftDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.temporal.update-rate-drift";

    // TODO(argus): UpdateRateDrift
    /// <summary>Creates the stub.</summary>
    public UpdateRateDriftDetector()
        : base(DetectorId, HealthFlags.UpdateRateDrift)
    {
    }
}
