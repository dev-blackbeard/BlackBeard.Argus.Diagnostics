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
    public void ContrastColorPicksWhiteForABlackBackground()
    {
        Assert.Equal(Colors.White, ContrastColor.ForBackground(Colors.Black));
    }

    [Fact]
    public void ContrastColorPicksBlackForAWhiteBackground()
    {
        Assert.Equal(Colors.Black, ContrastColor.ForBackground(Colors.White));
    }

    [Fact]
    public void ContrastColorPicksBlackForAMidGreyBackground()
    {
        // Rec. 601 luma for an equal-channel grey is just that channel's value, so 0.6 sits
        // clearly above the 0.5 threshold -- light enough that black should read better.
        Assert.Equal(Colors.Black, ContrastColor.ForBackground(new Color(0.6f, 0.6f, 0.6f)));
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
    public void ReceiptPulseIsAtPeakForNonPositiveRendersSinceUpdate()
    {
        var pulse = new ReceiptPulse { FadeRenders = 8, PeakOpacity = 0.6 };

        Assert.Equal(0.6, pulse.Resolve(0));
        Assert.Equal(0.6, pulse.Resolve(-1));
    }

    [Fact]
    public void ReceiptPulseFadesLinearlyToZeroByFadeRenders()
    {
        var pulse = new ReceiptPulse { FadeRenders = 8, PeakOpacity = 0.8 };

        Assert.Equal(0.8, pulse.Resolve(0));
        Assert.Equal(0.4, pulse.Resolve(4));
        Assert.Equal(0.0, pulse.Resolve(8));
        Assert.Equal(0.0, pulse.Resolve(20));
    }

    [Fact]
    public void GetColorForFlagPrefersAnOverrideOverTheCategoryColor()
    {
        var overrideColor = new Color(0.1f, 0.2f, 0.3f);
        var policy = new ColorPolicy().Override(HealthFlags.Teleport, overrideColor);

        Assert.Equal(overrideColor, policy.GetColorForFlag(HealthFlags.Teleport));
    }

    [Fact]
    public void GetColorForFlagFallsBackToTheCategoryColorWithNoOverride()
    {
        var policy = new ColorPolicy();
        Color categoryColor = policy.GetColorForFlag(HealthFlags.GroupOutlier);
        Color anotherGroupFlag = policy.GetColorForFlag(HealthFlags.CohesionBreak);

        Assert.Equal(categoryColor, anotherGroupFlag);
    }

    [Fact]
    public void ResolveAndGetColorForFlagAgreeForASingleFlagReport()
    {
        var policy = new ColorPolicy();
        EntityHealthReport report = Report(HealthFinding.Flagged(HealthFlags.Teleport, "d", "m", "e"));

        Assert.Equal(policy.GetColorForFlag(HealthFlags.Teleport), policy.Resolve(report));
    }

    [Theory]
    [InlineData(HealthFlags.NonPositiveDeltaTime, AlarmGlyphKind.ReverseClock)]
    [InlineData(HealthFlags.DuplicateSample, AlarmGlyphKind.Duplicate)]
    [InlineData(HealthFlags.OutOfOrderSequence, AlarmGlyphKind.Shuffled)]
    [InlineData(HealthFlags.NonFiniteValue, AlarmGlyphKind.Infinity)]
    [InlineData(HealthFlags.Teleport, AlarmGlyphKind.JumpArrow)]
    [InlineData(HealthFlags.ImplausibleSpeed, AlarmGlyphKind.Chevron)]
    [InlineData(HealthFlags.NonNormalisedQuaternion, AlarmGlyphKind.SquashedCircle)]
    [InlineData(HealthFlags.GroupOutlier, AlarmGlyphKind.Outlier)]
    public void EveryImplementedFlagHasItsOwnDistinctGlyph(HealthFlags flag, AlarmGlyphKind expected)
    {
        Assert.Equal(expected, AlarmIconPainter.GetGlyphKind(flag));
    }

    [Fact]
    public void AnUnassignedFlagFallsBackToThePlaceholderGlyph()
    {
        // A stub detector's flag -- never assigned a shape, since it can never actually fire.
        Assert.Equal(AlarmGlyphKind.Placeholder, AlarmIconPainter.GetGlyphKind(HealthFlags.FieldShift));
    }

    [Fact]
    public void EveryImplementedGlyphIsDistinctFromEveryOther()
    {
        var kinds = new HashSet<AlarmGlyphKind>();
        foreach (HealthFlags flag in new[]
                 {
                     HealthFlags.NonPositiveDeltaTime,
                     HealthFlags.DuplicateSample,
                     HealthFlags.OutOfOrderSequence,
                     HealthFlags.NonFiniteValue,
                     HealthFlags.Teleport,
                     HealthFlags.ImplausibleSpeed,
                     HealthFlags.NonNormalisedQuaternion,
                     HealthFlags.GroupOutlier,
                 })
        {
            Assert.True(kinds.Add(AlarmIconPainter.GetGlyphKind(flag)), flag + " shares a glyph with another implemented flag.");
        }
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
