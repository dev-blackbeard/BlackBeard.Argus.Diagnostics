using System;
using System.Collections.Generic;

namespace Argus.Contracts;

/// <summary>
/// One observation of one entity, as it was consumed from the stream.
/// </summary>
/// <remarks>
/// <para>
/// Every measurement field is nullable, and <b>an unsupplied field is <c>null</c>, never
/// zero</b> (architecture rule 6). This is load-bearing: a zero altitude and an absent
/// altitude are different facts, and conflating them is how a detector ends up reporting
/// "healthy" for something it never actually looked at. A detector that finds the field
/// it needs is <c>null</c> reports <see cref="DetectorOutcome.NotEvaluable"/>.
/// </para>
/// <para>
/// The type is a mutable data-transfer object by design: it is built once per arrival on
/// the reading thread and then treated as immutable. See <c>docs/threading.md</c>.
/// </para>
/// </remarks>
public sealed class EntitySample
{
    /// <summary>Creates a sample for an entity at a known arrival time.</summary>
    /// <param name="entityId">Stable identity of the entity within the stream.</param>
    /// <param name="arrivalTimeUtc">When the consumer received this sample, in UTC.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entityId"/> is <c>null</c>.</exception>
    public EntitySample(string entityId, DateTime arrivalTimeUtc)
    {
        if (entityId == null)
        {
            throw new ArgumentNullException(nameof(entityId));
        }

        EntityId = entityId;
        ArrivalTimeUtc = arrivalTimeUtc;
    }

    /// <summary>Stable identity of the entity within the stream.</summary>
    public string EntityId { get; set; }

    /// <summary>When the consumer received this sample, in UTC. Always supplied.</summary>
    public DateTime ArrivalTimeUtc { get; set; }

    /// <summary>When the producer stamped the sample, in UTC, if the protocol carries it.</summary>
    public DateTime? SourceTimeUtc { get; set; }

    /// <summary>Monotonic sequence number the producer assigned, if the protocol carries one.</summary>
    public long? SequenceNumber { get; set; }

    /// <summary>Latitude in degrees, positive north.</summary>
    public double? Latitude { get; set; }

    /// <summary>Longitude in degrees, positive east.</summary>
    public double? Longitude { get; set; }

    /// <summary>Altitude in metres above the reference ellipsoid.</summary>
    public double? Altitude { get; set; }

    /// <summary>Roll in degrees.</summary>
    public double? RollDegrees { get; set; }

    /// <summary>Pitch in degrees.</summary>
    public double? PitchDegrees { get; set; }

    /// <summary>Yaw in degrees.</summary>
    public double? YawDegrees { get; set; }

    /// <summary>Reported heading in degrees, measured clockwise from north.</summary>
    public double? HeadingDegrees { get; set; }

    /// <summary>Quaternion X component, if attitude is carried as a quaternion.</summary>
    public double? QuaternionX { get; set; }

    /// <summary>Quaternion Y component, if attitude is carried as a quaternion.</summary>
    public double? QuaternionY { get; set; }

    /// <summary>Quaternion Z component, if attitude is carried as a quaternion.</summary>
    public double? QuaternionZ { get; set; }

    /// <summary>Quaternion W component, if attitude is carried as a quaternion.</summary>
    public double? QuaternionW { get; set; }

    /// <summary>Reported velocity along the local north axis, in metres per second.</summary>
    public double? VelocityNorthMetersPerSecond { get; set; }

    /// <summary>Reported velocity along the local east axis, in metres per second.</summary>
    public double? VelocityEastMetersPerSecond { get; set; }

    /// <summary>Reported velocity along the local down axis, in metres per second.</summary>
    public double? VelocityDownMetersPerSecond { get; set; }

    /// <summary>Reported angular velocity about the X axis, in degrees per second.</summary>
    public double? AngularVelocityXDegreesPerSecond { get; set; }

    /// <summary>Reported angular velocity about the Y axis, in degrees per second.</summary>
    public double? AngularVelocityYDegreesPerSecond { get; set; }

    /// <summary>Reported angular velocity about the Z axis, in degrees per second.</summary>
    public double? AngularVelocityZDegreesPerSecond { get; set; }

