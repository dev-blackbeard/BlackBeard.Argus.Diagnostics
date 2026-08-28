using System.Collections.Generic;

namespace Argus.Configuration;

/// <summary>
/// Every number a detector is allowed to compare against.
/// </summary>
/// <remarks>
/// <para>
/// Architecture rule 5: no magic numbers in detector bodies. A threshold that lives inside
/// a detector cannot be tuned per deployment, cannot be reported alongside the finding it
/// produced, and cannot be argued with. Every one of them is a named, documented member
/// here, and every finding quotes the value it was compared against.
/// </para>
/// <para>
/// Two kinds of default appear below, and the difference is deliberate.
/// </para>
/// <para>
/// <b>Invariant defaults</b> are concrete, because the number follows from mathematics or
/// from the representation rather than from any particular stream — a quaternion's
/// magnitude is one everywhere, latitude spans the same range everywhere.
/// </para>
/// <para>
/// <b>Deployment gates</b> default to <c>null</c>, and a detector whose gate is <c>null</c>
/// reports <c>NotEvaluable</c> rather than guessing. A plausible-looking default for "how
/// fast can this move" or "how far apart can these be" would be a fabricated number
/// dressed as a library default: every consumer would inherit it, most would never look at
/// it, and the resulting findings would be confidently wrong. Refusing to answer until
/// configured is the honest behaviour, and it also keeps this repository free of any value
/// tuned to any real deployment.
/// </para>
/// </remarks>
public sealed class DetectorThresholds
{
    // ---- Temporal -------------------------------------------------------------

    /// <summary>
    /// The absolute tolerance applied when deciding whether two samples carry the same
    /// payload, in the units of whichever field is being compared.
    /// </summary>
    /// <remarks>
    /// Small enough that two genuinely distinct measurements never collide — a degree of
    /// latitude is roughly a hundred kilometres, so this is a sub-micrometre difference —
    /// and large enough to absorb a round trip through a decimal text encoding.
    /// </remarks>
    public double DuplicatePayloadEpsilon { get; set; } = 1e-12;

    /// <summary>
    /// How many sequence numbers may be missing between consecutive samples before a gap is
    /// reported.
    /// </summary>
    /// <remarks>Zero: any missing sequence number is a lost packet, and losing one is a fact worth knowing.</remarks>
    public long SequenceGapTolerance { get; set; }

    /// <summary>
    /// How far a position may move between samples and still count as static, in metres.
    /// </summary>
    /// <remarks>
    /// Half a metre. This is not a statement about how precisely anything is positioned; it
    /// is a floor below which "it moved" cannot be distinguished from the last bit of a
    /// coordinate wobbling, whatever the entity is.
    /// </remarks>
    public double StaticPositionEpsilonMeters { get; set; } = 0.5;

    /// <summary>
    /// How many consecutive static samples constitute a frozen entity.
    /// </summary>
    /// <remarks>
    /// Five. One repeat is a duplicate, two are a coincidence; a run long enough to be worth
    /// a distinct flag has to outlast both, and every additional sample delays the finding.
    /// </remarks>
    public int FrozenSampleRun { get; set; } = 5;

    /// <summary>
    /// The reported speed above which a static position is contradictory, in metres per second.
    /// </summary>
    /// <remarks>
    /// The frozen check compares two claims the sample makes about itself, so this only has
    /// to exceed the noise floor of a reported velocity that means "stationary".
    /// </remarks>
    public double FrozenReportedSpeedMetersPerSecond { get; set; } = 0.5;

    /// <summary>
    /// The interval the stream is expected to hold between samples of one entity, in
    /// seconds, or <c>null</c> if unknown.
    /// </summary>
    /// <remarks>Deployment gate. Update-rate drift is not evaluable without it.</remarks>
    public double? ExpectedUpdateIntervalSeconds { get; set; }

