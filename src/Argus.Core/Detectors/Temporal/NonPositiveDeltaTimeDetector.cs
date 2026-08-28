using System.Globalization;
using Argus.Contracts;

namespace Argus.Detectors.Temporal;

/// <summary>
/// Reports an interval between arrivals that is zero or negative.
/// </summary>
/// <remarks>
/// <para>
/// This is the detector the prototype most needed and did not have. It had the check — a
/// <c>dt &lt;= 0</c> guard — but the guard incremented the sample counter and then returned,
/// so a stale or repeated arrival lowered the entity's health percentage without producing
/// anything that said why. The percentage moved and nothing explained it, which is the
/// precise shape of a diagnostic that generates arguments instead of settling them.
/// </para>
/// <para>
/// It is now a finding in its own right, and the counters it affects are separate:
/// <c>SamplesObserved</c> counts arrivals, <c>SamplesEvaluated</c> counts evaluations.
/// </para>
/// </remarks>
public sealed class NonPositiveDeltaTimeDetector : IDetector
{
    /// <summary>The stable identifier this detector stamps on its findings.</summary>
    public const string DetectorId = "argus.temporal.non-positive-delta-time";

    /// <inheritdoc />
    public string Id
    {
        get { return DetectorId; }
    }

    /// <inheritdoc />
    public HealthFlags Flag
    {
        get { return HealthFlags.NonPositiveDeltaTime; }
    }

    /// <inheritdoc />
    public DetectorStatus Status
    {
        get { return DetectorStatus.Implemented; }
    }

    /// <inheritdoc />
    public DetectorResult Evaluate(DetectorContext context)
    {
        if (!context.DeltaTimeSeconds.HasValue)
        {
            return DetectorResult.NotEvaluable(
                Flag,
                DetectorId,
                "this is the first sample seen for this entity, and an interval needs two arrivals");
        }

        double delta = context.DeltaTimeSeconds.Value;
        string measured = delta.ToString("G6", CultureInfo.InvariantCulture) + " s";

        if (delta <= 0.0)
        {
            return DetectorResult.Flagged(
                Flag,
                DetectorId,
                measured,
                "greater than 0 s",
                delta,
                "s");
        }

        return DetectorResult.Healthy(Flag, DetectorId, measured, "greater than 0 s", delta, "s");
    }
}
