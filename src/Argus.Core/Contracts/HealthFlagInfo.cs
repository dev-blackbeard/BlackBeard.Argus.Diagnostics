using System;
using System.Collections.Generic;

namespace Argus.Contracts;

/// <summary>
/// The single source of truth for what each <see cref="HealthFlags"/> value means.
/// </summary>
/// <remarks>
/// Architecture rule 7 requires every finding to be self-describing: a reader who has
/// only the emitted finding — no repository access, no source — must be able to tell
/// what was checked, what was measured and what was expected. The one-line definitions
/// here are what travels with the finding to make that true, and they are the same
/// strings <c>docs/detector-catalogue.md</c> documents.
/// </remarks>
public static class HealthFlagInfo
{
    private static readonly Dictionary<HealthFlags, string> Definitions = new Dictionary<HealthFlags, string>
    {
        [HealthFlags.None] = "No condition detected.",

        [HealthFlags.NonPositiveDeltaTime] = "The interval between this sample and the previous sample for the same entity was zero or negative, so no rate can be derived from it.",
        [HealthFlags.DuplicateSample] = "This sample repeats the previous sample's payload exactly, so it carries no new information about the entity.",
        [HealthFlags.OutOfOrderSequence] = "This sample's sequence number is lower than one already observed, so samples are arriving out of the order they were produced in.",
        [HealthFlags.SequenceGap] = "One or more sequence numbers between the previous sample and this one never arrived.",
        [HealthFlags.FrozenEntity] = "The entity's position has not changed across several consecutive samples while its reported velocity claims it is moving.",
        [HealthFlags.UpdateRateDrift] = "The observed interval between samples has drifted away from the interval the stream is expected to hold.",
        [HealthFlags.ClockSkew] = "The timestamp the source stamped on this sample and the time it arrived disagree by more than the permitted skew.",

        [HealthFlags.NonFiniteValue] = "A numeric field carried NaN, an infinity, or a subnormal value, none of which can represent a real measurement.",
        [HealthFlags.ByteOrderSwap] = "A field is implausible as read but becomes plausible when its bytes are reversed, which is the signature of an endianness mismatch between producer and consumer.",
        [HealthFlags.FixedPointScaleError] = "A field's magnitude matches a fixed-point representation of the expected value rather than the value itself, or the inverse.",
        [HealthFlags.RadiansAsDegrees] = "Angular values are confined to a range consistent with radians in a field that is specified in degrees.",
        [HealthFlags.AxisSwap] = "Latitude and longitude are implausible as read but plausible when transposed.",
        [HealthFlags.FieldShift] = "Every field's magnitude matches the field a fixed number of bytes away, which is the signature of a framing misalignment in the producing struct.",
        [HealthFlags.QuantisationCollapse] = "Positional resolution coarsened abruptly, consistent with values having passed through a narrower floating-point type.",
        [HealthFlags.SentinelValue] = "A field holds a value characteristic of uninitialised or filler memory rather than a measurement.",

        [HealthFlags.Teleport] = "The entity moved further between consecutive samples than the absolute distance gate permits, regardless of how much time elapsed.",
        [HealthFlags.ImplausibleSpeed] = "The speed implied by consecutive positions and the elapsed time exceeds the rate gate.",
        [HealthFlags.ImplausibleAcceleration] = "The change in derived speed between consecutive intervals exceeds the acceleration gate.",
        [HealthFlags.ImplausibleAltitudeRate] = "The rate at which altitude is changing exceeds the vertical rate gate.",
        [HealthFlags.Jitter] = "Position is oscillating about a mean by more than the permitted amount rather than describing a path.",
        [HealthFlags.VelocityMismatch] = "The velocity the sample reports and the velocity derived from consecutive positions disagree by more than the permitted tolerance.",

        [HealthFlags.AttitudeOutOfRange] = "Roll, pitch or yaw fell outside the range its definition allows.",
        [HealthFlags.AttitudeWrapDiscontinuity] = "An attitude angle jumped across a wrap boundary by more than a continuous rotation could account for.",
        [HealthFlags.NonNormalisedQuaternion] = "The supplied quaternion's magnitude differs from one by more than the permitted tolerance, so it does not describe a pure rotation.",
        [HealthFlags.HeadingCourseMismatch] = "Reported heading and the course derived from consecutive positions disagree by more than the permitted tolerance while the entity is moving fast enough for the course to be meaningful.",

        [HealthFlags.CohesionBreak] = "The spread of the group about its centroid has grown beyond the configured cohesion radius.",
        [HealthFlags.GroupOutlier] = "This entity lies further from the group centroid than the configured radius permits, with the centroid computed from the other valid entities only.",
        [HealthFlags.FormationCollapse] = "The group's geometric arrangement has degenerated: contributors have collapsed toward a single point or scattered without recoverable structure.",
    };