    /// <summary>
    /// The proportion by which the observed update interval may differ from
    /// <see cref="ExpectedUpdateIntervalSeconds"/> before drift is reported.
    /// </summary>
    /// <remarks>
    /// A factor of one half — the observed interval may be anywhere from half to one and a
    /// half times the expected one. Wide, because a stream that is merely jittery is not the
    /// thing this flag is for: it is for a stream that has changed cadence.
    /// </remarks>
    public double UpdateRateDriftTolerance { get; set; } = 0.5;

    /// <summary>
    /// The largest permitted difference between a sample's source timestamp and its arrival
    /// timestamp, in seconds, or <c>null</c> if unknown.
    /// </summary>
    /// <remarks>
    /// Deployment gate: the acceptable figure is transport latency plus clock discipline,
    /// both of which are properties of a particular deployment.
    /// </remarks>
    public double? MaxClockSkewSeconds { get; set; }

    // ---- Encoding and framing -------------------------------------------------

    /// <summary>Whether a subnormal value counts as non-finite for reporting purposes.</summary>
    /// <remarks>
    /// True. Nothing physical is measured to a magnitude that small; a subnormal in a
    /// position field means the bytes were never a position.
    /// </remarks>
    public bool TreatSubnormalAsNonFinite { get; set; } = true;

    /// <summary>
    /// The bound within which angular values are suspected of being radians rather than
    /// degrees, in the units of the field.
    /// </summary>
    /// <remarks>
    /// Slightly above pi. A field specified in degrees whose values never leave this bound is
    /// either radians or an entity that has never left a very small region; the detector
    /// therefore also requires <see cref="RadiansSuspicionMinimumSamples"/> before it will
    /// say so.
    /// </remarks>
    public double RadiansSuspicionBound { get; set; } = 3.15;

    /// <summary>
    /// How many consecutive samples must stay within <see cref="RadiansSuspicionBound"/>
    /// before radians-as-degrees is reported.
    /// </summary>
    /// <remarks>
    /// Eight. The false positive this guards against — a genuinely stationary entity near the
    /// origin — is indistinguishable from the fault on any single sample, so the evidence has
    /// to be a run.
    /// </remarks>
    public int RadiansSuspicionMinimumSamples { get; set; } = 8;

    /// <summary>
    /// The scale factor a fixed-point angular field is expected to carry.
    /// </summary>
    /// <remarks>
    /// Ten to the seventh: the conventional integer scaling for degrees, giving roughly
    /// centimetre resolution. This is a property of the representation, not of any deployment.
    /// </remarks>
    public double FixedPointScaleFactor { get; set; } = 1e7;

    /// <summary>
    /// The proportional tolerance applied when deciding whether a value matches a rescaled or
    /// reinterpreted form of itself.
    /// </summary>
    /// <remarks>
    /// One part in a thousand. The alternative interpretations an encoding detector tests are
    /// orders of magnitude apart, so the comparison does not need to be tight — it needs to be
    /// robust to the rounding that got the value here.
    /// </remarks>
    public double EncodingMatchRelativeTolerance { get; set; } = 1e-3;

    /// <summary>
    /// The byte offsets a field-shift inference will test.
    /// </summary>
    /// <remarks>
    /// Plus or minus one and two eight-byte fields. A misaligned read in a struct of doubles
    /// lands on a field boundary, and shifts beyond two fields are rare enough — and produce
    /// enough other symptoms — that testing further offsets buys more false positives than
    /// findings. See <c>docs/corruption-taxonomy.md</c>.
    /// </remarks>
    public IReadOnlyList<int> FieldShiftByteOffsets { get; set; } = new[] { -8, -4, 4, 8 };

    /// <summary>
    /// How many of a frame's fields must become simultaneously plausible under one shift
    /// before field shift is reported, as a proportion of the fields present.
    /// </summary>
    /// <remarks>
    /// All of them. The whole strength of the field-shift argument is that it explains every
    /// field at once with a single cause; an inference that explains most of them is a weaker
    /// claim than the individual per-field flags already being raised.
    /// </remarks>
    public double FieldShiftAgreementFraction { get; set; } = 1.0;

