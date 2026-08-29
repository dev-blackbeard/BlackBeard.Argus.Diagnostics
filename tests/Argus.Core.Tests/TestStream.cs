using System;
using System.Collections.Generic;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Geodesy;

namespace Argus.Core.Tests;

/// <summary>
/// Small helpers for building samples by hand.
/// </summary>
/// <remarks>
/// Everything here uses the synthetic origin. No test in this repository names a real place.
/// </remarks>
internal static class TestStream
{
    internal static readonly DateTime Epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Metres per degree of latitude, so offsets can be written in metres.</summary>
    internal static readonly double MetersPerDegreeLatitude = Geo.EarthRadiusMeters * Geo.DegreesToRadians;

    internal static EntitySample Sample(
        string entityId,
        double secondsFromEpoch,
        double? latitude = null,
        double? longitude = null,
        double? altitude = 100.0,
        long? sequenceNumber = null)
    {
        return new EntitySample(entityId, Epoch.AddSeconds(secondsFromEpoch))
        {
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude,
            SequenceNumber = sequenceNumber,
        };
    }

    /// <summary>A position a given number of metres north and east of the synthetic origin.</summary>
    internal static void Offset(double northMeters, double eastMeters, out double latitude, out double longitude)
    {
        latitude = northMeters / MetersPerDegreeLatitude;
        longitude = eastMeters / MetersPerDegreeLatitude;
    }

    internal static MonitorOptions Options()
    {
        var options = new MonitorOptions();
        options.Thresholds.MaxTeleportDistanceMeters = 1000.0;
        options.Thresholds.MaxSpeedMetersPerSecond = 500.0;
        options.Thresholds.GroupOutlierRadiusMeters = 5000.0;
        return options;
    }

    internal static List<EntitySample> Ring(double secondsFromEpoch, int count, double radiusMeters)
    {
        var samples = new List<EntitySample>(count);
        for (int i = 0; i < count; i++)
        {
            double angle = 2.0 * Math.PI * i / count;
            double latitude;
            double longitude;
            Offset(radiusMeters * Math.Cos(angle), radiusMeters * Math.Sin(angle), out latitude, out longitude);

            samples.Add(Sample("entity-" + i, secondsFromEpoch, latitude, longitude));
        }

        return samples;
    }
}
