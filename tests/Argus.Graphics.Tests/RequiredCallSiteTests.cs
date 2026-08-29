using System;
using System.Collections.Generic;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Graphics;
using Microsoft.Maui.Graphics;
using Xunit;

namespace Argus.Graphics.Tests;

/// <summary>
/// The application's entity type, reproduced faithfully: a plain model class that knows
/// nothing about Argus and does not implement <see cref="IArgusEntity"/>.
/// </summary>
/// <remarks>
/// This is the point of the test. If this type implemented the interface, the call site would
/// prove only that the fast path works. It does not implement it, so the call site exercises
/// convention resolution — the route that has to work for an application that cannot take a
/// dependency on Argus from its model layer, which is most of them.
/// </remarks>
internal sealed class ApplicationEntity
{
    public int Id { get; set; }

    public double LatitudeWgs84 { get; set; }

    public double LongitudeWgs84 { get; set; }

    public double Altitude { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// The Part 3 contract: this call site must keep compiling, character for character.
/// </summary>
/// <remarks>
/// Treat a compile error in this file as a report that the facade's signature has regressed,
/// not as a test to be updated. In particular:
/// <list type="bullet">
/// <item><description>the last argument is positional, which pins <c>out string debugSubTitle</c>
/// to parameter nine;</description></item>
/// <item><description>both generic parameters must be inferable, or the call site would need
/// explicit type arguments;</description></item>
/// <item><description><c>teleportDistanceMeters: 1000</c> is an <c>int</c> literal, so the
/// parameter must accept an implicit widening to <c>double</c>.</description></item>
/// </list>
/// </remarks>
public sealed class RequiredCallSiteTests
{
    [Fact]
    public void TheExistingCallSiteCompilesAndReturnsAColour()
    {
        var _entityHealthMonitor = new EntityHealthMonitor();
        var obj = new ApplicationEntity
        {
            Id = 7,
            LatitudeWgs84 = 0.001,
            LongitudeWgs84 = 0.002,
            Altitude = 500.0,
            DisplayName = "seven",
        };

        var someEntityCollection = new List<ApplicationEntity> { obj };

        // --- verbatim, from the application ------------------------------------------------
        var StatusColor = _entityHealthMonitor.SetStatusColor(
            entityId: obj.Id,
            latitude: obj.LatitudeWgs84,
            longitude: obj.LongitudeWgs84,
            altitude: obj.Altitude,
            timestamp: DateTime.UtcNow,
            allEntities: someEntityCollection,
            teleportDistanceMeters: 1000,
            entityRadiusMeters: 50000, out string debugSubTitle);
        // -----------------------------------------------------------------------------------

        Assert.IsType<Color>(StatusColor);
        Assert.False(string.IsNullOrWhiteSpace(debugSubTitle));
    }

    [Fact]
    public void ConventionResolutionWorksForATypeThatDoesNotImplementIArgusEntity()
    {
        Assert.False(typeof(IArgusEntity).IsAssignableFrom(typeof(ApplicationEntity)));

        var monitor = new EntityHealthMonitor();
        var entities = new List<ApplicationEntity>();
        for (int i = 0; i < 5; i++)
        {
            entities.Add(new ApplicationEntity
            {
                Id = i,
                LatitudeWgs84 = 0.001 * (i + 1),
                LongitudeWgs84 = 0.001,
                Altitude = 500.0,
            });
        }

        string subtitle;
        monitor.SetStatusColor(
            entityId: entities[0].Id,
            latitude: entities[0].LatitudeWgs84,
            longitude: entities[0].LongitudeWgs84,
            altitude: entities[0].Altitude,
            timestamp: DateTime.UtcNow,
            allEntities: entities,
            teleportDistanceMeters: 1000,
            entityRadiusMeters: 50000, out subtitle);

        EntityHealthReport? report = monitor.LastReport;
        Assert.NotNull(report);

        // Convention resolution actually read the positions: the group check ran instead of
        // reporting that it could not find any contributors.
        Assert.DoesNotContain(
            report!.NotEvaluableFindings(),
            f => f.Flag == HealthFlags.GroupOutlier && f.Reason != null && f.Reason.Contains("identity"));
    }

    [Fact]
    public void MaxSpeedIsAvailableAlongsideTheDistanceGate()
    {
        var monitor = new EntityHealthMonitor();
        var entities = new List<ApplicationEntity>();

        string subtitle;
        monitor.SetStatusColor(
            entityId: 1,
            latitude: 0.001,
            longitude: 0.001,
            altitude: 100.0,
            timestamp: DateTime.UtcNow,
            allEntities: entities,
            teleportDistanceMeters: 1000,
            entityRadiusMeters: 50000,
            out subtitle,
            maxSpeedMetersPerSecond: 250.0);

        Assert.NotNull(monitor.LastReport);

        // Both gates are configured, so neither reports "no gate configured".
        foreach (HealthFinding finding in monitor.LastReport!.NotEvaluableFindings())
        {
            if (finding.Flag == HealthFlags.Teleport || finding.Flag == HealthFlags.ImplausibleSpeed)
            {
                Assert.DoesNotContain("not configured", finding.Reason ?? string.Empty, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void AnUnresolvableEntityTypeThrowsRatherThanReturningTheOrigin()
    {
        var monitor = new EntityHealthMonitor();
        var entities = new List<UnreadableEntity> { new UnreadableEntity() };

        // A block-bodied lambda, so this binds to Assert.Throws(Action) rather than being
        // ambiguous with the Func<object> overload.
        Assert.Throws<EntityAccessorException>(() =>
        {
            string subtitle;
            monitor.SetStatusColor(
                entityId: 1,
                latitude: 0.001,
                longitude: 0.001,
                altitude: 100.0,
                timestamp: DateTime.UtcNow,
                allEntities: entities,
                teleportDistanceMeters: 1000,
                entityRadiusMeters: 50000, out subtitle);
        });
    }

    [Fact]
    public void AccessorDelegateRouteAvoidsConventionEntirely()
    {
        var options = new MonitorOptions();
        options.Accessors.Register<UnreadableEntity>(
            e => e.Marker,
            e => e.First,
            e => e.Second);

        var monitor = new EntityHealthMonitor(options);
        var entities = new List<UnreadableEntity>
        {
            new UnreadableEntity { Marker = "u1", First = 0.001, Second = 0.001 },
        };

        string subtitle;
        monitor.SetStatusColor(
            entityId: "u1",
            latitude: 0.001,
            longitude: 0.001,
            altitude: 100.0,
            timestamp: DateTime.UtcNow,
            allEntities: entities,
            teleportDistanceMeters: 1000,
            entityRadiusMeters: 50000, out subtitle);

        Assert.NotNull(monitor.LastReport);
    }
}

/// <summary>A type nothing can read by convention.</summary>
internal sealed class UnreadableEntity
{
    public string Marker { get; set; } = string.Empty;

    public double First { get; set; }

    public double Second { get; set; }
}
