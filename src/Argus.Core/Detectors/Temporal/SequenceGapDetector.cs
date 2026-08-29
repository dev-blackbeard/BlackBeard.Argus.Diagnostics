using Argus.Contracts;

namespace Argus.Detectors.Temporal;

/// <summary>
/// Reports sequence numbers that were skipped between consecutive arrivals.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Compare the sample's sequence number with the highest already observed. A difference greater than one plus DetectorThresholds.SequenceGapTolerance means that many frames never arrived. Must cope with the producer's wrap-around: a sequence number that is a fixed-width integer will roll over, and treating that as a gap of four billion is worse than not checking at all, so the width has to be an input rather than an assumption.</para>
/// </remarks>
public sealed class SequenceGapDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.temporal.sequence-gap";

    // TODO(argus): SequenceGap
    /// <summary>Creates the stub.</summary>
    public SequenceGapDetector()
        : base(DetectorId, HealthFlags.SequenceGap)
    {
    }
}
