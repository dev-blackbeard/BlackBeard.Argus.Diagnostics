using Argus.Contracts;

namespace Argus.Detectors.Encoding;

/// <summary>
/// Reports fields that appear to have been read at the wrong offset within the frame.
/// </summary>
/// <remarks>
/// <para><b>Not implemented.</b> Declared here because the catalogue in
/// <c>docs/detector-catalogue.md</c> is the specification, and a condition that is specified
/// but not yet checked should be visible rather than absent. The registry skips it; set
/// <c>MonitorOptions.IncludeUnimplementedDetectors</c> to have it appear in reports as
/// <c>NotEvaluable</c>.</para>
/// <para><b>Intended method.</b> THE HIGHEST VALUE DETECTOR IN THE CATALOGUE, and the least obvious. For each offset in DetectorThresholds.FieldShiftByteOffsets, reinterpret EntitySample.RawFields as though every field had been read that many bytes away from where it should have been, and test whether all of them - a proportion of DetectorThresholds.FieldShiftAgreementFraction - become simultaneously plausible. The strength of the inference is that a single cause explains every field at once, which no coincidence of independent faults does. The full reasoning, including why cross-field magnitude plausibility is the right test and why the altitude field is usually where it shows first, is written up in docs/corruption-taxonomy.md; read that before implementing.</para>
/// </remarks>
public sealed class FieldShiftDetector : NotImplementedDetector
{
    /// <summary>The stable identifier this detector will stamp on its findings.</summary>
    public const string DetectorId = "argus.encoding.field-shift";

    // TODO(argus): FieldShift
    /// <summary>Creates the stub.</summary>
    public FieldShiftDetector()
        : base(DetectorId, HealthFlags.FieldShift)
    {
    }
}
