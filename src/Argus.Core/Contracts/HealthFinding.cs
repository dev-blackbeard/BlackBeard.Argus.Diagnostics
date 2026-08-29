using System;
using System.Globalization;

namespace Argus.Contracts;

/// <summary>
/// One detector's conclusion about one sample, carrying everything a reader needs to
/// understand it without access to this repository.
/// </summary>
/// <remarks>
/// Architecture rule 7. A finding is an argument, and an argument that cannot be checked
/// is an impression. Every finding therefore states: the flag, its one-line definition,
/// what was measured, and what was expected. That is what makes it hard to dispute in a
/// conversation between the team producing the stream and the team consuming it — the
/// producing team can read the definition that generated the finding without needing
/// repository access.
/// </remarks>
public sealed class HealthFinding
{
    private HealthFinding(
        HealthFlags flag,
        string detectorId,
        DetectorOutcome outcome,
        string measured,
        string expected,
        double? measuredValue,
        string? unit,
        string? reason)
    {
        Flag = flag;
        DetectorId = detectorId;
        Outcome = outcome;
        Measured = measured;
        Expected = expected;
        MeasuredValue = measuredValue;
        Unit = unit;
        Reason = reason;
    }

    /// <summary>The condition this finding is about.</summary>
    public HealthFlags Flag { get; }

    /// <summary>The flag's name, carried explicitly so serialised findings stay readable.</summary>
    public string FlagName
    {
        get { return Flag.ToString(); }
    }

    /// <summary>The one-line, human-readable definition of <see cref="Flag"/>.</summary>
    public string Definition
    {
        get { return HealthFlagInfo.GetDefinition(Flag); }
    }

    /// <summary>The family <see cref="Flag"/> belongs to.</summary>
    public HealthFlagCategory Category
    {
        get { return HealthFlagInfo.GetCategory(Flag); }
    }

    /// <summary>Stable identifier of the detector that produced this finding.</summary>
    public string DetectorId { get; }

    /// <summary>What the detector concluded.</summary>
    public DetectorOutcome Outcome { get; }

    /// <summary>The measured value, rendered for a human reader.</summary>
    public string Measured { get; }

    /// <summary>The value or range that would have been acceptable, rendered for a human reader.</summary>
    public string Expected { get; }

    /// <summary>The measured value as a number, when the measurement is a single scalar.</summary>
    public double? MeasuredValue { get; }

    /// <summary>The unit <see cref="MeasuredValue"/> is expressed in, such as <c>"m"</c> or <c>"m/s"</c>.</summary>
    public string? Unit { get; }

    /// <summary>Why the detector could not run, when <see cref="Outcome"/> is <see cref="DetectorOutcome.NotEvaluable"/>.</summary>
    public string? Reason { get; }

    /// <summary>Creates a finding for a detected condition.</summary>
    /// <param name="flag">The condition detected.</param>
    /// <param name="detectorId">Stable identifier of the detector.</param>
    /// <param name="measured">The measured value, rendered for a human reader.</param>
    /// <param name="expected">The acceptable value or range, rendered for a human reader.</param>
    /// <param name="measuredValue">The measured value as a number, when it is a single scalar.</param>
    /// <param name="unit">The unit <paramref name="measuredValue"/> is expressed in.</param>
    /// <returns>The finding.</returns>
    public static HealthFinding Flagged(
        HealthFlags flag,
        string detectorId,
        string measured,
        string expected,
        double? measuredValue = null,
        string? unit = null)
    {
        return new HealthFinding(flag, detectorId, DetectorOutcome.Flagged, measured, expected, measuredValue, unit, null);
    }

    /// <summary>Creates a finding recording that the detector ran and found nothing.</summary>
    /// <param name="flag">The condition that was checked for and not found.</param>
    /// <param name="detectorId">Stable identifier of the detector.</param>
    /// <param name="measured">The measured value, rendered for a human reader.</param>
    /// <param name="expected">The acceptable value or range, rendered for a human reader.</param>
    /// <param name="measuredValue">The measured value as a number, when it is a single scalar.</param>
    /// <param name="unit">The unit <paramref name="measuredValue"/> is expressed in.</param>
    /// <returns>The finding.</returns>
    public static HealthFinding Healthy(
        HealthFlags flag,
        string detectorId,
        string measured,
        string expected,
        double? measuredValue = null,
        string? unit = null)
    {
        return new HealthFinding(flag, detectorId, DetectorOutcome.Healthy, measured, expected, measuredValue, unit, null);
    }

    /// <summary>Creates a finding recording that the detector could not run.</summary>
    /// <param name="flag">The condition that could not be checked.</param>
    /// <param name="detectorId">Stable identifier of the detector.</param>
    /// <param name="reason">
    /// Why it could not run — name the missing field or the unconfigured threshold, so the
    /// reader can act on it.
    /// </param>
    /// <returns>The finding.</returns>
    public static HealthFinding NotEvaluable(HealthFlags flag, string detectorId, string reason)
    {
        return new HealthFinding(flag, detectorId, DetectorOutcome.NotEvaluable, "not measured", "not evaluated", null, null, reason);
    }

    /// <summary>Renders the finding as a single self-describing line.</summary>
    /// <returns>A line of the form <c>FLAG (detector-id): measured X, expected Y — definition</c>.</returns>
    /// <remarks>
    /// Carries <see cref="DetectorId"/> as well as the flag: a reader pasted this line without
    /// repository access still needs a way to point back at exactly which check produced it,
    /// which the flag name alone does not give them once more than one detector can report the
    /// same flag.
    /// </remarks>
    public override string ToString()
    {
        if (Outcome == DetectorOutcome.NotEvaluable)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1}): not evaluable ({2}) - {3}",
                FlagName,
                DetectorId,
                Reason ?? "reason not recorded",
                Definition);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} ({1}): measured {2}, expected {3} - {4}",
            FlagName,
            DetectorId,
            Measured,
            Expected,
            Definition);
    }

    /// <summary>Formats a scalar measurement and its unit for the <see cref="Measured"/> and <see cref="Expected"/> fields.</summary>
    /// <param name="value">The value.</param>
    /// <param name="unit">The unit, such as <c>"m"</c>.</param>
    /// <returns>The rendered value, using the invariant culture so findings compare across locales.</returns>
    public static string Quantity(double value, string unit)
    {
        return value.ToString("G6", CultureInfo.InvariantCulture) + " " + unit;
    }

    /// <summary>Formats an inclusive numeric range for the <see cref="Expected"/> field.</summary>
    /// <param name="low">Lower bound.</param>
    /// <param name="high">Upper bound.</param>
    /// <param name="unit">The unit, such as <c>"deg"</c>.</param>
    /// <returns>The rendered range, using the invariant culture.</returns>
    public static string Range(double low, double high, string unit)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} to {1} {2}",
            low.ToString("G6", CultureInfo.InvariantCulture),
            high.ToString("G6", CultureInfo.InvariantCulture),
            unit);
    }

    /// <summary>Formats an upper bound for the <see cref="Expected"/> field.</summary>
    /// <param name="limit">The largest acceptable value.</param>
    /// <param name="unit">The unit, such as <c>"m"</c>.</param>
    /// <returns>The rendered bound, using the invariant culture.</returns>
    public static string AtMost(double limit, string unit)
    {
        return "at most " + limit.ToString("G6", CultureInfo.InvariantCulture) + " " + unit;
    }
}
