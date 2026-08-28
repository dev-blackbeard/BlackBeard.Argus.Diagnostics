using System;
using Argus.Contracts;
using Argus.Geodesy;

namespace Argus.Detectors.Attitude;

/// <summary>
/// Reports a quaternion whose magnitude is not one.
/// </summary>
/// <remarks>
/// <para>
/// A rotation quaternion has unit magnitude by definition, so this check needs no knowledge
/// of what is being rotated, no tuning, and no deployment context. It is the attitude
/// category's equivalent of the NaN check: an invariant of the representation rather than a
/// judgement about the values.
/// </para>
/// <para>
/// It is also a good encoding canary. A quaternion whose components have been shifted,
/// rescaled or byte-swapped almost never still has unit magnitude, so this frequently fires
/// alongside an encoding flag and confirms it. That is exactly why detection must not
/// suppress: the pair together is a far more specific diagnosis than either alone, and the
/// prototype's <c>else if</c> chain would have shown only the first.
/// </para>
/// </remarks>
public sealed class QuaternionNormalisationDetector : IDetector
{
    /// <summary>The stable identifier this detector stamps on its findings.</summary>
    public const string DetectorId = "argus.attitude.non-normalised-quaternion";

    /// <inheritdoc />
    public string Id
    {
        get { return DetectorId; }
    }

    /// <inheritdoc />
    public HealthFlags Flag
    {
        get { return HealthFlags.NonNormalisedQuaternion; }
    }

    /// <inheritdoc />
    public DetectorStatus Status
    {
        get { return DetectorStatus.Implemented; }
    }

    /// <inheritdoc />
    public DetectorResult Evaluate(DetectorContext context)
    {
        EntitySample sample = context.Sample;

        if (!sample.QuaternionX.HasValue || !sample.QuaternionY.HasValue
            || !sample.QuaternionZ.HasValue || !sample.QuaternionW.HasValue)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "the sample did not supply all four quaternion components");
        }

        double x = sample.QuaternionX.Value;
        double y = sample.QuaternionY.Value;
        double z = sample.QuaternionZ.Value;
        double w = sample.QuaternionW.Value;

        if (!Geo.IsFinite(x) || !Geo.IsFinite(y) || !Geo.IsFinite(z) || !Geo.IsFinite(w))
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "one or more quaternion components is not finite; see the NonFiniteValue finding");
        }

        double norm = Math.Sqrt((x * x) + (y * y) + (z * z) + (w * w));
        double tolerance = context.Thresholds.QuaternionNormTolerance;
        double deviation = Math.Abs(norm - 1.0);

        string measured = HealthFinding.Quantity(norm, "(magnitude)");
        string expected = HealthFinding.Range(1.0 - tolerance, 1.0 + tolerance, "(magnitude)");

        if (deviation > tolerance)
        {
            return DetectorResult.Flagged(Flag, DetectorId, measured, expected, norm, null);
        }

        return DetectorResult.Healthy(Flag, DetectorId, measured, expected, norm, null);
    }
}