    private static readonly Dictionary<HealthFlags, HealthFlagCategory> Categories = new Dictionary<HealthFlags, HealthFlagCategory>
    {
        [HealthFlags.NonPositiveDeltaTime] = HealthFlagCategory.Temporal,
        [HealthFlags.DuplicateSample] = HealthFlagCategory.Temporal,
        [HealthFlags.OutOfOrderSequence] = HealthFlagCategory.Temporal,
        [HealthFlags.SequenceGap] = HealthFlagCategory.Temporal,
        [HealthFlags.FrozenEntity] = HealthFlagCategory.Temporal,
        [HealthFlags.UpdateRateDrift] = HealthFlagCategory.Temporal,
        [HealthFlags.ClockSkew] = HealthFlagCategory.Temporal,

        [HealthFlags.NonFiniteValue] = HealthFlagCategory.Encoding,
        [HealthFlags.ByteOrderSwap] = HealthFlagCategory.Encoding,
        [HealthFlags.FixedPointScaleError] = HealthFlagCategory.Encoding,
        [HealthFlags.RadiansAsDegrees] = HealthFlagCategory.Encoding,
        [HealthFlags.AxisSwap] = HealthFlagCategory.Encoding,
        [HealthFlags.FieldShift] = HealthFlagCategory.Encoding,
        [HealthFlags.QuantisationCollapse] = HealthFlagCategory.Encoding,
        [HealthFlags.SentinelValue] = HealthFlagCategory.Encoding,

        [HealthFlags.Teleport] = HealthFlagCategory.Kinematic,
        [HealthFlags.ImplausibleSpeed] = HealthFlagCategory.Kinematic,
        [HealthFlags.ImplausibleAcceleration] = HealthFlagCategory.Kinematic,
        [HealthFlags.ImplausibleAltitudeRate] = HealthFlagCategory.Kinematic,
        [HealthFlags.Jitter] = HealthFlagCategory.Kinematic,
        [HealthFlags.VelocityMismatch] = HealthFlagCategory.Kinematic,

        [HealthFlags.AttitudeOutOfRange] = HealthFlagCategory.Attitude,
        [HealthFlags.AttitudeWrapDiscontinuity] = HealthFlagCategory.Attitude,
        [HealthFlags.NonNormalisedQuaternion] = HealthFlagCategory.Attitude,
        [HealthFlags.HeadingCourseMismatch] = HealthFlagCategory.Attitude,

        [HealthFlags.CohesionBreak] = HealthFlagCategory.Group,
        [HealthFlags.GroupOutlier] = HealthFlagCategory.Group,
        [HealthFlags.FormationCollapse] = HealthFlagCategory.Group,
    };

    /// <summary>Every single-bit flag in the catalogue, in declaration order.</summary>
    public static IReadOnlyList<HealthFlags> All { get; } = BuildAll();

    /// <summary>The bitwise union of every temporal flag.</summary>
    public static HealthFlags TemporalMask { get; } = MaskFor(HealthFlagCategory.Temporal);

    /// <summary>The bitwise union of every encoding and framing flag.</summary>
    public static HealthFlags EncodingMask { get; } = MaskFor(HealthFlagCategory.Encoding);

    /// <summary>The bitwise union of every kinematic flag.</summary>
    public static HealthFlags KinematicMask { get; } = MaskFor(HealthFlagCategory.Kinematic);

    /// <summary>The bitwise union of every attitude flag.</summary>
    public static HealthFlags AttitudeMask { get; } = MaskFor(HealthFlagCategory.Attitude);

    /// <summary>The bitwise union of every group flag.</summary>
    public static HealthFlags GroupMask { get; } = MaskFor(HealthFlagCategory.Group);

    /// <summary>
    /// Returns the one-line, human-readable definition of a single flag — the text that
    /// travels with a finding so it can be understood without repository access.
    /// </summary>
    /// <param name="flag">A single-bit flag value.</param>
    /// <returns>The definition, or a generic description if the flag is not a known single bit.</returns>
    public static string GetDefinition(HealthFlags flag)
    {
        string definition;
        if (Definitions.TryGetValue(flag, out definition))
        {
            return definition;
        }

        return "A combination of conditions: " + Describe(flag) + ".";
    }

    /// <summary>Returns the category a single flag belongs to.</summary>
    /// <param name="flag">A single-bit flag value.</param>
    /// <returns>The category, or <see cref="HealthFlagCategory.None"/> if the flag is not a known single bit.</returns>
    public static HealthFlagCategory GetCategory(HealthFlags flag)
    {
        HealthFlagCategory category;
        return Categories.TryGetValue(flag, out category) ? category : HealthFlagCategory.None;
    }

    /// <summary>Enumerates the individual set bits of a flag combination, in declaration order.</summary>
    /// <param name="flags">A flag combination.</param>
    /// <returns>The single-bit flags that are set.</returns>
    public static IEnumerable<HealthFlags> Split(HealthFlags flags)
    {
        foreach (HealthFlags candidate in All)
        {
            if ((flags & candidate) != HealthFlags.None)
            {
                yield return candidate;
            }
        }
    }

    /// <summary>Renders a flag combination as a stable, comma-separated list of flag names.</summary>
    /// <param name="flags">A flag combination.</param>
    /// <returns><c>"None"</c> if nothing is set, otherwise the set flag names.</returns>
    public static string Describe(HealthFlags flags)
    {
        if (flags == HealthFlags.None)
        {
            return nameof(HealthFlags.None);
        }

        var names = new List<string>();
        foreach (HealthFlags flag in Split(flags))
        {
            names.Add(flag.ToString());
        }

        return string.Join(", ", names.ToArray());
    }

    private static IReadOnlyList<HealthFlags> BuildAll()
    {
        var all = new List<HealthFlags>();
        foreach (object value in Enum.GetValues(typeof(HealthFlags)))
        {
            var flag = (HealthFlags)value;
            if (flag == HealthFlags.None)
            {
                continue;
            }

            all.Add(flag);
        }

        return all.AsReadOnly();
    }

    private static HealthFlags MaskFor(HealthFlagCategory category)
    {
        HealthFlags mask = HealthFlags.None;
        foreach (KeyValuePair<HealthFlags, HealthFlagCategory> pair in Categories)
        {
            if (pair.Value == category)
            {
                mask |= pair.Key;
            }
        }

        return mask;
    }
}
