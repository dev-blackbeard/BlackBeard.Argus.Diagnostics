using Argus.Controls;
using Xunit;

namespace Argus.Controls.Tests;

public sealed class EntityKeyTests
{
    [Fact]
    public void SameEntityIdWithDifferentGroupTagsAreNotEqual()
    {
        var first = new EntityKey("entity-1", "north");
        var second = new EntityKey("entity-1", "south");

        Assert.NotEqual(first, second);
        Assert.False(first == second);
    }

    [Fact]
    public void SameEntityIdAndGroupTagAreEqual()
    {
        var first = new EntityKey("entity-1", "north");
        var second = new EntityKey("entity-1", "north");

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void NullGroupTagsAreEqualToEachOtherButNotToANamedGroup()
    {
        var untagged = new EntityKey("entity-1", null);
        var otherUntagged = new EntityKey("entity-1", null);
        var tagged = new EntityKey("entity-1", "north");

        Assert.Equal(untagged, otherUntagged);
        Assert.NotEqual(untagged, tagged);
    }
}
