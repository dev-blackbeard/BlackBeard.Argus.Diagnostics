using System.Linq;
using Argus.Contracts;
using Argus.Graphics;
using Xunit;

namespace Argus.Controls.Tests;

public sealed class LegendCatalogueTests
{
    [Fact]
    public void BuildEntriesStartsWithTheTwoHealthStateRows()
    {
        var entries = LegendCatalogue.BuildEntries(new ColorPolicy());

        Assert.Equal("Healthy", entries[0].Title);
        Assert.Null(entries[0].Flag);
        Assert.True(entries[0].IsSwatchOnly);

        Assert.Equal("Not evaluated", entries[1].Title);
        Assert.Null(entries[1].Flag);
    }

    [Fact]
    public void BuildEntriesHasExactlyOneRowPerImplementedFlagAfterTheHealthStateRows()
    {
        var entries = LegendCatalogue.BuildEntries(new ColorPolicy());

        var flaggedEntries = entries.Skip(2).ToList();

        Assert.Equal(LegendCatalogue.ImplementedFlags.Count, flaggedEntries.Count);
        foreach (LegendEntry entry in flaggedEntries)
        {
            Assert.True(entry.HasIcon);
            Assert.False(entry.IsSwatchOnly);
            Assert.Contains(entry.Flag!.Value, LegendCatalogue.ImplementedFlags);
        }
    }

    [Fact]
    public void BuildEntriesUsesTheSuppliedPolicysColours()
    {
        var overrideColor = new Microsoft.Maui.Graphics.Color(0.9f, 0.1f, 0.1f);
        var colors = new ColorPolicy().Override(HealthFlags.Teleport, overrideColor);

        var entries = LegendCatalogue.BuildEntries(colors);

        LegendEntry teleportEntry = entries.Single(e => e.Flag == HealthFlags.Teleport);
        Assert.Equal(overrideColor, teleportEntry.Color);
    }

    [Fact]
    public void BuildEntriesDefinitionsMatchHealthFlagInfo()
    {
        var entries = LegendCatalogue.BuildEntries(new ColorPolicy());

        foreach (LegendEntry entry in entries.Where(e => e.Flag.HasValue))
        {
            Assert.Equal(HealthFlagInfo.GetDefinition(entry.Flag!.Value), entry.Definition);
        }
    }

    [Fact]
    public void BuildEntriesForegroundAlwaysContrastsItsOwnColour()
    {
        var entries = LegendCatalogue.BuildEntries(new ColorPolicy());

        foreach (LegendEntry entry in entries)
        {
            Assert.Equal(ContrastColor.ForBackground(entry.Color), entry.Foreground);
        }
    }

    [Fact]
    public void BuildEntriesThrowsForANullPolicy()
    {
        Assert.Throws<System.ArgumentNullException>(() => LegendCatalogue.BuildEntries(null!));
    }
}
