using System.Globalization;
using Argus.Contracts;

namespace Argus.Detectors.Temporal;

/// <summary>
/// Reports a sample whose payload repeats its predecessor's exactly.
/// </summary>
/// <remarks>
/// <para>
/// A duplicate is a first-class finding rather than something to be quietly discarded. A
/// stream that retransmits carries less information than its rate suggests, and a consumer
/// computing update rates or deriving velocity from it will be confidently wrong in a way
/// that looks entirely plausible on a map: the entity simply appears to have stopped.
/// </para>
/// <para>
/// Identity, arrival time and sequence number are excluded from the comparison. A producer
/// that retransmits with a fresh sequence number is still saying nothing new.
/// </para>
/// </remarks>
public sealed class DuplicateSampleDetector : IDetector
{
    /// <summary>The stable identifier this detector stamps on its findings.</summary>
    public const string DetectorId = "argus.temporal.duplicate-sample";

    /// <inheritdoc />
    public string Id
    {
        get { return DetectorId; }
    }

    /// <inheritdoc />
    public HealthFlags Flag
    {
        get { return HealthFlags.DuplicateSample; }
    }

    /// <inheritdoc />
    public DetectorStatus Status
    {
        get { return DetectorStatus.Implemented; }
    }

    /// <inheritdoc />
    public DetectorResult Evaluate(DetectorContext context)
    {
        EntitySample? previous = context.PreviousSeenSample;
        if (previous == null)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "this is the first sample seen for this entity, so there is nothing to compare it against");
        }

        double epsilon = context.Thresholds.DuplicatePayloadEpsilon;
        string expected = "at least one measurement field differing by more than "
            + epsilon.ToString("G6", CultureInfo.InvariantCulture);

        if (context.Sample.PayloadEquals(previous, epsilon))
        {
            return DetectorResult.Flagged(
                Flag,
                DetectorId,
                "every measurement field identical to the previous sample",
                expected);
        }

        return DetectorResult.Healthy(Flag, DetectorId, "payload differs from the previous sample", expected);
    }
}
