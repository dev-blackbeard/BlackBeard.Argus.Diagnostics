using System;
using Argus.Contracts;
using Argus.Geodesy;
using Argus.State;
using Xunit;

namespace Argus.Core.Tests;

public sealed class GeodesyTests
{
    [Fact]
    public void DistanceIsSymmetricAndZeroForIdenticalPoints()
    {
        Assert.Equal(0.0, Geo.DistanceMeters(0.001, 0.002, 0.001, 0.002), 6);

        double forward = Geo.DistanceMeters(0.0, 0.0, 0.01, 0.01);
        double backward = Geo.DistanceMeters(0.01, 0.01, 0.0, 0.0);
        Assert.Equal(forward, backward, 6);
    }

    [Fact]
    public void DistanceAcrossTheAntimeridianIsShort()
    {
        // Two points a fifth of a degree apart, with the antimeridian between them. Subtracting
        // longitudes naively would give 359.8 degrees instead of 0.2.
        double distance = Geo.DistanceMeters(0.0, 179.9, 0.0, -179.9);
        Assert.True(distance < 25000.0, "expected a short hop across the antimeridian, got " + distance);
    }

    [Fact]
    public void NonFiniteInputsProduceNaNRatherThanANumber()
    {
        Assert.True(double.IsNaN(Geo.DistanceMeters(double.NaN, 0.0, 0.0, 0.0)));
        Assert.True(double.IsNaN(Geo.DistanceMeters(0.0, 0.0, 91.0, 0.0)));
    }

    [Fact]
    public void SubnormalsAreDetected()
    {
        Assert.True(Geo.IsSubnormal(double.Epsilon));
        Assert.False(Geo.IsSubnormal(0.0));
        Assert.False(Geo.IsSubnormal(1.0));
        Assert.False(Geo.IsSubnormal(double.NaN));
    }

    [Fact]
    public void AngularDifferenceWrapsTheShortWay()
    {
        Assert.Equal(2.0, Geo.AngularDifferenceDegrees(359.0, 1.0), 9);
        Assert.Equal(-2.0, Geo.AngularDifferenceDegrees(1.0, 359.0), 9);
    }

    [Fact]
    public void CentroidOfPointsStraddlingTheAntimeridianDoesNotLandAtZero()
    {
        var builder = new GroupTickContextBuilder(TestStream.Epoch);
        builder.Add("a", 0.0, 179.9, null);
        builder.Add("b", 0.0, -179.9, null);
        builder.Add("c", 0.0, 180.0, null);
        GroupTickContext context = builder.Build();

        Assert.True(context.HasCentroid);
        Assert.True(
            Math.Abs(Geo.AngularDifferenceDegrees(180.0, context.CentroidLongitudeDegrees)) < 1.0,
            "the centroid should be near the antimeridian, not at the prime meridian; got "
                + context.CentroidLongitudeDegrees);
    }

    [Fact]
    public void ZeroIslandIsRejectedByDefaultAndAcceptedWhenConfigured()
    {
        Assert.False(PositionValidity.IsUsable(0.0, 0.0, treatZeroIslandAsInvalid: true));
        Assert.True(PositionValidity.IsUsable(0.0, 0.0, treatZeroIslandAsInvalid: false));
        Assert.True(PositionValidity.IsUsable(0.001, 0.001, treatZeroIslandAsInvalid: true));
        Assert.False(PositionValidity.IsUsable(null, 0.001, treatZeroIslandAsInvalid: true));
        Assert.False(PositionValidity.IsUsable(91.0, 0.001, treatZeroIslandAsInvalid: true));
    }

    [Fact]
    public void InvalidContributorsNeverEnterTheCentroid()
    {
        var builder = new GroupTickContextBuilder(TestStream.Epoch);
        Assert.True(builder.Add("a", 0.001, 0.001, null));
        Assert.False(builder.Add("b", 999.0, 999.0, null));
        Assert.False(builder.Add("c", double.NaN, 0.001, null));
        Assert.False(builder.Add(null, 0.002, 0.002, null));

        GroupTickContext context = builder.Build();
        Assert.Equal(1, context.ContributorCount);
        Assert.Equal(4, context.SampleCount);
        Assert.False(context.IdentitiesResolved);
    }
}