    /// <summary>
    /// The raw numeric fields as they were decoded from the wire, keyed by the producing
    /// struct's field name.
    /// </summary>
    /// <remarks>
    /// Encoding detectors work from this when it is supplied: cross-field magnitude
    /// plausibility, and the field-shift inference in particular, need to see the fields
    /// in their declared order rather than after they have been mapped onto the semantic
    /// properties above. See <c>docs/corruption-taxonomy.md</c>.
    /// </remarks>
    public IReadOnlyList<RawField>? RawFields { get; set; }

    /// <summary>Length in bytes of the frame this sample was decoded from, if known.</summary>
    public int? PayloadByteLength { get; set; }

    /// <summary>
    /// Compares this sample's measurement fields with another's, within a tolerance.
    /// </summary>
    /// <param name="other">The sample to compare against.</param>
    /// <param name="epsilon">Absolute tolerance applied to every numeric comparison.</param>
    /// <returns><c>true</c> if every measurement field matches within tolerance.</returns>
    /// <remarks>
    /// Identity, arrival time and sequence number are deliberately excluded: a duplicate
    /// is a repeat of the <i>payload</i>, and a producer that retransmits with a fresh
    /// sequence number is still telling the consumer nothing new.
    /// </remarks>
    public bool PayloadEquals(EntitySample? other, double epsilon)
    {
        if (other == null)
        {
            return false;
        }

        return Close(Latitude, other.Latitude, epsilon)
            && Close(Longitude, other.Longitude, epsilon)
            && Close(Altitude, other.Altitude, epsilon)
            && Close(RollDegrees, other.RollDegrees, epsilon)
            && Close(PitchDegrees, other.PitchDegrees, epsilon)
            && Close(YawDegrees, other.YawDegrees, epsilon)
            && Close(HeadingDegrees, other.HeadingDegrees, epsilon)
            && Close(QuaternionX, other.QuaternionX, epsilon)
            && Close(QuaternionY, other.QuaternionY, epsilon)
            && Close(QuaternionZ, other.QuaternionZ, epsilon)
            && Close(QuaternionW, other.QuaternionW, epsilon)
            && Close(VelocityNorthMetersPerSecond, other.VelocityNorthMetersPerSecond, epsilon)
            && Close(VelocityEastMetersPerSecond, other.VelocityEastMetersPerSecond, epsilon)
            && Close(VelocityDownMetersPerSecond, other.VelocityDownMetersPerSecond, epsilon)
            && Close(AngularVelocityXDegreesPerSecond, other.AngularVelocityXDegreesPerSecond, epsilon)
            && Close(AngularVelocityYDegreesPerSecond, other.AngularVelocityYDegreesPerSecond, epsilon)
            && Close(AngularVelocityZDegreesPerSecond, other.AngularVelocityZDegreesPerSecond, epsilon);
    }

    /// <summary>Creates a shallow copy of this sample.</summary>
    /// <returns>A new sample carrying the same values.</returns>
    public EntitySample Clone()
    {
        return new EntitySample(EntityId, ArrivalTimeUtc)
        {
            SourceTimeUtc = SourceTimeUtc,
            SequenceNumber = SequenceNumber,
            Latitude = Latitude,
            Longitude = Longitude,
            Altitude = Altitude,
            RollDegrees = RollDegrees,
            PitchDegrees = PitchDegrees,
            YawDegrees = YawDegrees,
            HeadingDegrees = HeadingDegrees,
            QuaternionX = QuaternionX,
            QuaternionY = QuaternionY,
            QuaternionZ = QuaternionZ,
            QuaternionW = QuaternionW,
            VelocityNorthMetersPerSecond = VelocityNorthMetersPerSecond,
            VelocityEastMetersPerSecond = VelocityEastMetersPerSecond,
            VelocityDownMetersPerSecond = VelocityDownMetersPerSecond,
            AngularVelocityXDegreesPerSecond = AngularVelocityXDegreesPerSecond,
            AngularVelocityYDegreesPerSecond = AngularVelocityYDegreesPerSecond,
            AngularVelocityZDegreesPerSecond = AngularVelocityZDegreesPerSecond,
            RawFields = RawFields,
            PayloadByteLength = PayloadByteLength,
        };
    }

    private static bool Close(double? left, double? right, double epsilon)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return left.HasValue == right.HasValue;
        }

        double a = left.Value;
        double b = right.Value;

        // Two NaNs are the same payload even though NaN != NaN.
        if (double.IsNaN(a) || double.IsNaN(b))
        {
            return double.IsNaN(a) && double.IsNaN(b);
        }

        return Math.Abs(a - b) <= epsilon;
    }
}
