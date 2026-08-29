using System;
using Argus.Contracts;

namespace Argus.Cli;

/// <summary>
/// One line of a capture file, as it is deserialised.
/// </summary>
/// <remarks>
/// A separate type from <see cref="EntitySample"/> on purpose. The capture format is an
/// interchange concern with its own compatibility obligations, and binding a serialiser
/// directly to a domain contract is how a field rename becomes a breaking file-format change.
/// Every field is nullable, so an absent field stays absent rather than becoming zero.
/// </remarks>
internal sealed class CaptureRecord
{
    public string? EntityId { get; set; }

    public DateTime? ArrivalTimeUtc { get; set; }

    public DateTime? SourceTimeUtc { get; set; }

    public long? SequenceNumber { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? Altitude { get; set; }

    public double? RollDegrees { get; set; }

    public double? PitchDegrees { get; set; }

    public double? YawDegrees { get; set; }

    public double? HeadingDegrees { get; set; }

    public double? QuaternionX { get; set; }

    public double? QuaternionY { get; set; }

    public double? QuaternionZ { get; set; }

    public double? QuaternionW { get; set; }

    public double? VelocityNorthMetersPerSecond { get; set; }

    public double? VelocityEastMetersPerSecond { get; set; }

    public double? VelocityDownMetersPerSecond { get; set; }

    internal EntitySample? ToSample()
    {
        if (string.IsNullOrEmpty(EntityId) || !ArrivalTimeUtc.HasValue)
        {
            return null;
        }

        return new EntitySample(EntityId!, ArrivalTimeUtc.Value)
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
        };
    }
}
