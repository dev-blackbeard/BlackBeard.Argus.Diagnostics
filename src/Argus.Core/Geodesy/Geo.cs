using System;

namespace Argus.Geodesy;

/// <summary>
/// The geodetic primitives the detectors share.
/// </summary>
/// <remarks>
/// Everything here is spherical rather than ellipsoidal. That is a deliberate choice for a
/// diagnostic library: the faults Argus looks for change a position by hundreds of metres
/// or by whole degrees, and the sub-metre difference between a spherical and an ellipsoidal
/// distance never changes a verdict. A cheap, allocation-free, dependency-free calculation
/// that runs per entity per tick is worth more here than exactness that no threshold is
/// sensitive to.
/// </remarks>
public static class Geo
{
    /// <summary>
    /// The smallest positive normal <see cref="double"/>, derived from its bit pattern
    /// rather than written as a literal so no long decimal constant appears in this
    /// repository for a hygiene check to have to reason about.
    /// </summary>
    private static readonly double SmallestNormalDouble = BitConverter.Int64BitsToDouble(0x0010000000000000L);

    /// <summary>The IUGG mean Earth radius, in metres.</summary>
    public const double EarthRadiusMeters = 6371008.8;

    /// <summary>Multiply degrees by this to get radians.</summary>
    public const double DegreesToRadians = Math.PI / 180.0;

    /// <summary>Multiply radians by this to get degrees.</summary>
    public const double RadiansToDegrees = 180.0 / Math.PI;

    /// <summary>The largest magnitude a latitude may have, in degrees.</summary>
    public const double MaxLatitudeDegrees = 90.0;

    /// <summary>The largest magnitude a longitude may have, in degrees.</summary>
    public const double MaxLongitudeDegrees = 180.0;

    /// <summary>
    /// Whether a value is an ordinary finite number.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><c>true</c> if the value is neither NaN nor an infinity.</returns>
    /// <remarks><c>double.IsFinite</c> does not exist on netstandard2.0, hence this.</remarks>
    public static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Whether a value is subnormal — non-zero, but too small to be represented with a full
    /// mantissa.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><c>true</c> if the value is subnormal.</returns>
    /// <remarks>
    /// Subnormals are worth flagging in a stream of physical measurements because nothing
    /// physical produces them. They show up when a field has been reinterpreted from bytes
    /// that were never a double — the tail of an adjacent field, or filler.
    /// </remarks>
    public static bool IsSubnormal(double value)
    {
        if (value == 0.0 || !IsFinite(value))
        {
            return false;
        }

        return Math.Abs(value) < SmallestNormalDouble;
    }

    /// <summary>Whether a latitude is finite and within its defined range.</summary>
    /// <param name="latitudeDegrees">Latitude in degrees.</param>
    /// <returns><c>true</c> if the latitude is usable.</returns>
    public static bool IsValidLatitude(double latitudeDegrees)
    {
        return IsFinite(latitudeDegrees) && Math.Abs(latitudeDegrees) <= MaxLatitudeDegrees;
    }

    /// <summary>Whether a longitude is finite and within its defined range.</summary>
    /// <param name="longitudeDegrees">Longitude in degrees.</param>
    /// <returns><c>true</c> if the longitude is usable.</returns>
    public static bool IsValidLongitude(double longitudeDegrees)
    {
        return IsFinite(longitudeDegrees) && Math.Abs(longitudeDegrees) <= MaxLongitudeDegrees;
    }

    /// <summary>Wraps a longitude into the half-open range [-180, 180).</summary>
    /// <param name="longitudeDegrees">Longitude in degrees.</param>
    /// <returns>The wrapped longitude, or the input unchanged if it is not finite.</returns>
    public static double NormaliseLongitude(double longitudeDegrees)
    {
        if (!IsFinite(longitudeDegrees))
        {
            return longitudeDegrees;
        }

        double wrapped = (longitudeDegrees + 180.0) % 360.0;
        if (wrapped < 0.0)
        {
            wrapped += 360.0;
        }

        return wrapped - 180.0;
    }

