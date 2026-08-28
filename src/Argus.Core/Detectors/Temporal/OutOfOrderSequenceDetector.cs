using System.Globalization;
using Argus.Contracts;

namespace Argus.Detectors.Temporal;

/// <summary>
/// Reports a sample whose sequence number has already been passed.
/// </summary>
/// <remarks>
/// <para>
/// Out-of-order arrival is its own fault with its own cause — a datagram transport
/// reordering, or two producers interleaving — and it is worth separating from the
/// non-positive interval it usually also produces. The two together say "reordered"; a
/// non-positive interval alone says "the clock went backwards", which is a different
/// problem with a different owner.
/// </para>
/// <para>
/// A repeated sequence number is reported here too. It is not strictly "out of order", but
/// it is the same class of fault, it is never correct, and giving it its own flag would add
/// a catalogue entry that says nothing the two existing ones do not.
/// </para>
/// </remarks>
public sealed class OutOfOrderSequenceDetector : IDetector
{
    /// <summary>The stable identifier this detector stamps on its findings.</summary>
    public const string DetectorId = "argus.temporal.out-of-order-sequence";

    /// <inheritdoc />
    public string Id
    {
        get { return DetectorId; }
    }

    /// <inheritdoc />
    public HealthFlags Flag
    {
        get { return HealthFlags.OutOfOrderSequence; }
    }

    /// <inheritdoc />
    public DetectorStatus Status
    {
        get { return DetectorStatus.Implemented; }
    }

    /// <inheritdoc />
    public DetectorResult Evaluate(DetectorContext context)
    {
        if (!context.Sample.SequenceNumber.HasValue)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "EntitySample.SequenceNumber was not supplied, so arrival order cannot be checked");
        }

        long? highest = context.Track.HighestSequenceNumber;
        if (!highest.HasValue)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "no earlier sequence number has been seen for this entity");
        }

        long sequence = context.Sample.SequenceNumber!.Value;
        string measured = sequence.ToString(CultureInfo.InvariantCulture);
        string expected = "greater than " + highest.Value.ToString(CultureInfo.InvariantCulture);

        if (sequence <= highest.Value)
        {
            return DetectorResult.Flagged(Flag, DetectorId, measured, expected, sequence, null);
        }

        return DetectorResult.Healthy(Flag, DetectorId, measured, expected, sequence, null);
    }
}