    /// <summary>
    /// How much coarser the smallest observed positional step may become before quantisation
    /// collapse is reported, as a multiple of the previously observed step.
    /// </summary>
    /// <remarks>
    /// A factor of sixteen. Narrowing a double to a float loses far more than that, so a
    /// threshold this loose still catches it while ignoring an entity that has merely slowed
    /// down.
    /// </remarks>
    public double QuantisationCoarseningFactor { get; set; } = 16.0;

    /// <summary>Whether an exact <c>(0, 0)</c> position is treated as an uninitialised value.</summary>
    /// <remarks>
    /// True. It is a legal position and it is also what a zeroed buffer decodes to, and the
    /// value alone cannot distinguish the two. See <c>Argus.Geodesy.PositionValidity</c> for
    /// why rejection is the conservative side of that trade.
    /// </remarks>
    public bool TreatZeroIslandAsSentinel { get; set; } = true;

    // ---- Kinematic ------------------------------------------------------------

    /// <summary>
    /// The furthest an entity may move between consecutive samples, in metres, regardless of
    /// how much time elapsed, or <c>null</c> if unconfigured.
    /// </summary>
    /// <remarks>
    /// Deployment gate. This is the absolute distance gate, and it exists alongside
    /// <see cref="MaxSpeedMetersPerSecond"/> rather than instead of it: a distance gate
    /// catches a slow entity that jumps across a tick boundary and misses a fast entity
    /// drifting steadily, and a rate gate does exactly the inverse. Configure both.
    /// </remarks>
    public double? MaxTeleportDistanceMeters { get; set; }

    /// <summary>
    /// The fastest an entity may travel, in metres per second, or <c>null</c> if unconfigured.
    /// </summary>
    /// <remarks>Deployment gate. This is the rate gate; see <see cref="MaxTeleportDistanceMeters"/>.</remarks>
    public double? MaxSpeedMetersPerSecond { get; set; }

    /// <summary>
    /// The largest permitted change in derived speed per second, in metres per second squared,
    /// or <c>null</c> if unconfigured.
    /// </summary>
    /// <remarks>Deployment gate.</remarks>
    public double? MaxAccelerationMetersPerSecondSquared { get; set; }

    /// <summary>
    /// The largest permitted rate of altitude change, in metres per second, or <c>null</c> if
    /// unconfigured.
    /// </summary>
    /// <remarks>Deployment gate.</remarks>
    public double? MaxAltitudeRateMetersPerSecond { get; set; }

    /// <summary>
    /// The largest permitted oscillation about the local mean position, in metres, or
    /// <c>null</c> if unconfigured.
    /// </summary>
    /// <remarks>Deployment gate: what counts as dither depends on the reporting precision of the source.</remarks>
    public double? MaxJitterMeters { get; set; }

    /// <summary>How many recent positions the jitter check considers.</summary>
    /// <remarks>Ten — enough for a mean to mean something, few enough to still be local in time.</remarks>
    public int JitterWindowSamples { get; set; } = 10;

    /// <summary>
    /// The largest permitted difference between reported and position-derived velocity, in
    /// metres per second, or <c>null</c> if unconfigured.
    /// </summary>
    /// <remarks>Deployment gate.</remarks>
    public double? MaxVelocityMismatchMetersPerSecond { get; set; }

    // ---- Attitude -------------------------------------------------------------

    /// <summary>The largest permitted magnitude of roll, in degrees.</summary>
    /// <remarks>A half turn: the range of the representation, not a limit on the entity.</remarks>
    public double MaxRollDegrees { get; set; } = 180.0;

    /// <summary>The largest permitted magnitude of pitch, in degrees.</summary>
    /// <remarks>A quarter turn: beyond this, pitch and roll have exchanged meanings.</remarks>
    public double MaxPitchDegrees { get; set; } = 90.0;

