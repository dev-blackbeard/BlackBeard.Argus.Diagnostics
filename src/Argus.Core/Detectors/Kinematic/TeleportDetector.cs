using Argus.Contracts;

namespace Argus.Detectors.Kinematic;

/// <summary>
/// Reports a displacement larger than the absolute distance gate, however much time elapsed.
/// </summary>
/// <remarks>
/// <para>
/// This is one half of a pair, and it is not redundant with the other. An absolute distance
/// gate catches a slow entity that jumps across a tick boundary — a discontinuity that a
/// rate gate forgives whenever the interval happens to be long enough to make the implied
/// speed acceptable. It misses, in return, a fast entity drifting steadily: a corrupted
/// value that moves an entity a little further every tick never trips a distance gate and
/// always trips a rate gate.
/// </para>
/// <para>
/// Run both. <c>ImplausibleSpeedDetector</c> is the other half.
/// </para>
/// <para>
/// The comparison is made against the last <i>valid</i> position, not the last one seen.
/// The prototype compared against the last seen, so the sample after an unusable one always
/// measured its displacement from wherever the unusable value had landed — which is how a
/// single <c>(0,0)</c> produced a fabricated jump of thousands of kilometres on the
/// following tick, every time.
/// </para>
/// </remarks>
public sealed class TeleportDetector : IDetector
{
    /// <summary>The stable identifier this detector stamps on its findings.</summary>
    public const string DetectorId = "argus.kinematic.teleport";

    /// <inheritdoc />
    public string Id
    {
        get { return DetectorId; }
    }

    /// <inheritdoc />
    public HealthFlags Flag
    {
        get { return HealthFlags.Teleport; }
    }

    /// <inheritdoc />
    public DetectorStatus Status
    {
        get { return DetectorStatus.Implemented; }
    }

    /// <inheritdoc />
    public DetectorResult Evaluate(DetectorContext context)
    {
        double? gate = context.Thresholds.MaxTeleportDistanceMeters;
        if (!gate.HasValue)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "DetectorThresholds.MaxTeleportDistanceMeters is not configured, so there is no distance gate to compare against");
        }

        if (!context.PositionIsUsable)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "this sample's position is not usable, so no displacement can be derived from it");
        }

        if (context.PreviousValidSample == null)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "no earlier valid position has been seen for this entity");
        }

        double? distance = context.DistanceFromPreviousValidMeters();
        if (!distance.HasValue)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "the distance from the previous valid position could not be computed");
        }

        string measured = HealthFinding.Quantity(distance.Value, "m");
        string expected = HealthFinding.AtMost(gate.Value, "m");

        if (distance.Value > gate.Value)
        {
            return DetectorResult.Flagged(Flag, DetectorId, measured, expected, distance.Value, "m");
        }

        return DetectorResult.Healthy(Flag, DetectorId, measured, expected, distance.Value, "m");
    }
}
