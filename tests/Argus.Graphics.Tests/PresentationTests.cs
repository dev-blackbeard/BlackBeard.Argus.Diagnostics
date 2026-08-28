using System;
using System.Collections.Generic;
using Argus.Configuration;
using Argus.Contracts;
using Microsoft.Maui.Graphics;
using Xunit;

namespace Argus.Graphics.Tests;

public sealed class PresentationTests
{
    private static EntityHealthReport Report(params HealthFinding[] findings)
    {
        return new EntityHealthReport("a", DateTime.UtcNow, new List<HealthFinding>(findings).AsReadOnly(), 10L, 10L, 1L);
    }

    [Fact]
    public void SeverityPrecedencePicksEncodingOverEverythingElse()
    {
        var policy = new ColorPolicy();

        HealthFlags combined = HealthFlags.GroupOutlier | HealthFlags.FieldShift | HealthFlags.Teleport;
        Assert.Equal(HealthFlags.FieldShift, policy.MostSevere(combined));
    }

    [Fact]
    public void SeverityPrecedenceFallsThroughTheCategories()
    {
        var policy = new ColorPolicy();

        Assert.Equal(HealthFlags.DuplicateSample, policy.MostSevere(HealthFlags.DuplicateSample | HealthFlags.Teleport));
        Assert.Equal(HealthFlags.Teleport, policy.MostSevere(HealthFlags.Teleport | HealthFlags.GroupOutlier));
        Assert.Equal(HealthFlags.GroupOutlier, policy.MostSevere(HealthFlags.GroupOutlier | HealthFlags.AttitudeOutOfRange));
    }

    [Fact]
    public void HealthyAndNotEvaluatedAreDifferentColours()
    {
        var policy = new ColorPolicy();

        EntityHealthReport healthy = Report();
        EntityHealthReport partial = Report(HealthFinding.NotEvaluable(HealthFlags.Teleport, "d", "no gate configured"));

        Assert.Equal(policy.HealthyColor, policy.Resolve(healthy));
        Assert.Equal(policy.NotEvaluatedColor, policy.Resolve(partial));
        Assert.NotEqual(policy.HealthyColor, policy.NotEvaluatedColor);
    }

    [Fact]
    public void FlashCadenceDimsOnAlternatePhasesAndNeverChangesTheHue()
    {
        var cadence = new FlashCadence { RendersPerPhase = 4, DimAlpha = 0.25f };
        var baseColor = new Color(0.8f, 0.2f, 0.2f);

        Color bright = cadence.Apply(baseColor, 0L);
        Color dim = cadence.Apply(baseColor, 4L);

        Assert.Equal(1.0f, bright.Alpha);
        Assert.Equal(0.25f, dim.Alpha);
        Assert.Equal(baseColor.Red, dim.Red);
        Assert.Equal(baseColor.Green, dim.Green);
        Assert.Equal(baseColor.Blue, dim.Blue);
    }

    [Fact]
    public void FlashCadenceIsNotConsultedForAHealthyEntity()
    {
        var policy = new ColorPolicy { Flash = new FlashCadence { RendersPerPhase = 1 } };
        EntityHealthReport healthy = Report();

        Assert.Equal(policy.Resolve(healthy, 0L), policy.Resolve(healthy, 1L));
    }

    [Fact]
    public void SubtitleIsSelfDescribing()
    {
        var formatter = new SubtitleFormatter();
        EntityHealthReport report = Report(
            HealthFinding.Flagged(HealthFlags.Teleport, "argus.kinematic.teleport", "12000 m", "at most 1000 m", 12000.0, "m"));

        string subtitle = formatter.Format(report);

        Assert.Contains("Teleport", subtitle, StringComparison.Ordinal);
        Assert.Contains("12000 m", subtitle, StringComparison.Ordinal);
        Assert.Contains("at most 1000 m", subtitle, StringComparison.Ordinal);
    }

    [Fact]
    public void SubtitleIsDeterministicForTheSameReport()
    {
        var formatter = new SubtitleFormatter();
        EntityHealthReport report = Report(
            HealthFinding.Flagged(HealthFlags.Teleport, "d", "12000 m", "at most 1000 m", 12000.0, "m"),
            HealthFinding.Flagged(HealthFlags.GroupOutlier, "d", "61000 m", "at most 50000 m", 61000.0, "m"));

        Assert.Equal(formatter.Format(report), formatter.Format(report));
    }

    [Fact]
    public void DetailFormatCarriesTheDefinitionSoItCanBeReadWithoutTheRepository()
    {
        var formatter = new SubtitleFormatter();
        EntityHealthReport report = Report(
            HealthFinding.Flagged(HealthFlags.FieldShift, "argus.encoding.field-shift", "shift of 8 bytes", "no shift", 8.0, "bytes"));

        string detail = formatter.FormatDetail(report);

        Assert.Contains(HealthFlagInfo.GetDefinition(HealthFlags.FieldShift), detail, StringComparison.Ordinal);
        Assert.Contains("argus.encoding.field-shift", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFacadeAssignsTheSubtitleExactlyOnceAndFromTheFormatter()
    {
        var options = new MonitorOptions();
        var formatter = new SubtitleFormatter();
        // Fully qualified on both sides: inside this namespace the unqualified name resolves to
        // the facade, and the two types deliberately share a name across the two assemblies.
        var engine = new global::Argus.Pipeline.EntityHealthMonitor(options);
        var facade = new global::Argus.Graphics.EntityHealthMonitor(engine, null, formatter);

        var entities = new List<ApplicationEntity>
        {
            new ApplicationEntity { Id = 1, LatitudeWgs84 = 0.001, LongitudeWgs84 = 0.001, Altitude = 10.0 },
        };

        string subtitle;
        facade.SetStatusColor(
            entityId: 1,
            latitude: 0.001,
            longitude: 0.001,
            altitude: 10.0,
            timestamp: DateTime.UtcNow,
            allEntities: entities,
            teleportDistanceMeters: 1000,
            entityRadiusMeters: 50000, out subtitle);

        Assert.Equal(formatter.Format(facade.LastReport), subtitle);
    }
}
