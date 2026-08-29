using Argus.Contracts;

namespace Argus.Detectors.Kinematic;

/// <summary>
/// Reports a position oscillating about a mean rather than describing a path.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Over the last DetectorThresholds.JitterWindowSamples points in EntityTrack.RecentPoints, compare the root-mean-square deviation from their mean against DetectorThresholds.MaxJitterMeters. Distinguish dither from motion by testing whether successive displacements correlate: a moving entity's steps point the same way, and a dithering one's cancel.</para>
/// </remarks>
public sealed class JitterDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.kinematic.jitter";

    // TODO(argus): Jitter
    /// <summary>Creates the stub.</summary>
    public JitterDetector()
        : base(DetectorId, HealthFlags.Jitter)
    {
    }
}