    /// <summary>The largest permitted magnitude of yaw, in degrees.</summary>
    /// <remarks>
    /// A full turn, which admits both conventions — zero to three-sixty and plus or minus a
    /// half turn — because a stream that uses the wider one is not thereby faulty.
    /// </remarks>
    public double MaxYawDegrees { get; set; } = 360.0;

    /// <summary>
    /// How far a quaternion's magnitude may differ from one before it is reported as
    /// non-normalised.
    /// </summary>
    /// <remarks>
    /// One part in a million. A rotation quaternion has unit magnitude by definition; this
    /// tolerance exists only for accumulated floating-point error, not for a range of
    /// acceptable magnitudes.
    /// </remarks>
    public double QuaternionNormTolerance { get; set; } = 1e-6;

    /// <summary>
    /// The largest instantaneous change in an attitude angle that a continuous rotation could
    /// account for, in degrees per sample.
    /// </summary>
    /// <remarks>
    /// A half turn less a margin. Anything at or beyond a half turn is ambiguous in direction,
    /// so it is the largest change that can be interpreted at all.
    /// </remarks>
    public double MaxAttitudeStepDegrees { get; set; } = 170.0;

    /// <summary>
    /// The largest permitted disagreement between reported heading and derived course, in
    /// degrees, or <c>null</c> if unconfigured.
    /// </summary>
    /// <remarks>Deployment gate: how closely the two agree is a property of what is being observed.</remarks>
    public double? MaxHeadingCourseDifferenceDegrees { get; set; }

    /// <summary>
    /// The speed below which derived course is too noisy for the heading comparison to mean
    /// anything, in metres per second.
    /// </summary>
    /// <remarks>
    /// One metre per second. Below this the derived course is dominated by positional noise
    /// and the comparison would flag a stationary entity for pointing the wrong way.
    /// </remarks>
    public double HeadingCourseMinimumSpeedMetersPerSecond { get; set; } = 1.0;

    // ---- Group ----------------------------------------------------------------

    /// <summary>
    /// How far an entity may lie from its group's centroid before it is reported as an
    /// outlier, in metres, or <c>null</c> if unconfigured.
    /// </summary>
    /// <remarks>
    /// Deployment gate. The centroid excludes the entity under test and every invalid entity;
    /// see <c>Argus.State.GroupTickContext</c>.
    /// </remarks>
    public double? GroupOutlierRadiusMeters { get; set; }

    /// <summary>
    /// How far the group's root-mean-square spread may grow before cohesion is reported as
    /// broken, in metres, or <c>null</c> if unconfigured.
    /// </summary>
    /// <remarks>Deployment gate.</remarks>
    public double? GroupCohesionRadiusMeters { get; set; }

    /// <summary>
    /// The spread below which a group is reported as collapsed toward a point, in metres, or
    /// <c>null</c> if unconfigured.
    /// </summary>
    /// <remarks>Deployment gate.</remarks>
    public double? GroupCollapseSpreadMeters { get; set; }

    /// <summary>
    /// How many other valid entities must remain after self-exclusion before a group check is
    /// evaluable.
    /// </summary>
    /// <remarks>
    /// Three. With one other entity a "centroid" is just that entity, and with two it is the
    /// midpoint of a line — in both cases the entity under test is compared against something
    /// that a single further fault would move arbitrarily. Three is the smallest count at
    /// which the centroid is a statement about a group rather than about one neighbour.
    /// </remarks>
    public int MinimumGroupContributors { get; set; } = 3;

    /// <summary>Creates a copy of these thresholds.</summary>
    /// <returns>An independent copy.</returns>
    /// <remarks>
    /// Used by the compatibility facade, which receives per-call gate values and must not
    /// mutate the thresholds shared by every other caller of the same monitor.
    /// </remarks>
    public DetectorThresholds Clone()
    {
        var copy = (DetectorThresholds)MemberwiseClone();
        copy.FieldShiftByteOffsets = new List<int>(FieldShiftByteOffsets);
        return copy;
    }
}
