using System;
using System.Collections.Generic;
using System.ComponentModel;
using Argus.Contracts;
using Argus.Controls;
using Argus.Graphics;
using Xunit;

namespace Argus.Controls.Tests;

public sealed class EntityHealthCollectionTests
{
    [Fact]
    public void ObserveCreatesOneRowPerDistinctKey()
    {
        var collection = new EntityHealthCollection();

        collection.Observe(HealthyReport("entity-1"));
        collection.Observe(HealthyReport("entity-2"));
        collection.Observe(HealthyReport("entity-1")); // same key again

        Assert.Equal(2, collection.Items.Count);
    }

    [Fact]
    public void SameEntityIdWithDifferentGroupTagsAreSeparateRows()
    {
        var collection = new EntityHealthCollection();

        collection.Observe(HealthyReport("entity-1"), groupTag: "north");
        collection.Observe(HealthyReport("entity-1"), groupTag: "south");

        Assert.Equal(2, collection.Items.Count);
    }

    [Fact]
    public void FlagCountsAccumulateAcrossRepeatedObserveCalls()
    {
        var collection = new EntityHealthCollection();

        collection.Observe(FlaggedReport("entity-1", HealthFlags.Teleport));
        collection.Observe(FlaggedReport("entity-1", HealthFlags.Teleport));
        collection.Observe(FlaggedReport("entity-1", HealthFlags.GroupOutlier));

        bool found = collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? item);

        Assert.True(found);
        Assert.NotNull(item);
        Assert.Equal(2L, item!.FlagCounts[HealthFlags.Teleport]);
        Assert.Equal(1L, item.FlagCounts[HealthFlags.GroupOutlier]);
    }

    [Fact]
    public void FlagCountsAccumulateBothBitsOfACombinedFlagReport()
    {
        var collection = new EntityHealthCollection();

        // A single report can raise more than one flag at once (architecture rule 4: detection
        // never suppresses), so a report combining two flags must increment both counters.
        collection.Observe(FlaggedReport("entity-1", HealthFlags.Teleport | HealthFlags.GroupOutlier));

        collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? item);

        Assert.Equal(1L, item!.FlagCounts[HealthFlags.Teleport]);
        Assert.Equal(1L, item.FlagCounts[HealthFlags.GroupOutlier]);
    }

    [Fact]
    public void ObservePropagatesTheSampleForDisplay()
    {
        var collection = new EntityHealthCollection();
        var sample = new EntitySample("entity-1", DateTime.UtcNow) { Latitude = 12.0, Longitude = 34.0 };

        collection.Observe(HealthyReport("entity-1"), sample);

        collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? item);

        Assert.Same(sample, item!.LatestSample);
    }

    [Fact]
    public void ObserveResolvesColourThroughTheConfiguredPolicy()
    {
        var colors = new ColorPolicy();
        var collection = new EntityHealthCollection(colors);
        EntityHealthReport report = FlaggedReport("entity-1", HealthFlags.Teleport);

        collection.Observe(report);

        collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? item);

        Assert.Equal(colors.Resolve(report), item!.Color);
    }

    [Fact]
    public void ItemViewModelRaisesPropertyChangedWhenColorChanges()
    {
        var collection = new EntityHealthCollection();
        collection.Observe(HealthyReport("entity-1"));
        collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? item);

        var raised = new List<string?>();
        item!.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        collection.Observe(FlaggedReport("entity-1", HealthFlags.Teleport));

        Assert.Contains(nameof(EntityHealthItemViewModel.Color), raised);
        Assert.Contains(nameof(EntityHealthItemViewModel.LatestReport), raised);
    }

    [Fact]
    public void ToggleExpandedCommandFlipsIsExpanded()
    {
        var collection = new EntityHealthCollection();
        collection.Observe(HealthyReport("entity-1"));
        collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? item);

        Assert.False(item!.IsExpanded);

        item.ToggleExpandedCommand.Execute(null);
        Assert.True(item.IsExpanded);

        item.ToggleExpandedCommand.Execute(null);
        Assert.False(item.IsExpanded);
    }

    [Fact]
    public void ObserveSetsReceiptOpacityToThePulsesPeak()
    {
        var collection = new EntityHealthCollection { Receipt = new ReceiptPulse { PeakOpacity = 0.6 } };

        collection.Observe(HealthyReport("entity-1"));

        collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? item);
        Assert.Equal(0.6, item!.ReceiptOpacity);
    }

    [Fact]
    public void RenderTickFadesReceiptOpacityForARowThatHasNotBeenObservedAgain()
    {
        var collection = new EntityHealthCollection { Receipt = new ReceiptPulse { FadeRenders = 4, PeakOpacity = 0.8 } };
        collection.Observe(HealthyReport("entity-1"));

        collection.RenderTick();
        collection.RenderTick();

        collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? item);
        Assert.Equal(0.4, item!.ReceiptOpacity);
    }

    [Fact]
    public void ObserveResetsReceiptOpacityToThePeakEvenAfterItHasFaded()
    {
        var collection = new EntityHealthCollection { Receipt = new ReceiptPulse { FadeRenders = 2, PeakOpacity = 0.6 } };
        collection.Observe(HealthyReport("entity-1"));

        collection.RenderTick();
        collection.RenderTick();
        collection.RenderTick();

        collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? faded);
        Assert.Equal(0.0, faded!.ReceiptOpacity);

        collection.Observe(HealthyReport("entity-1"));

        collection.TryGetItem(new EntityKey("entity-1", null), out EntityHealthItemViewModel? refreshed);
        Assert.Equal(0.6, refreshed!.ReceiptOpacity);
    }

    [Fact]
    public void ClearRemovesEveryRowAndForgetsEveryKey()
    {
        var collection = new EntityHealthCollection();
        collection.Observe(HealthyReport("entity-1"));

        collection.Clear();

        Assert.Empty(collection.Items);
        Assert.False(collection.TryGetItem(new EntityKey("entity-1", null), out _));
    }

    private static EntityHealthReport HealthyReport(string entityId)
    {
        return new EntityHealthReport(entityId, DateTime.UtcNow, Array.Empty<HealthFinding>(), samplesObserved: 1, samplesEvaluated: 1, samplesFlagged: 0);
    }

    private static EntityHealthReport FlaggedReport(string entityId, HealthFlags flags)
    {
        var findings = new List<HealthFinding>();
        foreach (HealthFlags flag in HealthFlagInfo.Split(flags))
        {
            findings.Add(HealthFinding.Flagged(flag, "test.detector", "measured", "expected"));
        }

        return new EntityHealthReport(entityId, DateTime.UtcNow, findings, samplesObserved: 1, samplesEvaluated: 1, samplesFlagged: 1);
    }
}
