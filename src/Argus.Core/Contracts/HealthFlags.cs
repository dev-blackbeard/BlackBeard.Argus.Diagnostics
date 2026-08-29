using System;

namespace Argus.Contracts;

/// <summary>
/// The conditions Argus can report about a single entity sample.
/// </summary>
/// <remarks>
/// <para>
/// These are <b>not</b> mutually exclusive. An entity can be a group outlier
/// <i>and</i> carry a non-normalised quaternion <i>and</i> have arrived out of order.
/// Detection never suppresses one condition because another fired; presentation picks
/// a single colour by severity precedence, and does so in <c>Argus.Graphics</c>.
/// </para>
/// <para>
/// The backing type is <see cref="ulong"/> rather than <see cref="int"/> purely for
/// headroom: the catalogue already occupies 28 bits and grows as encoding faults are
/// characterised. Values are explicit and must never be renumbered — a persisted
/// finding or a golden test locks them.
/// </para>
/// </remarks>
[Flags]
public enum HealthFlags : ulong
{
    /// <summary>No condition detected. The sample passed every detector that was evaluable.</summary>
    None = 0UL,

    // ---- Temporal -------------------------------------------------------------

    /// <summary>The interval since the previous sample for this entity was zero or negative.</summary>
    NonPositiveDeltaTime = 1UL << 0,

    /// <summary>The sample's payload is byte-for-byte equivalent to the previous sample for this entity.</summary>
    DuplicateSample = 1UL << 1,

    /// <summary>The sample's sequence number is lower than one already observed for this entity.</summary>
    OutOfOrderSequence = 1UL << 2,

    /// <summary>One or more sequence numbers between the previous sample and this one were never seen.</summary>
    SequenceGap = 1UL << 3,

    /// <summary>Position has not moved across several consecutive samples while reported velocity claims motion.</summary>
    FrozenEntity = 1UL << 4,

    /// <summary>The observed update interval has drifted away from the expected interval.</summary>
    UpdateRateDrift = 1UL << 5,

    /// <summary>The source timestamp and the arrival timestamp disagree by more than the permitted skew.</summary>
    ClockSkew = 1UL << 6,

    // ---- Encoding and framing -------------------------------------------------

    /// <summary>A field carried NaN, an infinity, or a subnormal value.</summary>
    NonFiniteValue = 1UL << 7,

    /// <summary>A field is implausible as read but plausible when its bytes are reversed.</summary>
    ByteOrderSwap = 1UL << 8,

    /// <summary>A fixed-point field was interpreted at the wrong scale, or a scaled field was read raw.</summary>
    FixedPointScaleError = 1UL << 9,

    /// <summary>Angular values appear to be in radians where degrees were expected.</summary>
    RadiansAsDegrees = 1UL << 10,

    /// <summary>Latitude and longitude appear to have been transposed.</summary>
    AxisSwap = 1UL << 11,

    /// <summary>
    /// Fields appear to have been read at the wrong offset — the signature of a
    /// framing misalignment in the producing serial struct.
    /// </summary>
    FieldShift = 1UL << 12,

    /// <summary>Positional resolution coarsened abruptly, consistent with a narrowing to 32-bit floating point.</summary>
    QuantisationCollapse = 1UL << 13,

    /// <summary>A field holds a value characteristic of uninitialised or filler memory.</summary>
    SentinelValue = 1UL << 14,

    // ---- Kinematic ------------------------------------------------------------

    /// <summary>The entity moved further between samples than the configured absolute distance gate permits.</summary>
    Teleport = 1UL << 15,

    /// <summary>The speed implied by consecutive positions exceeds the configured rate gate.</summary>
    ImplausibleSpeed = 1UL << 16,

    /// <summary>The acceleration implied by consecutive derived speeds exceeds the configured gate.</summary>
    ImplausibleAcceleration = 1UL << 17,

    /// <summary>The rate of altitude change exceeds the configured gate.</summary>
    ImplausibleAltitudeRate = 1UL << 18,

    /// <summary>Position is oscillating about a mean rather than describing a path.</summary>
    Jitter = 1UL << 19,

    /// <summary>Reported velocity and the velocity derived from consecutive positions disagree.</summary>
    VelocityMismatch = 1UL << 20,

    // ---- Attitude -------------------------------------------------------------

    /// <summary>Roll, pitch or yaw fell outside its defined range.</summary>
    AttitudeOutOfRange = 1UL << 21,

    /// <summary>An attitude angle jumped discontinuously across a wrap boundary.</summary>
    AttitudeWrapDiscontinuity = 1UL << 22,

    /// <summary>The supplied quaternion does not have unit magnitude.</summary>
    NonNormalisedQuaternion = 1UL << 23,

    /// <summary>Reported heading disagrees with the course derived from consecutive positions.</summary>
    HeadingCourseMismatch = 1UL << 24,

    // ---- Group ----------------------------------------------------------------

    /// <summary>The group's spread has grown beyond its configured cohesion radius.</summary>
    CohesionBreak = 1UL << 25,

    /// <summary>The entity lies further from the group centroid than the configured radius permits.</summary>
    GroupOutlier = 1UL << 26,

    /// <summary>The group's geometric arrangement has degenerated — contributors collapsed toward a point or scattered without structure.</summary>
    FormationCollapse = 1UL << 27,
}
