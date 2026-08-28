using System.Collections.Generic;
using Argus.Contracts;

namespace Argus.Golden.Tests;

/// <summary>
/// One locked expectation: this injector, against this catalogue, produces exactly these flags.
/// </summary>
public sealed class GoldenCase
{
    public GoldenCase(string name, HealthFlags expected, string note)
    {
        Name = name;
        Expected = expected;
        Note = note;
    }

    /// <summary>The scenario's name.</summary>
    public string Name { get; }

    /// <summary>The flags the run must raise, exactly.</summary>
    public HealthFlags Expected { get; }

    /// <summary>Why this expectation is what it is.</summary>
    public string Note { get; }

    public override string ToString()
    {
        return Name;
    }
}

/// <summary>
/// The golden table.
/// </summary>
/// <remarks>
/// <para>
/// A golden test is worth having here because the interesting property of a detector is not
/// "it fires on the bad input" but "it fires on the bad input and on nothing else". An
/// assertion that a flag is present passes just as happily when six other flags are also
/// firing spuriously, and spurious flags are how a diagnostic tool loses the argument it was
/// built to win. These cases therefore assert the flag set <i>exactly</i>.
/// </para>
/// <para>
/// <see cref="Pending"/> is the other half. Every injector whose detector is still a stub is
/// listed there, and <c>GoldenTests</c> asserts that the pending list matches the set of
/// unimplemented detectors precisely. Implement a detector and the golden tests fail until
/// its case is moved from <see cref="Pending"/> into the locked table — which is the point:
/// the backlog cannot be quietly abandoned.
/// </para>
/// </remarks>
public static class GoldenCases
{
    /// <summary>Locked expectations, running against implemented detectors.</summary>
    public static IReadOnlyList<GoldenCase> Locked { get; } = new List<GoldenCase>
    {
        new GoldenCase(
            "clean",
            HealthFlags.None,
            "The synthetic generator's output is clean by construction. Any flag here is a false positive, which makes this the most valuable case in the table."),

        new GoldenCase(
            "reorder",
            HealthFlags.OutOfOrderSequence | HealthFlags.NonPositiveDeltaTime,
            "Swapping adjacent ticks makes both fire together. The pair is the diagnosis: out-of-order alone could be a producer numbering bug, and a non-positive interval alone could be a clock going backwards."),

        new GoldenCase(
            "teleport",
            HealthFlags.Teleport | HealthFlags.ImplausibleSpeed | HealthFlags.GroupOutlier,
            "One entity displaced far enough to trip the distance gate also trips the rate gate and leaves the group. All three are true and all three are reported: detection never suppresses."),

        new GoldenCase(
            "non-finite",
            HealthFlags.NonFiniteValue,
            "A NaN in the position. The kinematic and group checks report NotEvaluable rather than healthy, so they contribute no flags."),

        new GoldenCase(
            "non-normalised-quaternion",
            HealthFlags.NonNormalisedQuaternion,
            "A quaternion of magnitude one half. Nothing about the position changes, so nothing else fires."),
    }.AsReadOnly();

    /// <summary>
    /// Faults the harness can inject that no implemented detector catches yet.
    /// </summary>
    /// <remarks>
    /// This list is asserted to equal exactly the set of unimplemented detectors. It is not a
    /// list of known failures to be tolerated; it is the backlog, kept honest by a test.
    /// </remarks>
    public static IReadOnlyList<HealthFlags> Pending { get; } = new List<HealthFlags>
    {
        HealthFlags.SequenceGap,
        HealthFlags.FrozenEntity,
        HealthFlags.UpdateRateDrift,
        HealthFlags.ClockSkew,
        HealthFlags.ByteOrderSwap,
        HealthFlags.FixedPointScaleError,
        HealthFlags.RadiansAsDegrees,
        HealthFlags.AxisSwap,
        HealthFlags.FieldShift,
        HealthFlags.QuantisationCollapse,
        HealthFlags.SentinelValue,
        HealthFlags.ImplausibleAcceleration,
        HealthFlags.ImplausibleAltitudeRate,
        HealthFlags.Jitter,
        HealthFlags.VelocityMismatch,
        HealthFlags.AttitudeOutOfRange,
        HealthFlags.AttitudeWrapDiscontinuity,
        HealthFlags.HeadingCourseMismatch,
        HealthFlags.CohesionBreak,
        HealthFlags.FormationCollapse,
    }.AsReadOnly();
}
