using Argus.Contracts;

namespace Argus.Detectors.Kinematic;

/// <summary>
/// Reports a speed derived from consecutive positions that exceeds the rate gate.
/// </summary>
/// <remarks>
/// The other half of the pair described on <c>TeleportDetector</c>. This one divides by the
/// interval since the last <i>valid</i> sample rather than the last arrival: if three of the
/// last four samples were unusable, the displacement happened over four intervals, and
/// dividing it by one would manufacture a speed four times too high — a finding produced
/// entirely by the diagnostic tool.
/// </remarks>
public sealed class ImplausibleSpeedDetector : IDetector
{
    /// <summary>The stable identifier this detector stamps on its findings.</summary>
    public const string DetectorId = "argus.kinematic.implausible-speed";

    // Results that carry nothing specific to one sample are built once: HealthFinding is
    // immutable, and allocating an identical one per sample was pure garbage-collector load.
    private static readonly DetectorResult GateNotConfigured =
        DetectorResult.NotEvaluable(HealthFlags.ImplausibleSpeed, DetectorId, "DetectorThresholds.MaxSpeedMetersPerSecond is not configured, so there is no rate gate to compare against");

    private static readonly DetectorResult PositionNotUsable =
        DetectorResult.NotEvaluable(HealthFlags.ImplausibleSpeed, DetectorId, "this sample's position is not usable, so no speed can be derived from it");

    private static readonly DetectorResult NoPreviousValidPosition =
        DetectorResult.NotEvaluable(HealthFlags.ImplausibleSpeed, DetectorId, "no earlier valid position has been seen for this entity");

    private static readonly DetectorResult IntervalNotPositive =
        DetectorResult.NotEvaluable(HealthFlags.ImplausibleSpeed, DetectorId, "the interval since the previous valid sample is not positive, so a speed cannot be derived");

    private static readonly DetectorResult SpeedNotComputable =
        DetectorResult.NotEvaluable(HealthFlags.ImplausibleSpeed, DetectorId, "the speed since the previous valid position could not be computed");

    private static readonly DetectorResult HealthyUnrecorded = DetectorResult.HealthyWithoutDetail(HealthFlags.ImplausibleSpeed, DetectorId);

    /// <inheritdoc />
    public string Id
    {
        get { return DetectorId; }
    }

    /// <inheritdoc />
    public HealthFlags Flag
    {
        get { return HealthFlags.ImplausibleSpeed; }
    }

    /// <inheritdoc />
    public DetectorStatus Status
    {
        get { return DetectorStatus.Implemented; }
    }

    /// <inheritdoc />
    public DetectorResult Evaluate(DetectorContext context)
    {
        double? gate = context.Thresholds.MaxSpeedMetersPerSecond;
        if (!gate.HasValue)
        {
            return GateNotConfigured;
        }

        if (!context.PositionIsUsable)
        {
            return PositionNotUsable;
        }

        if (context.PreviousValidSample == null)
        {
            return NoPreviousValidPosition;
        }

        if (!context.ValidDeltaTimeSeconds.HasValue || context.ValidDeltaTimeSeconds.Value <= 0.0)
        {
            return IntervalNotPositive;
        }

        double? speed = context.DerivedSpeedMetersPerSecond();
        if (!speed.HasValue)
        {
            return SpeedNotComputable;
        }

        if (!(speed.Value > gate.Value) && !context.RecordHealthyDetail)
        {
            return HealthyUnrecorded;
        }

        string measured = HealthFinding.Quantity(speed.Value, "m/s");
        string expected = HealthFinding.AtMost(gate.Value, "m/s");

        if (speed.Value > gate.Value)
        {
            return DetectorResult.Flagged(Flag, DetectorId, measured, expected, speed.Value, "m/s");
        }

        return DetectorResult.Healthy(Flag, DetectorId, measured, expected, speed.Value, "m/s");
    }
}
