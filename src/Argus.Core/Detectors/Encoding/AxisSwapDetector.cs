using Argus.Contracts;

namespace Argus.Detectors.Encoding;

/// <summary>
/// Reports latitude and longitude that appear to have been transposed.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Test whether the transposed pair is markedly more plausible than the pair as read. The strongest available evidence is the group: if the swapped pair lands near the group centroid and the pair as read does not, the transposition explains the entity's position and its group membership at once. Latitude beyond ninety degrees is a much weaker signal on its own, because a transposition between two values that are both small produces a position that is wrong and entirely plausible.</para>
/// </remarks>
public sealed class AxisSwapDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.encoding.axis-swap";

    // TODO(argus): AxisSwap
    /// <summary>Creates the stub.</summary>
    public AxisSwapDetector()
        : base(DetectorId, HealthFlags.AxisSwap)
    {
    }
}
