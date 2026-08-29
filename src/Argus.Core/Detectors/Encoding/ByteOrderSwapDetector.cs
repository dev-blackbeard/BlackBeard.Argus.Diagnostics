using Argus.Contracts;

namespace Argus.Detectors.Encoding;

/// <summary>
/// Reports a value that is implausible as read but plausible with its bytes reversed.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> Reinterpret the field's eight bytes in the opposite order and test whether the result is plausible where the original was not. The argument only holds one way round: many plausible values are also plausible when byte-swapped, so a swap that makes an implausible value plausible is evidence, and a swap that makes a plausible value implausible is not. See docs/corruption-taxonomy.md.</para>
/// </remarks>
public sealed class ByteOrderSwapDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.encoding.byte-order-swap";

    // TODO(argus): ByteOrderSwap
    /// <summary>Creates the stub.</summary>
    public ByteOrderSwapDetector()
        : base(DetectorId, HealthFlags.ByteOrderSwap)
    {
    }
}
