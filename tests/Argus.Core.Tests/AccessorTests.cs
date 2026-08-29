using System;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Internal;
using Xunit;

namespace Argus.Core.Tests;

/// <summary>A type with conventional property names and no knowledge of Argus.</summary>
internal sealed class ConventionEntity
{
    public int Id { get; set; }

    public double LatitudeWgs84 { get; set; }

    public double LongitudeWgs84 { get; set; }

    public double Altitude { get; set; }
}

/// <summary>A type whose property names match nothing.</summary>
internal sealed class OpaqueEntity
{
    public string Tag { get; set; } = string.Empty;

    public double Northing { get; set; }

    public double Easting { get; set; }
}

/// <summary>A type that implements the interface outright.</summary>
internal sealed class DirectEntity : IArgusEntity
{
    public string EntityId { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? Altitude { get; set; }
}

public sealed class AccessorTests
{
    [Fact]
    public void InterfaceRouteIsUsedWhenAvailable()
    {
        var options = new MonitorOptions();
        Func<DirectEntity, EntitySnapshot> accessor = EntityAccessorFactory.Resolve<DirectEntity>(options);

        EntitySnapshot snapshot = accessor(new DirectEntity
        {
            EntityId = "direct-1",
            Latitude = 0.001,
            Longitude = 0.002,
            Altitude = 300.0,
        });

        Assert.Equal("direct-1", snapshot.EntityId);
        Assert.Equal(0.001, snapshot.Latitude!.Value);
    }

    [Fact]
    public void ConventionRouteResolvesUnrelatedPropertyNames()
    {
        var options = new MonitorOptions();
        Func<ConventionEntity, EntitySnapshot> accessor = EntityAccessorFactory.Resolve<ConventionEntity>(options);

        EntitySnapshot snapshot = accessor(new ConventionEntity
        {
            Id = 42,
            LatitudeWgs84 = 0.003,
            LongitudeWgs84 = 0.004,
            Altitude = 250.0,
        });

        Assert.Equal("42", snapshot.EntityId);
        Assert.Equal(0.003, snapshot.Latitude!.Value);
        Assert.Equal(0.004, snapshot.Longitude!.Value);
        Assert.Equal(250.0, snapshot.Altitude!.Value);
    }

    [Fact]
    public void ConventionRouteCachesPerType()
    {
        var options = new MonitorOptions();
        Func<ConventionEntity, EntitySnapshot> first = EntityAccessorFactory.Resolve<ConventionEntity>(options);
        Func<ConventionEntity, EntitySnapshot> second = EntityAccessorFactory.Resolve<ConventionEntity>(options);

        Assert.Same(first, second);
    }

    [Fact]
    public void RegisteredDelegateResolvesATypeConventionCannot()
    {
        var options = new MonitorOptions();
        options.Accessors.Register<OpaqueEntity>(
            e => e.Tag,
            e => e.Northing,
            e => e.Easting);

        Func<OpaqueEntity, EntitySnapshot> accessor = EntityAccessorFactory.Resolve<OpaqueEntity>(options);
        EntitySnapshot snapshot = accessor(new OpaqueEntity { Tag = "t", Northing = 0.005, Easting = 0.006 });

        Assert.Equal("t", snapshot.EntityId);
        Assert.Equal(0.005, snapshot.Latitude!.Value);
    }

    [Fact]
    public void UnresolvableTypeThrowsAnActionableExceptionRatherThanReturningZero()
    {
        var options = new MonitorOptions();

        EntityAccessorException exception = Assert.Throws<EntityAccessorException>(
            () => EntityAccessorFactory.Resolve<OpaqueEntity>(options));

        // The message has to be usable by somebody who has never read this code.
        Assert.Contains("OpaqueEntity", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Latitude", exception.Message, StringComparison.Ordinal);
        Assert.Contains("IArgusEntity", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Accessors.Register", exception.Message, StringComparison.Ordinal);
        Assert.Contains("LatitudeCandidates", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomCandidateNamesAreHonoured()
    {
        var options = new MonitorOptions();
        options.LatitudeCandidates.Add("Northing");
        options.LongitudeCandidates.Add("Easting");
        options.IdentityCandidates.Add("Tag");

        Func<OpaqueEntity, EntitySnapshot> accessor = EntityAccessorFactory.Resolve<OpaqueEntity>(options);
        EntitySnapshot snapshot = accessor(new OpaqueEntity { Tag = "t", Northing = 0.007, Easting = 0.008 });

        Assert.Equal(0.007, snapshot.Latitude!.Value);
    }
}
