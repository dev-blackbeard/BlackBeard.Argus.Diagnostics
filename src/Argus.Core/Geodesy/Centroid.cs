using System;

namespace Argus.Geodesy;

/// <summary>
/// A position on the unit sphere, in Cartesian form.
/// </summary>
/// <remarks>
/// Group statistics are accumulated in this form rather than as degrees. Averaging degrees
/// is wrong in two places that both occur in practice: across the antimeridian, where the
/// mean of +179 and -179 is 0 rather than 180, and near a pole, where longitudes converge
/// and their mean stops meaning anything. Summing unit vectors and normalising the sum has
/// neither failure, and it makes excluding one contributor a subtraction rather than a
/// re-scan (see <c>GroupTickContext</c>).
/// </remarks>
public readonly struct GeoVector : IEquatable<GeoVector>
{
    /// <summary>Creates a vector from its components.</summary>
    /// <param name="x">X component.</param>
    /// <param name="y">Y component.</param>
    /// <param name="z">Z component.</param>
    public GeoVector(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>The zero vector.</summary>
    public static GeoVector Zero
    {
        get { return new GeoVector(0.0, 0.0, 0.0); }
    }

    /// <summary>X component.</summary>
    public double X { get; }

    /// <summary>Y component.</summary>
    public double Y { get; }

    /// <summary>Z component.</summary>
    public double Z { get; }

    /// <summary>The vector's Euclidean magnitude.</summary>
    public double Magnitude
    {
        get { return Math.Sqrt((X * X) + (Y * Y) + (Z * Z)); }
    }

    /// <summary>Adds two vectors.</summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector.</param>
    /// <returns>The sum.</returns>
    public static GeoVector operator +(GeoVector left, GeoVector right)
    {
        return new GeoVector(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    /// <summary>Subtracts one vector from another.</summary>
    /// <param name="left">Vector to subtract from.</param>
    /// <param name="right">Vector to subtract.</param>
    /// <returns>The difference.</returns>
    public static GeoVector operator -(GeoVector left, GeoVector right)
    {
        return new GeoVector(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    /// <summary>Adds two vectors.</summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector.</param>
    /// <returns>The sum.</returns>
    public static GeoVector Add(GeoVector left, GeoVector right)
    {
        return left + right;
    }

    /// <summary>Subtracts one vector from another.</summary>
    /// <param name="left">Vector to subtract from.</param>
    /// <param name="right">Vector to subtract.</param>
    /// <returns>The difference.</returns>
    public static GeoVector Subtract(GeoVector left, GeoVector right)
    {
        return left - right;
    }

    /// <inheritdoc />
    public bool Equals(GeoVector other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is GeoVector other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + X.GetHashCode();
            hash = (hash * 31) + Y.GetHashCode();
            hash = (hash * 31) + Z.GetHashCode();
            return hash;
        }
    }

    /// <summary>Compares two vectors for equality.</summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector.</param>
    /// <returns><c>true</c> if the components are equal.</returns>
    public static bool operator ==(GeoVector left, GeoVector right)
    {
        return left.Equals(right);
    }

    /// <summary>Compares two vectors for inequality.</summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector.</param>
    /// <returns><c>true</c> if the components differ.</returns>
    public static bool operator !=(GeoVector left, GeoVector right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Converts between geodetic degrees and the unit-sphere vectors group statistics are
/// accumulated in.
/// </summary>
public static class Centroid
{
    /// <summary>
    /// The magnitude below which a summed vector is treated as having no direction.
    /// </summary>
    /// <remarks>
    /// A sum can cancel to near zero when contributors are spread symmetrically — antipodal
    /// pairs, or a ring around the globe. There is no meaningful centroid in that case, and
    /// returning one anyway would put the group's centre somewhere arbitrary. Callers get
    /// <c>false</c> and report <c>NotEvaluable</c> instead.
    /// </remarks>
    public const double DegenerateMagnitude = 1e-9;

    /// <summary>Converts a position in degrees to a unit vector.</summary>
    /// <param name="latitudeDegrees">Latitude in degrees.</param>
    /// <param name="longitudeDegrees">Longitude in degrees.</param>
    /// <returns>The unit vector for the position.</returns>
    public static GeoVector ToVector(double latitudeDegrees, double longitudeDegrees)
    {
        double lat = latitudeDegrees * Geo.DegreesToRadians;
        double lon = longitudeDegrees * Geo.DegreesToRadians;
        double cosLat = Math.Cos(lat);

        return new GeoVector(
            cosLat * Math.Cos(lon),
            cosLat * Math.Sin(lon),
            Math.Sin(lat));
    }

    /// <summary>
    /// Converts an accumulated vector sum back to a position in degrees.
    /// </summary>
    /// <param name="sum">The summed unit vectors.</param>
    /// <param name="latitudeDegrees">The resulting latitude in degrees.</param>
    /// <param name="longitudeDegrees">The resulting longitude in degrees.</param>
    /// <returns>
    /// <c>false</c> if the sum is degenerate — near zero magnitude, or not finite — in which
    /// case no centroid exists and the outputs are meaningless.
    /// </returns>
    public static bool TryToPosition(GeoVector sum, out double latitudeDegrees, out double longitudeDegrees)
    {
        latitudeDegrees = 0.0;
        longitudeDegrees = 0.0;

        if (!Geo.IsFinite(sum.X) || !Geo.IsFinite(sum.Y) || !Geo.IsFinite(sum.Z))
        {
            return false;
        }

        double magnitude = sum.Magnitude;
        if (magnitude < DegenerateMagnitude)
        {
            return false;
        }

        double x = sum.X / magnitude;
        double y = sum.Y / magnitude;
        double z = sum.Z / magnitude;

        if (z > 1.0)
        {
            z = 1.0;
        }
        else if (z < -1.0)
        {
            z = -1.0;
        }

        latitudeDegrees = Math.Asin(z) * Geo.RadiansToDegrees;
        longitudeDegrees = Math.Atan2(y, x) * Geo.RadiansToDegrees;
        return true;
    }
}