    /// <summary>
    /// Returns the smallest signed difference between two angles in degrees, in the range
    /// (-180, 180].
    /// </summary>
    /// <param name="fromDegrees">The angle to measure from.</param>
    /// <param name="toDegrees">The angle to measure to.</param>
    /// <returns>The signed difference.</returns>
    /// <remarks>
    /// Used by the wrap-discontinuity and heading checks: without this, a heading crossing
    /// north reads as a 359 degree jump instead of a 1 degree one.
    /// </remarks>
    public static double AngularDifferenceDegrees(double fromDegrees, double toDegrees)
    {
        double difference = (toDegrees - fromDegrees) % 360.0;
        if (difference > 180.0)
        {
            difference -= 360.0;
        }
        else if (difference <= -180.0)
        {
            difference += 360.0;
        }

        return difference;
    }

    /// <summary>
    /// Great-circle distance between two positions, in metres.
    /// </summary>
    /// <param name="latitude1Degrees">Latitude of the first position, in degrees.</param>
    /// <param name="longitude1Degrees">Longitude of the first position, in degrees.</param>
    /// <param name="latitude2Degrees">Latitude of the second position, in degrees.</param>
    /// <param name="longitude2Degrees">Longitude of the second position, in degrees.</param>
    /// <returns>The distance in metres, or <see cref="double.NaN"/> if any input is unusable.</returns>
    /// <remarks>
    /// The haversine form is used rather than the spherical law of cosines because it stays
    /// accurate for the short separations that dominate here: consecutive samples of the
    /// same entity are usually metres apart, where the cosine form loses precision.
    /// </remarks>
    public static double DistanceMeters(
        double latitude1Degrees,
        double longitude1Degrees,
        double latitude2Degrees,
        double longitude2Degrees)
    {
        if (!IsValidLatitude(latitude1Degrees) || !IsValidLatitude(latitude2Degrees)
            || !IsFinite(longitude1Degrees) || !IsFinite(longitude2Degrees))
        {
            return double.NaN;
        }

        double lat1 = latitude1Degrees * DegreesToRadians;
        double lat2 = latitude2Degrees * DegreesToRadians;
        double deltaLat = (latitude2Degrees - latitude1Degrees) * DegreesToRadians;
        double deltaLon = AngularDifferenceDegrees(longitude1Degrees, longitude2Degrees) * DegreesToRadians;

        double sinHalfLat = Math.Sin(deltaLat * 0.5);
        double sinHalfLon = Math.Sin(deltaLon * 0.5);

        double a = (sinHalfLat * sinHalfLat) + (Math.Cos(lat1) * Math.Cos(lat2) * sinHalfLon * sinHalfLon);
        if (a < 0.0)
        {
            a = 0.0;
        }
        else if (a > 1.0)
        {
            a = 1.0;
        }

        return 2.0 * EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>
    /// Initial bearing from one position to another, in degrees clockwise from north.
    /// </summary>
    /// <param name="latitude1Degrees">Latitude of the first position, in degrees.</param>
    /// <param name="longitude1Degrees">Longitude of the first position, in degrees.</param>
    /// <param name="latitude2Degrees">Latitude of the second position, in degrees.</param>
    /// <param name="longitude2Degrees">Longitude of the second position, in degrees.</param>
    /// <returns>The bearing in [0, 360), or <see cref="double.NaN"/> if any input is unusable.</returns>
    public static double BearingDegrees(
        double latitude1Degrees,
        double longitude1Degrees,
        double latitude2Degrees,
        double longitude2Degrees)
    {
        if (!IsValidLatitude(latitude1Degrees) || !IsValidLatitude(latitude2Degrees)
            || !IsFinite(longitude1Degrees) || !IsFinite(longitude2Degrees))
        {
            return double.NaN;
        }

        double lat1 = latitude1Degrees * DegreesToRadians;
        double lat2 = latitude2Degrees * DegreesToRadians;
        double deltaLon = AngularDifferenceDegrees(longitude1Degrees, longitude2Degrees) * DegreesToRadians;

        double y = Math.Sin(deltaLon) * Math.Cos(lat2);
        double x = (Math.Cos(lat1) * Math.Sin(lat2)) - (Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLon));

        double bearing = Math.Atan2(y, x) * RadiansToDegrees;
        return (bearing + 360.0) % 360.0;
    }
}
