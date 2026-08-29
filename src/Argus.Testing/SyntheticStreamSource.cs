using System;
using System.Collections.Generic;
using System.Globalization;
using Argus.Contracts;
using Argus.Geodesy;

namespace Argus.Testing;

/// <summary>
/// Generates a clean synthetic stream from a <see cref="ScenarioDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// The output is deliberately boring: a line of entities evenly spaced along a bearing,
/// travelling together at a constant speed, with a normalised quaternion and a reported
/// velocity consistent with the motion. Nothing here is realistic, and nothing here is meant
/// to be — realism would mean encoding somebody's deployment into a public repository.
/// </para>
/// <para>
/// What it <i>is</i> is a stream that every detector should agree is clean, which is what
/// makes it a useful baseline: run it through the monitor and any finding at all is a false
/// positive. Then wrap it in an injector and the expected findings become exact.
/// </para>
/// </remarks>
public sealed class SyntheticStreamSource : IEntityStreamSource
{
    private readonly ScenarioDefinition _scenario;

    /// <summary>Creates a source.</summary>
    /// <param name="scenario">The scenario to generate. Defaults are used when <c>null</c>.</param>
    public SyntheticStreamSource(ScenarioDefinition? scenario = null)
    {
        _scenario = scenario ?? new ScenarioDefinition();
    }

    /// <summary>The scenario being generated.</summary>
    public ScenarioDefinition Scenario
    {
        get { return _scenario; }
    }

    /// <inheritdoc />
    public string Name
    {
        get { return _scenario.Name; }
    }

    /// <inheritdoc />
    public IEnumerable<StreamTick> Read()
    {
        double metersPerDegreeLatitude = Geo.EarthRadiusMeters * Geo.DegreesToRadians;
        double headingRadians = _scenario.HeadingDegrees * Geo.DegreesToRadians;

        for (int tickIndex = 0; tickIndex < _scenario.TickCount; tickIndex++)
        {
            double elapsedSeconds = tickIndex * _scenario.UpdateIntervalSeconds;
            DateTime tickTime = _scenario.StartTimeUtc.AddSeconds(elapsedSeconds);
            double travelled = _scenario.SpeedMetersPerSecond * elapsedSeconds;

            var samples = new List<EntitySample>(_scenario.EntityCount);

            for (int entityIndex = 0; entityIndex < _scenario.EntityCount; entityIndex++)
            {
                double alongTrack = _scenario.OriginOffsetMeters + (entityIndex * _scenario.SpacingMeters) + travelled;

                // A local flat-Earth offset from the origin. Accurate enough for a generator:
                // the point is that positions are consistent, not that they are survey grade.
                double northMeters = alongTrack * Math.Cos(headingRadians);
                double eastMeters = alongTrack * Math.Sin(headingRadians);

                double latitude = _scenario.OriginLatitude + (northMeters / metersPerDegreeLatitude);
                double cosLatitude = Math.Cos(latitude * Geo.DegreesToRadians);
                if (Math.Abs(cosLatitude) < 1e-9)
                {
                    cosLatitude = 1e-9;
                }

                double longitude = Geo.NormaliseLongitude(
                    _scenario.OriginLongitude + (eastMeters / (metersPerDegreeLatitude * cosLatitude)));

                string entityId = _scenario.EntityIdPrefix + entityIndex.ToString(CultureInfo.InvariantCulture);

                var sample = new EntitySample(entityId, tickTime)
                {
                    SourceTimeUtc = tickTime,
                    Latitude = latitude,
                    Longitude = longitude,
                    Altitude = _scenario.OriginAltitude,
                };

                if (_scenario.IncludeSequenceNumbers)
                {
                    sample.SequenceNumber = tickIndex;
                }

                if (_scenario.IncludeVelocity)
                {
                    sample.VelocityNorthMetersPerSecond = _scenario.SpeedMetersPerSecond * Math.Cos(headingRadians);
                    sample.VelocityEastMetersPerSecond = _scenario.SpeedMetersPerSecond * Math.Sin(headingRadians);
                    sample.VelocityDownMetersPerSecond = 0.0;
                    sample.HeadingDegrees = _scenario.HeadingDegrees;
                }

                if (_scenario.IncludeAttitude)
                {
                    // A yaw-only rotation, already normalised: sin squared plus cos squared is
                    // one, so the quaternion check passes exactly rather than approximately.
                    double halfYaw = 0.5 * _scenario.HeadingDegrees * Geo.DegreesToRadians;
                    sample.QuaternionX = 0.0;
                    sample.QuaternionY = 0.0;
                    sample.QuaternionZ = Math.Sin(halfYaw);
                    sample.QuaternionW = Math.Cos(halfYaw);
                    sample.RollDegrees = 0.0;
                    sample.PitchDegrees = 0.0;
                    sample.YawDegrees = _scenario.HeadingDegrees;
                }

                sample.RawFields = new List<RawField>
                {
                    new RawField("latitude", 0, 8, latitude),
                    new RawField("longitude", 8, 8, longitude),
                    new RawField("altitude", 16, 8, _scenario.OriginAltitude),
                }.AsReadOnly();

                sample.PayloadByteLength = 24;

                samples.Add(sample);
            }

            yield return new StreamTick(tickIndex, tickTime, samples.AsReadOnly());
        }
    }
}
