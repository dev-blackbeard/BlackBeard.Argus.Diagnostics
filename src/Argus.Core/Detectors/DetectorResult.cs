using Argus.Contracts;

namespace Argus.Detectors;

/// <summary>
/// What one detector concluded, ready to be turned into a <see cref="HealthFinding"/>.
/// </summary>
public readonly struct DetectorResult
{
    private DetectorResult(HealthFinding finding)
    {
        Finding = finding;
    }

    /// <summary>The finding this result carries.</summary>
    public HealthFinding Finding { get; }

    /// <summary>What the detector concluded.</summary>
    public DetectorOutcome Outcome
    {
        get { return Finding == null ? DetectorOutcome.NotEvaluable : Finding.Outcome; }
    }

    /// <summary>Wraps a finding as a result.</summary>
    /// <param name="finding">The finding.</param>
    /// <returns>The result.</returns>
    public static DetectorResult From(HealthFinding finding)
    {
        return new DetectorResult(finding);
    }

    /// <summary>Creates a result for a detected condition.</summary>
    /// <param name="flag">The condition detected.</param>
    /// <param name="detectorId">Stable identifier of the detector.</param>
    /// <param name="measured">The measured value, rendered for a human reader.</param>
    /// <param name="expected">The acceptable value or range, rendered for a human reader.</param>
    /// <param name="measuredValue">The measured value as a number, when it is a single scalar.</param>
    /// <param name="unit">The unit <paramref name="measuredValue"/> is expressed in.</param>
    /// <returns>The result.</returns>
    public static DetectorResult Flagged(
        HealthFlags flag,
        string detectorId,
        string measured,
        string expected,
        double? measuredValue = null,
        string? unit = null)
    {
        return new DetectorResult(HealthFinding.Flagged(flag, detectorId, measured, expected, measuredValue, unit));
    }

    /// <summary>Creates a result recording that the detector ran and found nothing.</summary>
    /// <param name="flag">The condition that was checked for and not found.</param>
    /// <param name="detectorId">Stable identifier of the detector.</param>
    /// <param name="measured">The measured value, rendered for a human reader.</param>
    /// <param name="expected">The acceptable value or range, rendered for a human reader.</param>
    /// <param name="measuredValue">The measured value as a number, when it is a single scalar.</param>
    /// <param name="unit">The unit <paramref name="measuredValue"/> is expressed in.</param>
    /// <returns>The result.</returns>
    public static DetectorResult Healthy(
        HealthFlags flag,
        string detectorId,
        string measured,
        string expected,
        double? measuredValue = null,
        string? unit = null)
    {
        return new DetectorResult(HealthFinding.Healthy(flag, detectorId, measured, expected, measuredValue, unit));
    }

    /// <summary>Creates a result recording that the detector could not run.</summary>
    /// <param name="flag">The condition that could not be checked.</param>
    /// <param name="detectorId">Stable identifier of the detector.</param>
    /// <param name="reason">Why it could not run. Name the missing field or unconfigured threshold.</param>
    /// <returns>The result.</returns>
    public static DetectorResult NotEvaluable(HealthFlags flag, string detectorId, string reason)
    {
        return new DetectorResult(HealthFinding.NotEvaluable(flag, detectorId, reason));
    }
}
